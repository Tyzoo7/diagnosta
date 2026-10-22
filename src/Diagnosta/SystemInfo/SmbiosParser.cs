using System.Buffers.Binary;
using System.Text;

namespace Diagnosta.SystemInfo;

/// <summary>
/// Reads RAM modules from the raw SMBIOS table, the hardware description the mainboard
/// firmware hands to Windows. Pure byte parsing, so it is fully unit tested.
/// Layout per the DMTF SMBIOS specification: each structure is a header (type, length,
/// handle), a formatted area, then a list of strings ending with two zero bytes.
/// </summary>
internal static class SmbiosParser
{
    private const int MemoryDeviceType = 17;
    private const int EndOfTableType = 127;

    // Offsets inside a type 17 "Memory Device" structure.
    private const int SizeOffset = 0x0C;
    private const int MemoryTypeOffset = 0x12;
    private const int SpeedOffset = 0x15;
    private const int ManufacturerOffset = 0x17;
    private const int PartNumberOffset = 0x1A;
    private const int ExtendedSizeOffset = 0x1C;
    private const int ConfiguredSpeedOffset = 0x20;
    // Serial number (0x18) is deliberately never read: it identifies the exact module.

    /// <summary>
    /// Parses the table data (without the 8-byte RawSMBIOSData header). Returns null if
    /// the firmware lists no memory slots at all.
    /// </summary>
    public static MemoryLayout? ParseMemory(ReadOnlySpan<byte> table)
    {
        var slots = 0;
        var modules = new List<MemoryModuleInfo>();
        var offset = 0;

        while (offset + 4 <= table.Length)
        {
            var type = table[offset];
            var length = table[offset + 1];
            if (length < 4 || offset + length > table.Length)
            {
                break; // malformed: stop rather than read garbage
            }

            var formatted = table.Slice(offset, length);
            var stringsStart = offset + length;
            var next = EndOfStrings(table, stringsStart);

            if (type == MemoryDeviceType && length > SpeedOffset + 1)
            {
                slots++;
                if (ReadModule(formatted, table[stringsStart..next]) is { } module)
                {
                    modules.Add(module);
                }
            }

            if (type == EndOfTableType)
            {
                break;
            }

            offset = next;
        }

        return slots == 0 ? null : new MemoryLayout(modules, slots);
    }

    private static MemoryModuleInfo? ReadModule(ReadOnlySpan<byte> s, ReadOnlySpan<byte> strings)
    {
        var size = BinaryPrimitives.ReadUInt16LittleEndian(s[SizeOffset..]);
        ulong bytes;
        if (size == 0 || size == 0xFFFF)
        {
            return null; // empty slot, or size unknown
        }
        else if (size == 0x7FFF && s.Length >= ExtendedSizeOffset + 4)
        {
            bytes = (ulong)(BinaryPrimitives.ReadUInt32LittleEndian(s[ExtendedSizeOffset..]) & 0x7FFFFFFF) * 1024 * 1024;
        }
        else
        {
            // Bit 15 set: the value is in KB instead of MB.
            bytes = (size & 0x8000) != 0 ? (ulong)(size & 0x7FFF) * 1024 : (ulong)size * 1024 * 1024;
        }

        var speed = BinaryPrimitives.ReadUInt16LittleEndian(s[SpeedOffset..]);
        var configured = s.Length >= ConfiguredSpeedOffset + 2 ? BinaryPrimitives.ReadUInt16LittleEndian(s[ConfiguredSpeedOffset..]) : (ushort)0;
        // The speed the module actually runs at is more useful than its rated maximum.
        var effective = configured is > 0 and < 0xFFFF ? configured : speed is > 0 and < 0xFFFF ? speed : (int?)null;

        return new MemoryModuleInfo(
            SizeBytes: bytes,
            Type: MemoryTypeName(s[MemoryTypeOffset]),
            SpeedMts: effective,
            Manufacturer: StringAt(strings, s.Length > ManufacturerOffset ? s[ManufacturerOffset] : (byte)0),
            PartNumber: StringAt(strings, s.Length > PartNumberOffset ? s[PartNumberOffset] : (byte)0));
    }

    /// <summary>Memory type codes from the SMBIOS specification (table "Memory Device — Type").</summary>
    private static string? MemoryTypeName(byte code) => code switch
    {
        0x12 => "DDR",
        0x13 => "DDR2",
        0x18 => "DDR3",
        0x1A => "DDR4",
        0x1B => "LPDDR",
        0x1C => "LPDDR2",
        0x1D => "LPDDR3",
        0x1E => "LPDDR4",
        0x22 => "DDR5",
        0x23 => "LPDDR5",
        _ => null,
    };

    /// <summary>Strings are numbered from 1; 0 means "no string".</summary>
    private static string? StringAt(ReadOnlySpan<byte> strings, byte index)
    {
        if (index == 0)
        {
            return null;
        }

        var current = 1;
        var start = 0;
        for (var i = 0; i < strings.Length; i++)
        {
            if (strings[i] != 0)
            {
                continue;
            }

            if (current == index)
            {
                var text = Encoding.ASCII.GetString(strings[start..i]).Trim();
                // Firmware often fills unused fields with placeholders.
                return text.Length == 0 || IsPlaceholder(text) ? null : text;
            }

            current++;
            start = i + 1;
        }

        return null;
    }

    private static bool IsPlaceholder(string text) =>
        text.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
        || text.Equals("Undefined", StringComparison.OrdinalIgnoreCase)
        || text.Equals("Not Specified", StringComparison.OrdinalIgnoreCase)
        || text.All(c => c == '0');

    /// <summary>Index just after the double zero that ends a structure's strings.</summary>
    private static int EndOfStrings(ReadOnlySpan<byte> table, int start)
    {
        var i = start;
        while (i + 1 < table.Length && !(table[i] == 0 && table[i + 1] == 0))
        {
            i++;
        }

        return Math.Min(i + 2, table.Length);
    }
}
