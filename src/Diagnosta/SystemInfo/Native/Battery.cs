using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Diagnosta.SystemInfo.Native;

/// <summary>
/// Battery charge (GetSystemPowerStatus) and wear (the battery driver's design vs. full-charge
/// capacity, the same numbers "powercfg /batteryreport" uses). Read-only access is enough.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class Battery
{
    private static readonly Guid BatteryInterface = new("72631e54-78a4-11d0-bcf7-00aa00b7b32a");

    private const uint DigcfPresent = 0x2;
    private const uint DigcfDeviceInterface = 0x10;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareReadWrite = 0x1 | 0x2;
    private const uint OpenExisting = 3;
    private const uint IoctlBatteryQueryTag = 0x294040;
    private const uint IoctlBatteryQueryInformation = 0x294044;
    private const uint CapacityRelative = 0x40000000;
    private const byte NoSystemBattery = 128;
    private const byte Unknown = 255;

    public static BatteryInfo Read()
    {
        if (!GetSystemPowerStatus(out var status) || status.BatteryFlag == Unknown)
        {
            return new BatteryInfo(Present: true); // there may be one, Windows just won't say
        }

        if ((status.BatteryFlag & NoSystemBattery) != 0)
        {
            return BatteryInfo.None;
        }

        var details = ReadFirstBatteryDetails();
        return new BatteryInfo(
            Present: true,
            ChargePercent: status.BatteryLifePercent == Unknown ? null : status.BatteryLifePercent,
            PluggedIn: status.ACLineStatus switch { 0 => false, 1 => true, _ => null },
            DesignedCapacity: details?.DesignedCapacity,
            FullChargeCapacity: details?.FullChargedCapacity,
            CycleCount: details?.CycleCount is > 0 ? details.Value.CycleCount : null,
            Chemistry: details is { } d ? ChemistryName(d) : null);
    }

    /// <summary>Finds the first battery device and asks its driver for design and full-charge capacity.</summary>
    private static BatteryInformation? ReadFirstBatteryDetails()
    {
        var devices = SetupDiGetClassDevsW(BatteryInterface, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (devices == -1)
        {
            return null;
        }

        try
        {
            var data = new DeviceInterfaceData { Size = (uint)sizeof(DeviceInterfaceData) };
            if (!SetupDiEnumDeviceInterfaces(devices, IntPtr.Zero, BatteryInterface, 0, ref data))
            {
                return null;
            }

            var path = DevicePath(devices, ref data);
            if (path is null)
            {
                return null;
            }

            using var battery = CreateFileW(path, GenericRead, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (battery.IsInvalid)
            {
                return null;
            }

            uint wait = 0;
            uint tag;
            if (!DeviceIoControl(battery, IoctlBatteryQueryTag, &wait, sizeof(uint), &tag, sizeof(uint), out _, IntPtr.Zero) || tag == 0)
            {
                return null; // tag 0: no battery in this slot
            }

            var query = new BatteryQueryInformation { BatteryTag = tag, InformationLevel = 0 /* BatteryInformation */ };
            BatteryInformation info;
            if (!DeviceIoControl(battery, IoctlBatteryQueryInformation, &query, (uint)sizeof(BatteryQueryInformation), &info, (uint)sizeof(BatteryInformation), out _, IntPtr.Zero))
            {
                return null;
            }

            // Relative capacities are percentages, not mWh, and say nothing about wear.
            return (info.Capabilities & CapacityRelative) != 0 ? null : info;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devices);
        }
    }

    private static string? DevicePath(IntPtr devices, ref DeviceInterfaceData data)
    {
        SetupDiGetDeviceInterfaceDetailW(devices, ref data, IntPtr.Zero, 0, out var required, IntPtr.Zero);
        if (required == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)required);
        try
        {
            // SP_DEVICE_INTERFACE_DETAIL_DATA_W: cbSize (8 on 64-bit, 6 on 32-bit) then the path.
            Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
            return SetupDiGetDeviceInterfaceDetailW(devices, ref data, buffer, required, out _, IntPtr.Zero)
                ? Marshal.PtrToStringUni(buffer + 4)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>The driver reports chemistry as four ASCII letters, e.g. "LION" or "LiP ".</summary>
    private static string? ChemistryName(BatteryInformation info)
    {
        var raw = Encoding.ASCII.GetString(new ReadOnlySpan<byte>(info.Chemistry, 4)).Trim('\0', ' ').ToUpperInvariant();
        return raw switch
        {
            "LION" or "LI-I" => "Li-Ion",
            "LIP" or "LIPO" => "Li-Polymer",
            "NIMH" => "NiMH",
            "" => null,
            _ => raw,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public uint Size;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BatteryQueryInformation
    {
        public uint BatteryTag;
        public int InformationLevel;
        public int AtRate;
    }

    /// <summary>BATTERY_INFORMATION from batclass.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BatteryInformation
    {
        public uint Capabilities;
        public byte Technology;
        public fixed byte Reserved[3];
        public fixed byte Chemistry[4];
        public uint DesignedCapacity;
        public uint FullChargedCapacity;
        public uint DefaultAlert1;
        public uint DefaultAlert2;
        public uint CriticalBias;
        public uint CycleCount;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    private static partial IntPtr SetupDiGetClassDevsW(in Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInterfaces(IntPtr devices, IntPtr deviceInfo, in Guid interfaceGuid, uint index, ref DeviceInterfaceData data);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDeviceInterfaceDetailW(IntPtr devices, ref DeviceInterfaceData data, IntPtr detail, uint detailSize, out uint requiredSize, IntPtr deviceInfo);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(IntPtr devices);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle device, uint ioControlCode, void* inBuffer, uint inBufferSize, void* outBuffer, uint outBufferSize, out uint bytesReturned, IntPtr overlapped);
}
