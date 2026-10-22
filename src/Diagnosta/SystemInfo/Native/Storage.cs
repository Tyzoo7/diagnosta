using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Diagnosta.SystemInfo.Native;

/// <summary>
/// Asks the storage driver what kind of device sits behind a drive letter.
/// The volume is opened with access 0 ("no read, no write"), which is enough for
/// these queries and does not need admin rights.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class Storage
{
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageDeviceProperty = 0;
    private const int StorageDeviceSeekPenaltyProperty = 7;
    private const int PropertyStandardQuery = 0;

    private const uint FileShareReadWrite = 0x1 | 0x2;
    private const uint OpenExisting = 3;

    /// <summary>Type, bus and model of the device behind e.g. "C:", or nulls if Windows won't say.</summary>
    public static (DriveKind Kind, string? BusType, string? Model) Describe(string driveLetter)
    {
        using var volume = CreateFileW($@"\\.\{driveLetter}", 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (volume.IsInvalid)
        {
            return (DriveKind.Unknown, null, null);
        }

        var (busType, model) = QueryDevice(volume);
        return (QueryKind(volume), busType, model);
    }

    /// <summary>
    /// Hard disks have a "seek penalty" (a read head must move); SSDs don't.
    /// This is how Windows itself decides whether to defragment or TRIM a drive.
    /// </summary>
    private static DriveKind QueryKind(SafeFileHandle volume)
    {
        var query = new PropertyQuery { PropertyId = StorageDeviceSeekPenaltyProperty, QueryType = PropertyStandardQuery };
        SeekPenaltyDescriptor result;

        return DeviceIoControl(volume, IoctlStorageQueryProperty, &query, (uint)sizeof(PropertyQuery), &result, (uint)sizeof(SeekPenaltyDescriptor), out var returned, IntPtr.Zero)
            && returned >= (uint)sizeof(SeekPenaltyDescriptor)
                ? (result.IncursSeekPenalty != 0 ? DriveKind.Hdd : DriveKind.Ssd)
                : DriveKind.Unknown;
    }

    private static (string? BusType, string? Model) QueryDevice(SafeFileHandle volume)
    {
        var query = new PropertyQuery { PropertyId = StorageDeviceProperty, QueryType = PropertyStandardQuery };
        const int bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];

        if (!DeviceIoControl(volume, IoctlStorageQueryProperty, &query, (uint)sizeof(PropertyQuery), buffer, bufferSize, out var returned, IntPtr.Zero)
            || returned < (uint)sizeof(DeviceDescriptorHeader))
        {
            return (null, null);
        }

        var header = (DeviceDescriptorHeader*)buffer;
        return (BusTypeName(header->BusType), ReadAnsiString(buffer, returned, header->ProductIdOffset));
    }

    /// <summary>The descriptor stores strings as offsets into the same buffer, 0 meaning "none".</summary>
    private static string? ReadAnsiString(byte* buffer, uint length, uint offset)
    {
        if (offset == 0 || offset >= length)
        {
            return null;
        }

        var start = buffer + offset;
        var end = 0;
        while (offset + end < length && start[end] != 0)
        {
            end++;
        }

        var text = Encoding.ASCII.GetString(start, end).Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>STORAGE_BUS_TYPE values from winioctl.h, limited to those worth showing.</summary>
    private static string? BusTypeName(int busType) => busType switch
    {
        3 => "ATA",
        7 => "USB",
        11 => "SATA",
        12 => "SD",
        17 => "NVMe",
        _ => null,
    };

    /// <summary>STORAGE_PROPERTY_QUERY.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyQuery
    {
        public int PropertyId;
        public int QueryType;
        public byte AdditionalParameters;
    }

    /// <summary>DEVICE_SEEK_PENALTY_DESCRIPTOR.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SeekPenaltyDescriptor
    {
        public uint Version;
        public uint Size;
        public byte IncursSeekPenalty;
    }

    /// <summary>Fixed part of STORAGE_DEVICE_DESCRIPTOR; the strings follow in the buffer.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceDescriptorHeader
    {
        public uint Version;
        public uint Size;
        public byte DeviceType;
        public byte DeviceTypeModifier;
        public byte RemovableMedia;
        public byte CommandQueueing;
        public uint VendorIdOffset;
        public uint ProductIdOffset;
        public uint ProductRevisionOffset;
        public uint SerialNumberOffset; // never read: a unique device identifier
        public int BusType;
        public uint RawPropertiesLength;
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle device, uint ioControlCode, void* inBuffer, uint inBufferSize, void* outBuffer, uint outBufferSize, out uint bytesReturned, IntPtr overlapped);
}
