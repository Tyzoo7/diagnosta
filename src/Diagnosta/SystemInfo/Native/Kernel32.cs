using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Diagnosta.SystemInfo.Native;

/// <summary>Calls into kernel32.dll, the core Windows system library.</summary>
[SupportedOSPlatform("windows")]
internal static partial class Kernel32
{
    private const int ErrorInsufficientBuffer = 122;
    private const int RelationProcessorCore = 0;

    /// <summary>Total and available physical memory, or null if Windows refuses to say.</summary>
    public static MemoryInfo? ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status)
            ? new MemoryInfo(status.TotalPhys, status.AvailPhys)
            : null;
    }

    /// <summary>
    /// Counts physical CPU cores. Windows returns one variable-sized record per core,
    /// so we ask for the buffer size first, then walk the records.
    /// </summary>
    public static int? CountPhysicalCores()
    {
        uint length = 0;
        if (GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length)
            || Marshal.GetLastPInvokeError() != ErrorInsufficientBuffer)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
            {
                return null;
            }

            var cores = 0;
            for (var offset = 0; offset < length;)
            {
                // Each record starts with: int Relationship, uint Size.
                var size = Marshal.ReadInt32(buffer, offset + sizeof(int));
                if (size <= 0)
                {
                    break;
                }

                cores++;
                offset += size;
            }

            return cores > 0 ? cores : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// True when no other program shares our console window. That happens when
    /// Diagnosta is double-clicked in Explorer: Windows opens a new window just for us
    /// and closes it the moment we exit.
    /// </summary>
    public static unsafe bool IsAloneInConsoleWindow()
    {
        var processIds = stackalloc uint[2];
        return GetConsoleProcessList(processIds, 2) == 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static unsafe partial uint GetConsoleProcessList(uint* processIds, uint count);
}
