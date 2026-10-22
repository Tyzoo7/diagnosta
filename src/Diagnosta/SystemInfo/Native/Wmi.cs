using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Diagnosta.SystemInfo.Native;

/// <summary>
/// Reads the health of physical disks from Windows' storage service, the same source as
/// PowerShell's Get-PhysicalDisk. Works without admin rights.
/// </summary>
/// <remarks>
/// WMI is a COM API. The usual .NET wrapper (System.Management) relies on built-in COM
/// interop, which trimmed apps switch off. So, as with DXGI, we call the few functions we
/// need directly through their vtable positions, which are fixed by the Windows SDK (wbemcli.h).
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe partial class Wmi
{
    // vtable positions (IUnknown takes 0-2 in every interface)
    private const int ReleaseSlot = 2;
    private const int ConnectServerSlot = 3;   // IWbemLocator::ConnectServer
    private const int ExecQuerySlot = 20;      // IWbemServices::ExecQuery
    private const int NextSlot = 4;            // IEnumWbemClassObject::Next
    private const int GetSlot = 4;             // IWbemClassObject::Get

    private const uint ClsctxInprocServer = 1;
    private const int WbemFlagReturnImmediately = 0x10;
    private const int WbemFlagForwardOnly = 0x20;
    private const int WbemInfinite = -1;

    private const uint RpcCAuthnWinNt = 10;
    private const uint RpcCAuthzNone = 0;
    private const uint RpcCAuthnLevelCall = 3;
    private const uint RpcCImpLevelImpersonate = 3;
    private const uint EoacNone = 0;

    private const ushort VtBstr = 8;
    private const ushort VtI4 = 3;

    private const string StorageNamespace = @"ROOT\Microsoft\Windows\Storage";
    private const string Query = "SELECT FriendlyName, MediaType, BusType, HealthStatus FROM MSFT_PhysicalDisk";

    private static readonly Guid ClsidWbemLocator = new("4590f811-1d3a-11d0-891f-00aa004b2e24");
    private static readonly Guid IidWbemLocator = new("dc12a687-737f-11cf-884d-00aa004b2e24");

    /// <summary>All physical disks except USB, or null if Windows refuses to answer.</summary>
    public static IReadOnlyList<PhysicalDiskInfo>? ReadPhysicalDisks()
    {
        // COM must be initialised on this thread; it may already be (S_FALSE) or be in
        // another mode (RPC_E_CHANGED_MODE), which is fine for us as long as it works.
        var initHr = CoInitializeEx(IntPtr.Zero, 0 /* COINIT_MULTITHREADED */);
        try
        {
            return QueryDisks();
        }
        finally
        {
            if (initHr >= 0)
            {
                CoUninitialize();
            }
        }
    }

    private static List<PhysicalDiskInfo>? QueryDisks()
    {
        if (CoCreateInstance(ClsidWbemLocator, IntPtr.Zero, ClsctxInprocServer, IidWbemLocator, out var locator) < 0)
        {
            return null;
        }

        IntPtr services = IntPtr.Zero;
        IntPtr enumerator = IntPtr.Zero;
        var ns = Marshal.StringToBSTR(StorageNamespace);
        var language = Marshal.StringToBSTR("WQL");
        var query = Marshal.StringToBSTR(Query);
        try
        {
            var connect = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int, IntPtr, IntPtr, IntPtr*, int>)VTable(locator)[ConnectServerSlot];
            if (connect(locator, ns, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, &services) < 0
                || !AllowCallsAsCurrentUser(services))
            {
                return null;
            }

            var exec = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int, IntPtr, IntPtr*, int>)VTable(services)[ExecQuerySlot];
            if (exec(services, language, query, WbemFlagForwardOnly | WbemFlagReturnImmediately, IntPtr.Zero, &enumerator) < 0
                || !AllowCallsAsCurrentUser(enumerator))
            {
                return null;
            }

            return ReadAll(enumerator);
        }
        finally
        {
            Marshal.FreeBSTR(ns);
            Marshal.FreeBSTR(language);
            Marshal.FreeBSTR(query);
            Release(enumerator);
            Release(services);
            Release(locator);
        }
    }

    private static List<PhysicalDiskInfo>? ReadAll(IntPtr enumerator)
    {
        var disks = new List<PhysicalDiskInfo>();
        var next = (delegate* unmanaged[Stdcall]<IntPtr, int, uint, IntPtr*, uint*, int>)VTable(enumerator)[NextSlot];

        while (true)
        {
            IntPtr item;
            uint returned;
            var hr = next(enumerator, WbemInfinite, 1, &item, &returned);
            if (hr < 0)
            {
                return null;
            }

            if (returned == 0)
            {
                return disks; // end of the list
            }

            try
            {
                var busType = ReadInt(item, "BusType");
                if (busType == 7)
                {
                    continue; // USB: sticks and external drives would just add noise
                }

                disks.Add(new PhysicalDiskInfo(
                    Name: ReadString(item, "FriendlyName")?.Trim() is { Length: > 0 } name ? name : null,
                    Kind: ReadInt(item, "MediaType") switch
                    {
                        3 => DriveKind.Hdd,
                        4 => DriveKind.Ssd,
                        _ => DriveKind.Unknown,
                    },
                    Health: ReadInt(item, "HealthStatus") switch
                    {
                        0 => DiskHealth.Healthy,
                        1 => DiskHealth.Warning,
                        2 => DiskHealth.Unhealthy,
                        _ => DiskHealth.Unknown,
                    }));
            }
            finally
            {
                Release(item);
            }
        }
    }

    /// <summary>
    /// Lets WMI act on our behalf with our own (non-admin) rights. Without this, calls can
    /// fail with "access denied" depending on the system's default COM security.
    /// </summary>
    private static bool AllowCallsAsCurrentUser(IntPtr proxy) =>
        CoSetProxyBlanket(proxy, RpcCAuthnWinNt, RpcCAuthzNone, IntPtr.Zero, RpcCAuthnLevelCall, RpcCImpLevelImpersonate, IntPtr.Zero, EoacNone) >= 0;

    private static int? ReadInt(IntPtr item, string property)
    {
        var value = ReadVariant(item, property);
        try
        {
            // WMI returns small integer properties (uint16) as a 32-bit VT_I4.
            return value.Type == VtI4 ? unchecked((int)value.Data) : null;
        }
        finally
        {
            _ = VariantClear(&value);
        }
    }

    private static string? ReadString(IntPtr item, string property)
    {
        var value = ReadVariant(item, property);
        try
        {
            return value.Type == VtBstr && value.Data != 0 ? Marshal.PtrToStringBSTR(value.Data) : null;
        }
        finally
        {
            _ = VariantClear(&value);
        }
    }

    private static Variant ReadVariant(IntPtr item, string property)
    {
        var value = default(Variant);
        var get = (delegate* unmanaged[Stdcall]<IntPtr, char*, int, Variant*, int*, int*, int>)VTable(item)[GetSlot];
        fixed (char* name = property)
        {
            if (get(item, name, 0, &value, null, null) < 0)
            {
                return default;
            }
        }

        return value;
    }

    private static IntPtr* VTable(IntPtr comObject) => *(IntPtr**)comObject;

    private static void Release(IntPtr comObject)
    {
        if (comObject != IntPtr.Zero)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, uint>)VTable(comObject)[ReleaseSlot])(comObject);
        }
    }

    /// <summary>VARIANT: a type tag followed by the value (24 bytes on 64-bit Windows).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Variant
    {
        public ushort Type;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public nint Data;
        public nint Data2;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(IntPtr reserved, uint coInit);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid clsid, IntPtr outer, uint context, in Guid iid, out IntPtr instance);

    [LibraryImport("ole32.dll")]
    private static partial int CoSetProxyBlanket(IntPtr proxy, uint authnService, uint authzService, IntPtr serverPrincipalName, uint authnLevel, uint impLevel, IntPtr authInfo, uint capabilities);

    [LibraryImport("oleaut32.dll")]
    private static partial int VariantClear(Variant* variant);
}
