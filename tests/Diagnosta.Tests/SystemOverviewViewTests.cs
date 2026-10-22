using Diagnosta.Checks;
using Diagnosta.SystemInfo;
using Diagnosta.Ui;
using Spectre.Console;

namespace Diagnosta.Tests;

public class SystemOverviewViewTests
{
    private static string Render(SystemOverview overview, string culture = "en")
    {
        using var _ = new CultureScope(culture);
        var writer = new StringWriter();
        var console = TestConsole.Create(writer, width: 140);

        SystemOverviewView.Render(console, overview, SystemOverviewAnalyzer.Analyze(overview));
        return writer.ToString();
    }

    [Fact]
    public void HealthyPc_ShowsAllValues()
    {
        var output = Render(FakeSystemInfoSource.HealthyPc);

        Assert.Contains("Windows 11 Pro", output, StringComparison.Ordinal);
        Assert.Contains("Version 24H2 · Build 26100.2314", output, StringComparison.Ordinal);
        Assert.Contains("AMD Ryzen 5 7600X 6-Core Processor", output, StringComparison.Ordinal);
        Assert.Contains("6 cores · 12 threads · base clock 4.7 GHz", output, StringComparison.Ordinal);
        Assert.Contains("12 % in use right now", output, StringComparison.Ordinal);
        Assert.Contains("12 GB of 32 GB in use (38 %)", output, StringComparison.Ordinal);
        Assert.Contains("NVIDIA GeForce RTX 4070", output, StringComparison.Ordinal);
        Assert.Contains("Dedicated graphics card with 12 GB of its own memory", output, StringComparison.Ordinal);
        Assert.Contains("Driver 32.0.15.6094 from 8/20/2026", output, StringComparison.Ordinal);
        Assert.Contains("2 modules: 16 GB + 16 GB · DDR5 · 6000 MT/s", output, StringComparison.Ordinal);
        Assert.Contains("Slots: 2 of 4 in use", output, StringComparison.Ordinal);
        Assert.Contains("5 h 30 min", output, StringComparison.Ordinal);
        Assert.Contains("All good", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Findings", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Drives_ShowTypeModelAndFreeSpace()
    {
        var output = Render(FakeSystemInfoSource.HealthyPc);

        Assert.Contains("Windows · NVMe SSD · Samsung SSD 980 PRO 1TB", output, StringComparison.Ordinal);
        Assert.Contains("412 GB free of 931 GB (56 % used)", output, StringComparison.Ordinal);
        Assert.Contains("Games · SATA HDD · WDC WD20EZRZ-00Z5HB0", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DiskHealth_IsShownPerPhysicalDisk()
    {
        var output = Render(FakeSystemInfoSource.HealthyPc, "de");

        Assert.True(output.Contains("Samsung SSD 980 PRO 1TB  ✓ Gesund", StringComparison.Ordinal), output);
        Assert.Contains("WDC WD20EZRZ-00Z5HB0  ✓ Gesund", output, StringComparison.Ordinal);
    }

    [Fact]
    public void FailingDisk_IsMarkedAndTurnsTheStatusCritical()
    {
        var pc = FakeSystemInfoSource.HealthyPc with { PhysicalDisks = [new PhysicalDiskInfo("Old HDD", DriveKind.Hdd, DiskHealth.Unhealthy)] };

        var output = Render(pc);

        Assert.True(output.Contains("Old HDD  ✗ Failing", StringComparison.Ordinal), output);
        Assert.Contains("✗  1 finding", output, StringComparison.Ordinal);
    }

    [Fact]
    public void HealthIsShownOnTheDriveLine_WhenTheDiskIsKnown()
    {
        var output = RenderArea(FakeSystemInfoSource.HealthyPc, CheckArea.Drives);
        var cLine = output.Split('\n').Single(l => l.Contains("C:", StringComparison.Ordinal));

        Assert.Contains("Samsung SSD 980 PRO 1TB  ✓ Healthy", cLine, StringComparison.Ordinal);
        Assert.DoesNotContain("│ Health ", output, StringComparison.Ordinal); // no separate health line needed
    }

    [Fact]
    public void UnknownDiskHealth_ShowsNoBadge()
    {
        var output = Render(FakeSystemInfoSource.HealthyPc with { PhysicalDisks = null });

        Assert.Contains("Samsung SSD 980 PRO 1TB", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Healthy", output, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheChosenArea_IsShown_WithItsOwnFindings()
    {
        // 30 days uptime is a Windows finding; it must not appear on the drives screen.
        var pc = FakeSystemInfoSource.HealthyPc with { Uptime = TimeSpan.FromDays(30) };
        using var _ = new CultureScope("en");
        var writer = new StringWriter();
        var console = TestConsole.Create(writer, width: 140);

        SystemOverviewView.Render(console, pc, SystemOverviewAnalyzer.Analyze(pc), [CheckArea.Drives]);
        var output = writer.ToString();

        Assert.Contains("Drives", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Processor", output, StringComparison.Ordinal);
        Assert.DoesNotContain("restarted", output, StringComparison.Ordinal);
        Assert.Contains("All good", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DriveWithoutDeviceDetails_StillShowsSpace()
    {
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Drives = [new VolumeInfo("E:", null, 100 * FakeSystemInfoSource.GB, 50 * FakeSystemInfoSource.GB, false, DriveKind.Unknown, null, null)],
        };

        var output = Render(pc);

        Assert.Contains("50 GB free of 100 GB (50 % used)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegratedGraphics_IsExplainedInsteadOfShowingTinyMemory()
    {
        var pc = FakeSystemInfoSource.HealthyPc with { Gpus = [new GpuInfo("Intel(R) UHD Graphics", 128UL * 1024 * 1024)] };

        var output = RenderArea(pc, CheckArea.Graphics, "de");

        Assert.Contains("Intel(R) UHD Graphics", output, StringComparison.Ordinal);
        Assert.Contains("Integrierte Grafik, nutzt den Arbeitsspeicher mit", output, StringComparison.Ordinal);
        Assert.DoesNotContain("128 MB", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "1 finding")]
    [InlineData(3, "3 findings")]
    public void Status_CountsFindings(int count, string expected)
    {
        var findings = Enumerable.Range(0, count).Select(i => new Finding(Severity.Info, CheckArea.Windows, $"Tip {i}")).ToList();
        using var _ = new CultureScope("en");
        var writer = new StringWriter();
        var console = TestConsole.Create(writer);

        SystemOverviewView.Render(console, FakeSystemInfoSource.HealthyPc, findings);

        Assert.Contains(expected, writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("Tip 0", writer.ToString(), StringComparison.Ordinal);
    }

    private static string RenderArea(SystemOverview pc, CheckArea area, string culture = "en")
    {
        using var _ = new CultureScope(culture);
        var writer = new StringWriter();
        SystemOverviewView.Render(TestConsole.Create(writer, 140), pc, SystemOverviewAnalyzer.Analyze(pc), [area]);
        return writer.ToString();
    }

    [Fact]
    public void StartupApps_ShowCountAndMarkTurnedOffOnes()
    {
        var output = RenderArea(FakeSystemInfoSource.HealthyPc, CheckArea.Startup);

        Assert.Contains("2 programs start with Windows · 1 turned off", output, StringComparison.Ordinal);
        Assert.Contains("OneDrive.exe · only for you", output, StringComparison.Ordinal);
        Assert.Contains("SecurityHealthSystray.exe · for all users", output, StringComparison.Ordinal);
        Assert.Contains("Spotify.exe · only for you · turned off", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RunningPrograms_ShowTopMemoryAndCpu()
    {
        var output = RenderArea(FakeSystemInfoSource.HealthyPc, CheckArea.Processes, "de");

        Assert.Contains("Am meisten Arbeitsspeicher", output, StringComparison.Ordinal);
        Assert.Contains("chrome (14×)", output, StringComparison.Ordinal);
        Assert.Contains("3 GB", output, StringComparison.Ordinal);
        Assert.Contains("Am meisten Prozessor", output, StringComparison.Ordinal);
        Assert.Contains("5 %", output, StringComparison.Ordinal);
        Assert.True(output.IndexOf("chrome", StringComparison.Ordinal) < output.IndexOf("Discord", StringComparison.Ordinal), "sorted by usage");
    }

    [Fact]
    public void PendingRestart_IsShownInTheWindowsArea()
    {
        var output = RenderArea(FakeSystemInfoSource.HealthyPc with { RestartPending = true }, CheckArea.Windows);

        Assert.Contains("Waiting for a restart to finish installing", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RecognisedGraphics_DoesNotAlsoSayUnknown()
    {
        var output = Render(FakeSystemInfoSource.HealthyPc);

        Assert.DoesNotContain("unknown", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SilentPc_ShowsUnknownInsteadOfCrashing()
    {
        var output = Render(FakeSystemInfoSource.SilentPc, "de");

        Assert.Contains("4 Threads", output, StringComparison.Ordinal);
        Assert.True(output.Split("unbekannt").Length - 1 >= 5, "CPU, Windows, RAM, GPU and drives should all say 'unbekannt'");
    }

    [Fact]
    public void NoGpu_IsSaidPlainly()
    {
        var output = Render(FakeSystemInfoSource.HealthyPc with { Gpus = [] });

        Assert.Contains("No graphics card detected", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SeveralGpus_AreAllListed()
    {
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Gpus = [new GpuInfo("Intel(R) UHD Graphics", 128UL * 1024 * 1024), new GpuInfo("NVIDIA GeForce RTX 3050 Laptop GPU", 4 * FakeSystemInfoSource.GB)],
        };

        var output = Render(pc);

        Assert.Contains("Integrated graphics, shares system memory", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Driver", output.Split("RTX 3050")[0].Split("UHD Graphics")[1], StringComparison.Ordinal);
        Assert.Contains("RTX 3050 Laptop GPU", output, StringComparison.Ordinal);
        Assert.Contains("Dedicated graphics card with 4 GB of its own memory", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ValuesFromWindows_AreNotInterpretedAsMarkup()
    {
        // A GPU or CPU name is text from the system; brackets must be shown literally.
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Cpu = new CpuInfo("Weird [red]CPU[/]", 4, 8),
            Gpus = [new GpuInfo("GPU [/]", null)],
            Drives = [new VolumeInfo("C:", "[bold]Label", 100, 50, true, DriveKind.Ssd, null, "Model [red]")],
        };

        var output = Render(pc);

        Assert.Contains("Weird [red]CPU[/]", output, StringComparison.Ordinal);
        Assert.Contains("GPU [/]", output, StringComparison.Ordinal);
        Assert.Contains("Model [red]", output, StringComparison.Ordinal);
    }
}
