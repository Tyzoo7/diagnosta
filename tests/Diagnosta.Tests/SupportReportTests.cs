using Diagnosta.Checks;
using Diagnosta.Report;
using Diagnosta.SystemInfo;
using Diagnosta.Ui;

namespace Diagnosta.Tests;

public class SupportReportTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 23, 15, 30, 0, TimeSpan.FromHours(2));

    private static string Build(SystemOverview pc, bool anonymize = true, string culture = "en")
    {
        using var _ = new CultureScope(culture);
        return SupportReport.Build(pc, SystemOverviewAnalyzer.Analyze(pc, new DateOnly(2026, 10, 23)), new ReportOptions(anonymize, Created, "0.7.0 (abc1234)"));
    }

    [Fact]
    public void Report_IsAStandaloneHtmlPageThatCanRunNothing()
    {
        var html = Build(FakeSystemInfoSource.HealthyPc);

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValuesFromThePc_AreHtmlEncoded()
    {
        var pc = FakeSystemInfoSource.HealthyPc with
        {
            Processes = [new ProcessUsage("<script>alert(1)</script>", 1, FakeSystemInfoSource.GB, 2)],
            StartupApps = [new StartupEntry("<img src=x onerror=alert(1)>", "evil.exe", StartupScope.CurrentUser, true)],
        };

        var html = Build(pc);

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Anonymised_ShortensOwnIpAndHidesDriveNames()
    {
        var html = Build(FakeSystemInfoSource.HealthyPc, anonymize: true);

        Assert.Contains("IP 192.168.1.xxx", html, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.1.23", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Games", html, StringComparison.Ordinal); // D: is labelled "Games"
        Assert.Contains("Personal details hidden", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NotAnonymised_KeepsEverythingAndSaysSo()
    {
        var html = Build(FakeSystemInfoSource.HealthyPc, anonymize: false);

        Assert.Contains("192.168.1.23", html, StringComparison.Ordinal);
        Assert.Contains("Games", html, StringComparison.Ordinal);
        Assert.Contains("Not anonymised", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Findings_ComeBeforeTheDetails()
    {
        var html = Build(FakeSystemInfoSource.HealthyPc with { RestartPending = true });

        Assert.Contains("class=\"status warning\"", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("Windows is waiting for a restart", StringComparison.Ordinal) < html.IndexOf("<h2>System</h2>", StringComparison.Ordinal),
            "a helper should see the problems first");
    }

    [Fact]
    public void Report_ContainsEverySectionOfTheFullCheck()
    {
        var html = Build(FakeSystemInfoSource.HealthyPc, culture: "de");

        foreach (var heading in new[] { "System", "Laufwerke", "Akku", "Netzwerk", "Autostart-Programme", "Laufende Programme" })
        {
            Assert.Contains($"<h2>{heading}", html, StringComparison.Ordinal);
        }

        Assert.Contains("<html lang=\"de\">", html, StringComparison.Ordinal);
        Assert.Contains("Erstellt am", html, StringComparison.Ordinal);
        Assert.Contains("0.7.0 (abc1234)", html, StringComparison.Ordinal);
        Assert.Contains("Samsung SSD 980 PRO 1TB", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopPc_HasNoBatterySection()
    {
        var html = Build(FakeSystemInfoSource.HealthyPc with { Battery = BatteryInfo.None });

        Assert.DoesNotContain("<h2>Battery", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SilentPc_StillProducesAReport()
    {
        var html = Build(FakeSystemInfoSource.SilentPc);

        Assert.Contains("unknown", html, StringComparison.Ordinal);
        Assert.EndsWith("</html>\n", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("192.168.1.23", "192.168.1.xxx")]
    [InlineData("10.0.0.5", "10.0.0.xxx")]
    [InlineData("nonsense", "xxx")]
    public void MaskAddress_HidesTheDevicePart(string address, string expected)
    {
        Assert.Equal(expected, Describe.MaskAddress(address));
    }
}
