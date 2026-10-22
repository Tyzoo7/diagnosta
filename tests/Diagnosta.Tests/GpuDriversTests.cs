using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

public class GpuDriversTests
{
    [Theory]
    [InlineData("5-12-2023", 2023, 5, 12)]
    [InlineData("10-1-2026", 2026, 10, 1)]
    [InlineData("08-20-2026", 2026, 8, 20)]
    public void DriverDate_IsReadInRegistryFormat(string value, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), GpuDrivers.ParseDriverDate(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2023-05-12")]
    [InlineData("not a date")]
    public void UnexpectedDriverDate_IsNull(string? value)
    {
        Assert.Null(GpuDrivers.ParseDriverDate(value));
    }
}
