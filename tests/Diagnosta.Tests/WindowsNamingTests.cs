using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

public class WindowsNamingTests
{
    [Theory]
    [InlineData(19045, "Windows 10")] // 22H2, the last Windows 10 version
    [InlineData(21999, "Windows 10")]
    [InlineData(22000, "Windows 11")] // first Windows 11 build
    [InlineData(26100, "Windows 11")] // 24H2
    public void NameFor_UsesTheBuildNumber(int build, string expected)
    {
        Assert.Equal(expected, WindowsNaming.NameFor(build));
    }

    [Theory]
    [InlineData("Windows 10 Pro", "Pro")]
    [InlineData("Windows 10 Home", "Home")]
    [InlineData("Windows 11 Enterprise", "Enterprise")]
    [InlineData("Windows 10 Pro for Workstations", "Pro for Workstations")]
    [InlineData("Windows 10", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void EditionFrom_StripsTheMisleadingProductName(string? productName, string? expected)
    {
        // On Windows 11 the registry still says "Windows 10 Pro"; we only keep "Pro".
        Assert.Equal(expected, WindowsNaming.EditionFrom(productName));
    }
}
