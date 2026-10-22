using Diagnosta.Ui;

namespace Diagnosta.Tests;

public class FormattingTests
{
    private const ulong GB = FakeSystemInfoSource.GB;

    [Theory]
    [InlineData("en", 16 * GB, "16 GB")]
    [InlineData("de", 16 * GB, "16 GB")]
    [InlineData("en", 15 * GB + GB / 2, "15.5 GB")]
    [InlineData("de", 15 * GB + GB / 2, "15,5 GB")]
    [InlineData("en", 512UL * 1024 * 1024, "512 MB")]
    public void Bytes_UsesWindowsStyleUnitsAndLocalNumbers(string culture, ulong bytes, string expected)
    {
        using var _ = new CultureScope(culture);

        Assert.Equal(expected, Formatting.Bytes(bytes));
    }

    [Theory]
    [InlineData("en", 1608, "1.6 GHz")]
    [InlineData("de", 1608, "1,6 GHz")]
    [InlineData("en", 4700, "4.7 GHz")]
    [InlineData("en", 3792, "3.8 GHz")]
    public void Gigahertz_UsesOneDecimal(string culture, int mhz, string expected)
    {
        using var _ = new CultureScope(culture);

        Assert.Equal(expected, Formatting.Gigahertz(mhz));
    }

    [Theory]
    [InlineData("en", 0, 5, 30, "5 h 30 min")]
    [InlineData("de", 0, 5, 30, "5 Std. 30 Min.")]
    [InlineData("en", 1, 3, 0, "1 day, 3 h")]
    [InlineData("de", 1, 3, 0, "1 Tag, 3 Std.")]
    [InlineData("en", 23, 4, 59, "23 days, 4 h")]
    [InlineData("de", 23, 4, 59, "23 Tage, 4 Std.")]
    public void Uptime_IsReadable(string culture, int days, int hours, int minutes, string expected)
    {
        using var _ = new CultureScope(culture);

        Assert.Equal(expected, Formatting.Uptime(new TimeSpan(days, hours, minutes, 0)));
    }

    [Fact]
    public void Uptime_NeverShowsNegativeValues()
    {
        using var _ = new CultureScope("en");

        Assert.Equal("0 h 0 min", Formatting.Uptime(TimeSpan.FromMinutes(-5)));
    }
}
