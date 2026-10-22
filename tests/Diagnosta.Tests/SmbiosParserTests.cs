using System.Buffers.Binary;
using System.Text;
using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

/// <summary>Builds SMBIOS tables byte by byte, the way firmware lays them out.</summary>
public class SmbiosParserTests
{
    private const ulong MB = 1024 * 1024;
    private const ulong GB = 1024 * MB;

    /// <summary>A type 17 "Memory Device" structure (SMBIOS 2.8 layout, 0x28 bytes).</summary>
    private static byte[] MemoryDevice(ushort size, byte type, ushort speed, ushort configuredSpeed, uint extendedSize = 0, params string[] strings)
    {
        var s = new byte[0x28];
        s[0] = 17;
        s[1] = 0x28;
        BinaryPrimitives.WriteUInt16LittleEndian(s.AsSpan(0x0C), size);
        s[0x12] = type;
        BinaryPrimitives.WriteUInt16LittleEndian(s.AsSpan(0x15), speed);
        s[0x17] = 1; // manufacturer = string 1
        s[0x18] = 2; // serial number = string 2 (must never be read)
        s[0x1A] = 3; // part number = string 3
        BinaryPrimitives.WriteUInt32LittleEndian(s.AsSpan(0x1C), extendedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(s.AsSpan(0x20), configuredSpeed);
        return [.. s, .. StringSet(strings)];
    }

    private static byte[] Other(byte type) => [type, 4, 0, 0, 0, 0];

    private static byte[] StringSet(string[] strings) =>
        strings.Length == 0 ? [0, 0] : [.. strings.SelectMany(t => Encoding.ASCII.GetBytes(t).Append((byte)0)), 0];

    private static byte[] Table(params byte[][] structures) => [.. structures.SelectMany(s => s), .. Other(127)];

    [Fact]
    public void TwoModulesAndAnEmptySlot_AreReadCorrectly()
    {
        var table = Table(
            Other(0), // BIOS information, to be skipped
            MemoryDevice(8192, 0x1A, 3200, 2667, 0, "Samsung", "SERIAL-1234", "M471A1K43DB1-CWE"),
            MemoryDevice(16384, 0x1A, 3200, 2667, 0, "SK Hynix", "SERIAL-5678", "HMA82GS6CJR8N-XN"),
            MemoryDevice(0, 0x02, 0, 0)); // empty slot

        var layout = SmbiosParser.ParseMemory(table);

        Assert.NotNull(layout);
        Assert.Equal(3, layout.SlotCount);
        Assert.Equal(1, layout.FreeSlots);
        Assert.Equal(
            [new MemoryModuleInfo(8 * GB, "DDR4", 2667, "Samsung", "M471A1K43DB1-CWE"), new MemoryModuleInfo(16 * GB, "DDR4", 2667, "SK Hynix", "HMA82GS6CJR8N-XN")],
            layout.Modules);
    }

    [Fact]
    public void SerialNumbers_AreNeverRead()
    {
        var layout = SmbiosParser.ParseMemory(Table(MemoryDevice(8192, 0x22, 4800, 0, 0, "Crucial", "SECRET-SERIAL", "CT8G48C40S5")));

        Assert.DoesNotContain(layout!.Modules, m => m.ToString().Contains("SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public void ConfiguredSpeedMissing_FallsBackToRatedSpeed()
    {
        var module = SmbiosParser.ParseMemory(Table(MemoryDevice(8192, 0x22, 4800, 0)))!.Modules.Single();

        Assert.Equal(4800, module.SpeedMts);
        Assert.Equal("DDR5", module.Type);
    }

    [Fact]
    public void LargeModules_UseTheExtendedSizeField()
    {
        var module = SmbiosParser.ParseMemory(Table(MemoryDevice(0x7FFF, 0x22, 5600, 5600, extendedSize: 65536)))!.Modules.Single();

        Assert.Equal(64 * GB, module.SizeBytes);
    }

    [Fact]
    public void SizeInKilobytes_IsHonoured()
    {
        var module = SmbiosParser.ParseMemory(Table(MemoryDevice(0x8000 | 512, 0x1A, 0, 0)))!.Modules.Single();

        Assert.Equal(512UL * 1024, module.SizeBytes);
        Assert.Null(module.SpeedMts);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Not Specified")]
    [InlineData("0000")]
    public void PlaceholderStrings_BecomeNull(string placeholder)
    {
        var module = SmbiosParser.ParseMemory(Table(MemoryDevice(8192, 0x1A, 3200, 3200, 0, placeholder, "x", placeholder)))!.Modules.Single();

        Assert.Null(module.Manufacturer);
        Assert.Null(module.PartNumber);
    }

    [Fact]
    public void UnknownMemoryType_IsNull()
    {
        var module = SmbiosParser.ParseMemory(Table(MemoryDevice(8192, 0x02, 3200, 3200)))!.Modules.Single();

        Assert.Null(module.Type);
    }

    [Fact]
    public void TableWithoutMemorySlots_ReturnsNull()
    {
        Assert.Null(SmbiosParser.ParseMemory(Table(Other(0), Other(1))));
    }

    [Fact]
    public void MalformedTable_DoesNotThrow()
    {
        // A structure claiming to be longer than the table, then random garbage.
        byte[] table = [17, 200, 0, 0, 1, 2, 3];

        Assert.Null(SmbiosParser.ParseMemory(table));
        Assert.Null(SmbiosParser.ParseMemory([]));
    }
}
