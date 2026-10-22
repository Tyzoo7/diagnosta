using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Diagnosta.SystemInfo.Native;

/// <summary>
/// Lists graphics cards through DXGI, the same Windows component games use to find GPUs.
/// Unlike WMI it reports dedicated memory above 4 GB correctly, and unlike the registry
/// it only lists cards that are actually present (no leftovers from removed GPUs).
/// </summary>
/// <remarks>
/// DXGI is a COM API: an object is a pointer to a table of function pointers (the "vtable").
/// We call the few functions we need directly by their position in that table. The
/// positions are fixed by the Windows SDK headers (dxgi.h) and never change.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe partial class Dxgi
{
    private const int ReleaseSlot = 2;          // IUnknown::Release
    private const int EnumAdapters1Slot = 12;   // IDXGIFactory1::EnumAdapters1
    private const int GetDesc1Slot = 10;        // IDXGIAdapter1::GetDesc1

    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
    private const uint DxgiAdapterFlagSoftware = 2;

    private static readonly Guid IidFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    public static IReadOnlyList<GpuInfo>? ReadGpus()
    {
        if (CreateDXGIFactory1(IidFactory1, out var factory) < 0)
        {
            return null;
        }

        try
        {
            var gpus = new List<GpuInfo>();
            var enumAdapters = (delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)VTable(factory)[EnumAdapters1Slot];

            for (uint index = 0; ; index++)
            {
                IntPtr adapter;
                var hr = enumAdapters(factory, index, &adapter);
                if (hr == DxgiErrorNotFound)
                {
                    break; // no more adapters
                }

                if (hr < 0)
                {
                    return null;
                }

                try
                {
                    if (ReadAdapter(adapter) is { } gpu)
                    {
                        gpus.Add(gpu);
                    }
                }
                finally
                {
                    Release(adapter);
                }
            }

            return gpus;
        }
        finally
        {
            Release(factory);
        }
    }

    private static GpuInfo? ReadAdapter(IntPtr adapter)
    {
        AdapterDesc1 desc;
        var getDesc = (delegate* unmanaged[Stdcall]<IntPtr, AdapterDesc1*, int>)VTable(adapter)[GetDesc1Slot];
        if (getDesc(adapter, &desc) < 0)
        {
            return null;
        }

        // Skip the "Microsoft Basic Render Driver": it is a software fallback, not a real card.
        if ((desc.Flags & DxgiAdapterFlagSoftware) != 0)
        {
            return null;
        }

        var name = new string(desc.Description).Trim();
        var memory = (ulong)desc.DedicatedVideoMemory;
        var shared = (ulong)desc.SharedSystemMemory;
        var (driverVersion, driverDate) = GpuDrivers.Find(desc.VendorId, desc.DeviceId);
        return new GpuInfo(name, memory > 0 ? memory : null, shared > 0 ? shared : null, driverVersion, driverDate);
    }

    private static IntPtr* VTable(IntPtr comObject) => *(IntPtr**)comObject;

    private static void Release(IntPtr comObject) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint>)VTable(comObject)[ReleaseSlot])(comObject);

    /// <summary>DXGI_ADAPTER_DESC1 from dxgi.h.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDesc1
    {
        public fixed char Description[128];
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public uint AdapterLuidLow;
        public int AdapterLuidHigh;
        public uint Flags;
    }

    [LibraryImport("dxgi.dll")]
    private static partial int CreateDXGIFactory1(in Guid riid, out IntPtr factory);
}
