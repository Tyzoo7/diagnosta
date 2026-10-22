using Diagnosta.Checks;
using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

public class SystemOverviewAnalyzerTests
{
    private const ulong GB = FakeSystemInfoSource.GB;

    private static readonly DateOnly Today = new(2026, 10, 22);

    private static IReadOnlyList<Finding> Analyze(SystemOverview overview)
    {
        using var _ = new CultureScope("en");
        return SystemOverviewAnalyzer.Analyze(overview, Today);
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
        // No module data: this test is about total memory only, not about the upgrade tip.
        var pc = FakeSystemInfoSource.HealthyPc with { Memory = new MemoryInfo(total, total / 2), MemoryModules = null };

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
            MemoryModules = null,
            Processes = null,
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

    [Theory]
    [InlineData("Healthy", null)]
    [InlineData("Unknown", null)]
    [InlineData("Warning", "Warning")]
    [InlineData("Unhealthy", "Critical")]
    public void DiskHealth_IsReportedWithMatchingSeverity(string health, string? expected)
    {
        var disk = new PhysicalDiskInfo("Crucial MX500", DriveKind.Ssd, Enum.Parse<DiskHealth>(health));
        var pc = FakeSystemInfoSource.HealthyPc with { PhysicalDisks = [disk] };

        var finding = Analyze(pc).SingleOrDefault();

        Assert.Equal(expected, finding?.Severity.ToString());
        if (finding is not null)
        {
            Assert.Contains("Crucial MX500", finding.Message, StringComparison.Ordinal);
            Assert.Contains("Back up", finding.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FailingDisk_ComesFirst_EvenBeforeOtherWarnings()
    {
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Uptime = TimeSpan.FromDays(30),
            PhysicalDisks = [new PhysicalDiskInfo(null, DriveKind.Hdd, DiskHealth.Unhealthy)],
        };

        var findings = Analyze(pc);

        Assert.Equal(Severity.Critical, findings[0].Severity);
        Assert.Contains("Unnamed disk", findings[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(89.9, false)]
    [InlineData(90, true)]
    public void BusyProcessor_IsWarnedAbout(double usage, bool expectWarning)
    {
        var pc = FakeSystemInfoSource.HealthyPc with { Cpu = FakeSystemInfoSource.HealthyPc.Cpu with { UsagePercent = usage } };

        var finding = Analyze(pc).SingleOrDefault(f => f.Area == CheckArea.Processor);

        Assert.Equal(expectWarning, finding is { Severity: Severity.Warning });
    }

    [Theory]
    [InlineData(8, 2, 1, true)]    // 8 GB, one of two slots free
    [InlineData(8, 2, 2, false)]   // all slots full
    [InlineData(32, 4, 2, false)]  // plenty of RAM already
    public void RamUpgrade_IsSuggestedOnlyWhenUsefulAndPossible(int totalGb, int slots, int modules, bool expectTip)
    {
        var total = (ulong)totalGb * GB;
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Memory = new MemoryInfo(total, total / 2),
            MemoryModules = new MemoryLayout([.. Enumerable.Repeat(new MemoryModuleInfo(total / (ulong)modules, "DDR4", 3200, null, null), modules)], slots),
        };

        var tip = Analyze(pc).Any(f => f.Area == CheckArea.Memory && f.Message.Contains("slot", StringComparison.Ordinal));

        Assert.Equal(expectTip, tip);
    }

    [Theory]
    [InlineData(2024, 10, 23, false)] // just under two years old
    [InlineData(2024, 10, 21, true)]  // just over
    public void OldGraphicsDriver_IsPointedOut(int year, int month, int day, bool expectTip)
    {
        var gpu = new GpuInfo("Intel(R) UHD Graphics", 128 * 1024 * 1024, null, "27.20.100.8681", new DateOnly(year, month, day));
        var pc = FakeSystemInfoSource.HealthyPc with { Gpus = [gpu] };

        var tip = Analyze(pc).SingleOrDefault(f => f.Area == CheckArea.Graphics);

        Assert.Equal(expectTip, tip is not null);
        if (tip is not null)
        {
            Assert.Contains("Intel(R) UHD Graphics", tip.Message, StringComparison.Ordinal);
            Assert.Contains(year.ToString(System.Globalization.CultureInfo.InvariantCulture), tip.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PendingRestart_IsWarnedAbout()
    {
        var finding = Assert.Single(Analyze(FakeSystemInfoSource.HealthyPc with { RestartPending = true }));

        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(CheckArea.Windows, finding.Area);
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public void ManyStartupApps_ArePointedOut_CountingOnlyEnabledOnes(int enabled, bool expectTip)
    {
        var apps = Enumerable.Range(0, enabled).Select(i => new StartupEntry($"App{i}", null, StartupScope.CurrentUser, true))
            .Append(new StartupEntry("Off", null, StartupScope.CurrentUser, false))
            .ToList();

        var tip = Analyze(FakeSystemInfoSource.HealthyPc with { StartupApps = apps }).SingleOrDefault();

        Assert.Equal(expectTip, tip is { Area: CheckArea.Startup });
    }

    [Fact]
    public void FullMemory_NamesTheProgramUsingTheMost()
    {
        var pc = FakeSystemInfoSource.HealthyPc with { Memory = new MemoryInfo(32 * GB, GB) };

        var findings = Analyze(pc);

        var culprit = Assert.Single(findings, f => f.Area == CheckArea.Processes);
        Assert.Contains("chrome (3 GB)", culprit.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BusyProcessor_NamesTheProgramUsingTheMost()
    {
        var pc = FakeSystemInfoSource.HealthyPc with { Cpu = FakeSystemInfoSource.HealthyPc.Cpu with { UsagePercent = 97 } };

        var culprit = Assert.Single(Analyze(pc), f => f.Area == CheckArea.Processes);

        Assert.Contains("chrome", culprit.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(45_000u, null)]       // 90 %: fine
    [InlineData(35_000u, "Info")]     // 70 %: worn
    [InlineData(25_000u, "Warning")]  // 50 %: replace
    public void BatteryWear_IsReported(uint full, string? expected)
    {
        var pc = FakeSystemInfoSource.HealthyPc with { Battery = new BatteryInfo(true, DesignedCapacity: 50_000, FullChargeCapacity: full) };

        var finding = Analyze(pc).SingleOrDefault(f => f.Area == CheckArea.Battery);

        Assert.Equal(expected, finding?.Severity.ToString());
    }

    [Fact]
    public void NoNetwork_IsWarnedAbout()
    {
        var finding = Assert.Single(Analyze(FakeSystemInfoSource.HealthyPc with { Network = [] }));

        Assert.Equal(CheckArea.Network, finding.Area);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public void MissingRouterAddress_IsWarnedAbout()
    {
        var adapter = new NetworkAdapterInfo("Realtek PCIe GbE", NetworkKind.Ethernet, 1_000_000_000, ["169.254.3.4"], null, []);

        var finding = Assert.Single(Analyze(FakeSystemInfoSource.HealthyPc with { Network = [adapter] }));

        Assert.Contains("Realtek PCIe GbE", finding.Message, StringComparison.Ordinal);
        Assert.Contains("169.254", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ethernet", 100_000_000L, true)]
    [InlineData("Ethernet", 1_000_000_000L, false)]
    [InlineData("WiFi", 54_000_000L, false)] // Wi-Fi speeds vary; no cable to blame
    public void SlowCable_IsPointedOut(string kind, long speed, bool expectTip)
    {
        var adapter = new NetworkAdapterInfo("Adapter", Enum.Parse<NetworkKind>(kind), speed, ["192.168.0.5"], "192.168.0.1", []);

        var tip = Analyze(FakeSystemInfoSource.HealthyPc with { Network = [adapter] }).SingleOrDefault();

        Assert.Equal(expectTip, tip is not null);
    }

    [Fact]
    public void EmptyOrUnknownDriveSize_DoesNotWarn()
    {
        var drive = new VolumeInfo("E:", null, 0, 0, false, DriveKind.Unknown, null, null);
        var pc = FakeSystemInfoSource.HealthyPc with { Drives = [drive] };

        Assert.Empty(Analyze(pc));
    }
}
