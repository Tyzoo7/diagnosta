using System.Globalization;
using System.Net;
using System.Text;
using Diagnosta.Checks;
using Diagnosta.Localization;
using Diagnosta.SystemInfo;
using Diagnosta.Ui;

namespace Diagnosta.Report;

/// <param name="Anonymize">Shorten the PC's own IP addresses and leave out drive names.</param>
internal sealed record ReportOptions(bool Anonymize, DateTimeOffset CreatedAt, string Version);

/// <summary>
/// Builds the support report: one self-contained HTML file to attach to a ticket.
/// Safety: every value is HTML-encoded, and a Content-Security-Policy forbids scripts and
/// any outside requests, so even a strangely named program can't turn the file into
/// something active. Findings come first, because that is what a helper reads first.
/// </summary>
internal static class SupportReport
{
    private const string Css = """
        body { font-family: "Segoe UI", system-ui, sans-serif; max-width: 860px; margin: 2rem auto; padding: 0 1rem; color: #1b1f24; background: #fff; line-height: 1.45; }
        h1 { margin-bottom: .2rem; } h2 { margin-top: 2rem; border-bottom: 1px solid #d0d7de; padding-bottom: .3rem; }
        .meta, .quiet { color: #57606a; } .privacy { font-size: .9rem; color: #57606a; }
        .status { padding: .8rem 1rem; border-radius: 8px; font-weight: 600; margin: 1.5rem 0; }
        .ok { background: #dafbe1; color: #116329; } .warning { background: #fff8c5; color: #7d4e00; }
        .critical { background: #ffebe9; color: #a40e26; } .info { background: #ddf4ff; color: #0a3069; }
        ul.findings { list-style: none; padding: 0; } ul.findings li { padding: .6rem .8rem; border-radius: 6px; margin-bottom: .5rem; }
        table { border-collapse: collapse; width: 100%; } td { padding: .45rem .5rem; vertical-align: top; border-bottom: 1px solid #eaeef2; }
        td:first-child { font-weight: 600; width: 32%; white-space: nowrap; }
        footer { margin-top: 3rem; font-size: .85rem; color: #57606a; }
        """;

    public static string Build(SystemOverview overview, IReadOnlyList<Finding> findings, ReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(options);

        var html = new StringBuilder();
        var culture = CultureInfo.CurrentUICulture;

        html.Append("<!DOCTYPE html>\n<html lang=\"").Append(E(culture.TwoLetterISOLanguageName)).Append("\">\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<title>").Append(E(Strings.ReportTitle)).Append("</title>\n<style>").Append(Css).Append("</style>\n</head>\n<body>\n");

        html.Append("<h1>").Append(E(Strings.ReportTitle)).Append("</h1>\n")
            .Append("<p class=\"meta\">").Append(E(F(Strings.ReportCreated, options.CreatedAt.ToString("g", CultureInfo.CurrentCulture), options.Version))).Append("</p>\n")
            .Append("<p class=\"privacy\">").Append(E(options.Anonymize ? Strings.ReportAnonymized : Strings.ReportNotAnonymized)).Append("</p>\n");

        var worst = findings.Count == 0 ? "ok" : CssClass(findings.Max(f => f.Severity));
        html.Append("<div class=\"status ").Append(worst).Append("\">").Append(E(Describe.Status(findings.Count))).Append("</div>\n");

        if (findings.Count > 0)
        {
            html.Append("<h2>").Append(E(Strings.FindingsTitle)).Append("</h2>\n<ul class=\"findings\">\n");
            foreach (var finding in findings)
            {
                html.Append("<li class=\"").Append(CssClass(finding.Severity)).Append("\">").Append(E(finding.Message)).Append("</li>\n");
            }

            html.Append("</ul>\n");
        }

        System(html, overview);
        Drives(html, overview, options.Anonymize);
        Battery(html, overview.Battery);
        Network(html, overview.Network, options.Anonymize);
        Startup(html, overview.StartupApps);
        Processes(html, overview.Processes);

        html.Append("<footer>").Append(E(Strings.ReportFooter)).Append("</footer>\n</body>\n</html>\n");
        return html.ToString();
    }

    private static void System(StringBuilder html, SystemOverview overview)
    {
        var rows = new List<(string, string?, string?)>();
        var (windows, windowsDetail) = Describe.Windows(overview.Windows);
        rows.Add((Strings.LabelWindows, windows, windowsDetail));
        rows.Add((Strings.LabelUptime, Formatting.Uptime(overview.Uptime), null));
        if (overview.RestartPending is { } pending)
        {
            rows.Add((Strings.LabelUpdates, Describe.Restart(pending), null));
        }

        rows.Add((Strings.LabelCpu, overview.Cpu.Name, Describe.JoinDots(
            Describe.CpuSummary(overview.Cpu),
            overview.Cpu.UsagePercent is { } usage ? Describe.CpuUsage(usage) : null)));

        if (overview.Memory is { TotalBytes: > 0 } memory)
        {
            rows.Add((Strings.LabelMemory, Describe.MemoryUsage(memory), overview.MemoryModules is { Modules.Count: > 0 } layout
                ? Describe.JoinDots(Describe.Modules(layout.Modules), Describe.Slots(layout))
                : null));
        }
        else
        {
            rows.Add((Strings.LabelMemory, null, null));
        }

        if (overview.Gpus is { Count: > 0 } gpus)
        {
            foreach (var gpu in gpus)
            {
                rows.Add((Strings.LabelGpu, gpu.Name, Describe.JoinDots(Describe.GpuKind(gpu), Describe.GpuDriver(gpu))));
            }
        }
        else
        {
            rows.Add((Strings.LabelGpu, overview.Gpus is null ? null : Strings.GpuNone, null));
        }

        Table(html, Strings.SectionSystem, rows);
    }

    private static void Drives(StringBuilder html, SystemOverview overview, bool anonymize)
    {
        var rows = new List<(string, string?, string?)>();
        foreach (var drive in overview.Drives ?? [])
        {
            var disk = overview.PhysicalDisks?.FirstOrDefault(d => d.DiskNumber is not null && d.DiskNumber == drive.DiskNumber);
            rows.Add((drive.Letter, Describe.DriveTitle(drive, hideLabel: anonymize), Describe.JoinDots(
                Describe.DriveFree(drive),
                disk is null ? null : $"{Strings.LabelHealth}: {Describe.Health(disk.Health)}")));
        }

        if (rows.Count == 0)
        {
            rows.Add((Strings.SectionDrives, overview.Drives is null ? null : Strings.DrivesNone, null));
        }

        Table(html, Strings.SectionDrives, rows);
    }

    private static void Battery(StringBuilder html, BatteryInfo? battery)
    {
        if (battery is not { Present: true })
        {
            return; // desktop PCs: nothing to report
        }

        Table(html, Strings.SectionBattery,
        [
            (Strings.BatteryCharge, Describe.BatteryCharge(battery), null),
            (Strings.BatteryHealth, battery.Health is { } health ? Describe.BatteryHealth(health) : null, Describe.BatteryDetails(battery)),
        ]);
    }

    private static void Network(StringBuilder html, IReadOnlyList<NetworkAdapterInfo>? adapters, bool anonymize)
    {
        var rows = adapters?.Select(a => (Describe.NetworkKindName(a.Kind), (string?)a.Description,
            (string?)Describe.JoinDots(Describe.NetworkStatus(a), Describe.NetworkAddresses(a, anonymize)))).ToList() ?? [];

        if (rows.Count == 0)
        {
            rows.Add((Strings.SectionNetwork, adapters is null ? null : Strings.NetworkNone, null));
        }

        Table(html, Strings.SectionNetwork, rows);
    }

    private static void Startup(StringBuilder html, IReadOnlyList<StartupEntry>? apps)
    {
        if (apps is null)
        {
            return;
        }

        var rows = apps.OrderByDescending(a => a.Enabled).Select(a => (a.Name, (string?)Describe.StartupDetail(a), (string?)null)).ToList();
        Table(html, $"{Strings.SectionStartup} ({Describe.StartupSummary(apps)})", rows);
    }

    private static void Processes(StringBuilder html, IReadOnlyList<ProcessUsage>? processes)
    {
        if (processes is not { Count: > 0 })
        {
            return;
        }

        static string Name(ProcessUsage p) => p.Instances > 1 ? $"{p.Name} ({p.Instances}×)" : p.Name;

        Table(html, $"{Strings.SectionProcesses}: {Strings.ProcessesTopMemory}",
            processes.OrderByDescending(p => p.MemoryBytes).Take(10).Select(p => (Name(p), (string?)Formatting.Bytes(p.MemoryBytes), (string?)null)).ToList());
        Table(html, $"{Strings.SectionProcesses}: {Strings.ProcessesTopCpu}",
            processes.Where(p => p.CpuPercent >= 0.1).OrderByDescending(p => p.CpuPercent).Take(10)
                .Select(p => (Name(p), (string?)Describe.ProcessCpu(p.CpuPercent!.Value), (string?)null)).ToList());
    }

    /// <summary>A heading and a two-column table; the main value with an optional quieter detail line.</summary>
    private static void Table(StringBuilder html, string title, IReadOnlyList<(string Label, string? Main, string? Detail)> rows)
    {
        html.Append("<h2>").Append(E(title)).Append("</h2>\n<table>\n");
        if (rows.Count == 0)
        {
            html.Append("<tr><td colspan=\"2\" class=\"quiet\">").Append(E(Strings.ProcessesAllIdle)).Append("</td></tr>\n");
        }

        foreach (var (label, main, detail) in rows)
        {
            html.Append("<tr><td>").Append(E(label)).Append("</td><td>").Append(E(main ?? Strings.Unknown));
            if (!string.IsNullOrEmpty(detail))
            {
                html.Append("<br><span class=\"quiet\">").Append(E(detail)).Append("</span>");
            }

            html.Append("</td></tr>\n");
        }

        html.Append("</table>\n");
    }

    private static string CssClass(Severity severity) => severity switch
    {
        Severity.Critical => "critical",
        Severity.Warning => "warning",
        _ => "info",
    };

    /// <summary>HTML-encodes text that came from the system (program names, drive labels, …).</summary>
    private static string E(string text) => WebUtility.HtmlEncode(Describe.Printable(text));

    private static string F(string template, params object[] values) => string.Format(CultureInfo.CurrentCulture, template, values);
}
