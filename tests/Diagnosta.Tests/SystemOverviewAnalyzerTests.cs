using Diagnosta.Checks;
using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

public class SystemOverviewAnalyzerTests
{
    private const ulong GB = FakeSystemInfoSource.GB;

    private static IReadOnlyList<Finding> Analyze(SystemOverview overview)
    {
        using var _ = new CultureScope("en");
        return SystemOverviewAnalyzer.Analyze(overview);
    }

    [Fact]
    public void HealthyPc_HasNoFindings()
    {
        Assert.Empty(Analyze(FakeSystemInfoSource.HealthyPc));
    }

    [Fact]
    public void SilentPc_DoesNotCrash_AndRaisesNoFalseAlarms()
    {
        Assert.Empty(Analyze(FakeSystemInfoSource.SilentPc));
    }

    [Fact]
    public void Windows10_IsWarnedAbout()
    {
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Windows = new WindowsInfo("Windows 10", "Home", "22H2", 19045, 5011),
        };

        var finding = Assert.Single(Analyze(pc));

        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("Windows 10", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(6.9, false)]
    [InlineData(7.0, true)]
    [InlineData(23.4, true)]
    public void LongUptime_IsWarnedAboutFromSevenDays(double days, bool expectWarning)
    {
        var pc = FakeSystemInfoSource.HealthyPc with { Uptime = TimeSpan.FromDays(days) };

        var warnings = Analyze(pc).Where(f => f.Severity == Severity.Warning).ToList();

        Assert.Equal(expectWarning, warnings.Count == 1);
        if (expectWarning)
        {
            Assert.Contains(((int)days).ToString(System.Globalization.CultureInfo.InvariantCulture), warnings[0].Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void FastStartup_IsExplainedOnlyWhenItCausesALongUptime(bool? fastStartup, bool expectExplanation)
    {
        var pc = FakeSystemInfoSource.HealthyPc with { Uptime = TimeSpan.FromDays(23), FastStartupEnabled = fastStartup };

        var explained = Analyze(pc).Any(f => f.Severity == Severity.Info && f.Message.Contains("Fast Startup", StringComparison.Ordinal));

        Assert.Equal(expectExplanation, explained);
    }

    [Fact]
    public void FastStartup_WithShortUptime_IsNotMentioned()
    {
        var pc = FakeSystemInfoSource.HealthyPc with { FastStartupEnabled = true };

        Assert.Empty(Analyze(pc));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(6.9, true)]
    [InlineData(7.8, false)] // an "8 GB" PC with memory reserved for built-in graphics
    [InlineData(16, false)]
    public void LowTotalMemory_IsPointedOut(double totalGb, bool expectInfo)
    {
        var total = (ulong)(totalGb * GB);
        var pc = FakeSystemInfoSource.HealthyPc with { Memory = new MemoryInfo(total, total / 2) };

        var info = Analyze(pc).Any(f => f.Severity == Severity.Info);

        Assert.Equal(expectInfo, info);
    }

    [Theory]
    [InlineData(0.89, false)]
    [InlineData(0.90, true)]
    [InlineData(0.99, true)]
    public void NearlyFullMemory_IsWarnedAbout(double usedFraction, bool expectWarning)
    {
        var total = 16 * GB;
        var available = (ulong)(total * (1 - usedFraction));
        var pc = FakeSystemInfoSource.HealthyPc with { Memory = new MemoryInfo(total, available) };

        var warned = Analyze(pc).Any(f => f.Severity == Severity.Warning);

        Assert.Equal(expectWarning, warned);
    }

    [Fact]
    public void MultipleProblems_AreAllReported()
    {
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Windows = new WindowsInfo("Windows 10", "Pro", "22H2", 19045, null),
            Uptime = TimeSpan.FromDays(30),
            FastStartupEnabled = true,
            Memory = new MemoryInfo(4 * GB, GB / 10),
        };

        var findings = Analyze(pc);

        // Windows 10, long uptime, Fast Startup explanation, low memory, memory nearly full
        Assert.Equal(5, findings.Count);
    }

    [Fact]
    public void Warnings_ComeBeforeInformation()
    {
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Uptime = TimeSpan.FromDays(30),
            FastStartupEnabled = true,
            Memory = new MemoryInfo(4 * GB, GB / 10),
        };

        var severities = Analyze(pc).Select(f => f.Severity).ToList();

        Assert.Equal(severities.OrderByDescending(s => s), severities);
        Assert.Equal(Severity.Warning, severities[0]);
        Assert.Equal(Severity.Info, severities[^1]);
    }

    [Theory]
    [InlineData(0.89, false)]
    [InlineData(0.90, true)]
    public void DataDrive_IsWarnedAboutFromNinetyPercent(double usedFraction, bool expectWarning)
    {
        var total = 2000 * GB;
        var drive = new VolumeInfo("D:", null, total, (ulong)(total * (1 - usedFraction)), false, DriveKind.Hdd, null, null);
        var pc = FakeSystemInfoSource.HealthyPc with { Drives = [drive] };

        var warning = Analyze(pc).SingleOrDefault();

        Assert.Equal(expectWarning, warning is not null);
        if (warning is not null)
        {
            Assert.Contains("D:", warning.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SmallSystemDrive_IsWarnedAboutBelowTenGigabytesEvenIfNotNinetyPercentFull()
    {
        // 64 GB drive with 8 GB free is only 87.5 % full, but too tight for Windows updates.
        var drive = new VolumeInfo("C:", null, 64 * GB, 8 * GB, true, DriveKind.Ssd, null, null);
        var pc = FakeSystemInfoSource.HealthyPc with { Drives = [drive] };

        var warning = Assert.Single(Analyze(pc));

        Assert.Contains("C: (Windows)", warning.Message, StringComparison.Ordinal);
        Assert.Contains("8 GB", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyOrUnknownDriveSize_DoesNotWarn()
    {
        var drive = new VolumeInfo("E:", null, 0, 0, false, DriveKind.Unknown, null, null);
        var pc = FakeSystemInfoSource.HealthyPc with { Drives = [drive] };

        Assert.Empty(Analyze(pc));
    }
}
