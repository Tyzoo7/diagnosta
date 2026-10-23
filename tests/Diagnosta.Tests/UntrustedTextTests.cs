using Diagnosta.Checks;
using Diagnosta.Report;
using Diagnosta.SystemInfo;
using Diagnosta.Ui;

namespace Diagnosta.Tests;

/// <summary>
/// Names of startup apps and programs are chosen by other software. They must never be able
/// to steer the terminal or disguise themselves in the support report.
/// </summary>
public class UntrustedTextTests
{
    private const string Esc = "\u001b";

    // A name that tries to clear the screen and hide in a fake "all good" line.
    private static readonly string Hostile = $"Evil{Esc}[2J{Esc}]0;title\u0007‮exe.txt";

    private static readonly SystemOverview Pc = FakeSystemInfoSource.HealthyPc with
    {
        StartupApps = [new StartupEntry(Hostile, Hostile + ".exe", StartupScope.CurrentUser, Enabled: true)],
        Processes = [new ProcessUsage(Hostile, 1, 1UL << 30, 50)],
    };

    [Theory]
    [InlineData("chrome", "chrome")]
    [InlineData("Grüezi [ok] 😀", "Grüezi [ok] 😀")]
    [InlineData("a\u001b[31mb", "a[31mb")]
    [InlineData("line\r\nbreak\ttab", "linebreaktab")]
    [InlineData("‮gnp.exe", "gnp.exe")]
    [InlineData("x⁦y⁩z", "xyz")]
    public void Printable_KeepsTextButDropsControlCharacters(string input, string expected) =>
        Assert.Equal(expected, Describe.Printable(input));

    [Theory]
    [InlineData("Startup")]
    [InlineData("Processes")]
    public void TerminalView_NeverWritesControlCharactersFromNames(string area)
    {
        var writer = new StringWriter();
        SystemOverviewView.Render(TestConsole.Create(writer, 140), Pc, SystemOverviewAnalyzer.Analyze(Pc), [Enum.Parse<CheckArea>(area)]);
        var output = writer.ToString();

        Assert.Contains("Evil", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Esc, output, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0007", output, StringComparison.Ordinal);
        Assert.DoesNotContain("‮", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SupportReport_NeverContainsControlCharactersFromNames()
    {
        var html = SupportReport.Build(Pc, SystemOverviewAnalyzer.Analyze(Pc), new ReportOptions(Anonymize: true, DateTimeOffset.UnixEpoch, "1.0.0"));

        Assert.Contains("Evil", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Esc, html, StringComparison.Ordinal);
        Assert.DoesNotContain("‮", html, StringComparison.Ordinal);
    }
}
