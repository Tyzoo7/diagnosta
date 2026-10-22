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
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = 140;

        SystemOverviewView.Render(console, overview, SystemOverviewAnalyzer.Analyze(overview));
        return writer.ToString();
    }

    [Fact]
    public void HealthyPc_ShowsAllValues()
    {
        var output = Render(FakeSystemInfoSource.HealthyPc);

        Assert.Contains("Windows 11 Pro 24H2 (Build 26100.2314)", output, StringComparison.Ordinal);
        Assert.Contains("AMD Ryzen 5 7600X 6-Core Processor (6 cores, 12 threads)", output, StringComparison.Ordinal);
        Assert.Contains("12 GB of 32 GB in use (38 %)", output, StringComparison.Ordinal);
        Assert.Contains("NVIDIA GeForce RTX 4070 (12 GB dedicated memory)", output, StringComparison.Ordinal);
        Assert.Contains("5 h 30 min", output, StringComparison.Ordinal);
        Assert.Contains("Nothing unusual found.", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SilentPc_ShowsUnknownInsteadOfCrashing()
    {
        var output = Render(FakeSystemInfoSource.SilentPc, "de");

        Assert.Contains("unbekannt (4 Threads)", output, StringComparison.Ordinal);
        Assert.True(output.Split("unbekannt").Length - 1 >= 4, "CPU, Windows, RAM and GPU should all say 'unbekannt'");
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
            Gpus = [new GpuInfo("Intel(R) UHD Graphics", null), new GpuInfo("NVIDIA GeForce RTX 3050 Laptop GPU", 4 * FakeSystemInfoSource.GB)],
        };

        var output = Render(pc);

        Assert.Contains("Intel(R) UHD Graphics", output, StringComparison.Ordinal);
        Assert.Contains("RTX 3050 Laptop GPU (4 GB dedicated memory)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ValuesFromWindows_AreNotInterpretedAsMarkup()
    {
        // A GPU or CPU name is text from the system; brackets must be shown literally.
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Cpu = new CpuInfo("Weird [red]CPU[/]", 4, 8),
            Gpus = [new GpuInfo("GPU [/]", null)],
        };

        var output = Render(pc);

        Assert.Contains("Weird [red]CPU[/]", output, StringComparison.Ordinal);
        Assert.Contains("GPU [/]", output, StringComparison.Ordinal);
    }
}
