using System.Globalization;
using Diagnosta.Checks;
using Diagnosta.Localization;
using Diagnosta.SystemInfo;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Diagnosta.Ui;

/// <summary>
/// Renders the overview as stacked sections of equal width: a one-line status first
/// (the answer to "is my PC okay?"), then the details, then what to do about them.
/// </summary>
internal static class SystemOverviewView
{
    private const int MaxWidth = 96;
    private const int BarWidth = 30;

    public static void Render(IAnsiConsole console, SystemOverview overview, IReadOnlyList<Finding> findings)
    {
        var width = Math.Min(console.Profile.Width, MaxWidth);

        var status = StatusPanel(findings);
        status.Width = width;
        console.Write(status);
        console.Write(Section(Strings.SectionSystem, SystemGrid(overview), width));
        console.Write(Section(Strings.SectionDrives, DrivesGrid(overview.Drives), width));

        if (findings.Count > 0)
        {
            console.Write(Section(Strings.FindingsTitle, FindingsGrid(findings), width));
        }
    }

    private static Panel StatusPanel(IReadOnlyList<Finding> findings)
    {
        var hasWarning = findings.Any(f => f.Severity == Severity.Warning);
        var (symbol, color, text) = findings.Count switch
        {
            0 => ("✓", Color.Green, Strings.StatusAllGood),
            1 => (hasWarning ? "!" : "i", hasWarning ? Color.Yellow : Color.DeepSkyBlue1, Strings.StatusOneFinding),
            _ => (hasWarning ? "!" : "i", hasWarning ? Color.Yellow : Color.DeepSkyBlue1,
                string.Format(CultureInfo.CurrentCulture, Strings.StatusFindings, findings.Count)),
        };

        return new Panel(new Markup($"[bold {color.ToMarkup()}]{symbol}  {Markup.Escape(text)}[/]"))
            .RoundedBorder()
            .BorderColor(color);
    }

    private static Panel Section(string title, IRenderable content, int width) =>
        new Panel(content)
        {
            Header = new PanelHeader($"[bold deepskyblue1] {Markup.Escape(title)} [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0, 1, 0),
            Width = width,
        };

    private static Grid SystemGrid(SystemOverview overview)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        AddRow(grid, Strings.LabelWindows, Text(WindowsText(overview.Windows)));
        AddRow(grid, Strings.LabelCpu, Text(CpuText(overview.Cpu)));
        AddRow(grid, Strings.LabelMemory, MemoryCell(overview.Memory));
        AddRow(grid, Strings.LabelGpu, Text(GpuText(overview.Gpus)));
        AddRow(grid, Strings.LabelUptime, Text(Formatting.Uptime(overview.Uptime)));
        return grid;
    }

    private static IRenderable DrivesGrid(IReadOnlyList<VolumeInfo>? drives)
    {
        if (drives is null || drives.Count == 0)
        {
            return Text(drives is null ? Strings.Unknown : Strings.DrivesNone);
        }

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        foreach (var drive in drives)
        {
            var details = string.Join(" · ", new[] { drive.IsSystemDrive ? "Windows" : drive.Label, DeviceText(drive) }
                .Where(p => !string.IsNullOrEmpty(p)));
            var free = string.Format(CultureInfo.CurrentCulture, Strings.DriveFree, Formatting.Bytes(drive.FreeBytes), Formatting.Bytes(drive.TotalBytes), Formatting.Percent(drive.UsedFraction));
            var color = SystemOverviewAnalyzer.IsNearlyFull(drive) ? Color.Red : drive.UsedFraction >= 0.75 ? Color.Yellow : Color.Green;

            grid.AddRow(
                new Markup($"[bold]{Markup.Escape(drive.Letter)}[/]"),
                new Rows(
                    Text(details.Length == 0 ? Strings.Unknown : details),
                    new Columns(UsageBar(drive.UsedFraction, color), Text(free)).Collapse()));
        }

        return grid;
    }

    private static Grid FindingsGrid(IReadOnlyList<Finding> findings)
    {
        // A grid keeps wrapped lines aligned under the text instead of under the symbol.
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(1))
            .AddColumn();

        foreach (var finding in findings)
        {
            var (symbol, color) = finding.Severity == Severity.Warning ? ("!", "yellow") : ("i", "deepskyblue1");
            grid.AddRow(new Markup($"[{color} bold]{symbol}[/]"), Text(finding.Message));
        }

        return grid;
    }

    private static void AddRow(Grid grid, string label, IRenderable value) =>
        grid.AddRow(new Markup($"[bold]{Markup.Escape(label)}[/]"), value);

    private static Markup Text(string? text) => new(Markup.Escape(text ?? Strings.Unknown));

    private static BreakdownChart UsageBar(double usedFraction, Color color) =>
        new BreakdownChart()
            .Width(BarWidth)
            .HideTags()
            .AddItem(Strings.MemoryUsed, Math.Round(usedFraction * 100, 1), color)
            .AddItem(Strings.MemoryFree, Math.Round((1 - usedFraction) * 100, 1), Color.Grey23);

    private static string? WindowsText(WindowsInfo? windows)
    {
        if (windows is null)
        {
            return null;
        }

        var name = string.Join(' ', new[] { windows.Name, windows.Edition, windows.DisplayVersion }.Where(p => !string.IsNullOrEmpty(p)));
        var build = windows.UpdateRevision is { } ubr
            ? $"{windows.Build}.{ubr}"
            : windows.Build.ToString(CultureInfo.InvariantCulture);
        return $"{name} ({string.Format(CultureInfo.CurrentCulture, Strings.WindowsBuild, build)})";
    }

    private static string CpuText(CpuInfo cpu)
    {
        var counts = cpu.PhysicalCores is { } cores
            ? string.Format(CultureInfo.CurrentCulture, Strings.CpuCoresThreads, cores, cpu.LogicalProcessors)
            : string.Format(CultureInfo.CurrentCulture, Strings.CpuThreads, cpu.LogicalProcessors);
        return $"{cpu.Name ?? Strings.Unknown} ({counts})";
    }

    private static IRenderable MemoryCell(MemoryInfo? memory)
    {
        if (memory is null || memory.TotalBytes == 0)
        {
            return Text(Strings.Unknown);
        }

        var text = string.Format(
            CultureInfo.CurrentCulture,
            Strings.MemoryUsage,
            Formatting.Bytes(memory.UsedBytes),
            Formatting.Bytes(memory.TotalBytes),
            Formatting.Percent(memory.UsedFraction));

        var color = memory.UsedFraction >= SystemOverviewAnalyzer.MemoryNearlyFullFraction ? Color.Yellow : Color.Green;
        return new Rows(Text(text), UsageBar(memory.UsedFraction, color));
    }

    private static string GpuText(IReadOnlyList<GpuInfo>? gpus)
    {
        if (gpus is null)
        {
            return Strings.Unknown;
        }

        if (gpus.Count == 0)
        {
            return Strings.GpuNone;
        }

        return string.Join(Environment.NewLine, gpus.Select(gpu =>
        {
            var detail = gpu.IsIntegrated
                ? Strings.GpuIntegrated
                : string.Format(CultureInfo.CurrentCulture, Strings.GpuMemory, Formatting.Bytes(gpu.DedicatedMemoryBytes!.Value));
            return $"{gpu.Name} ({detail})";
        }));
    }

    /// <summary>E.g. "NVMe SSD · Samsung SSD 980 PRO 1TB".</summary>
    private static string? DeviceText(VolumeInfo drive)
    {
        var kind = drive.Kind switch
        {
            DriveKind.Ssd => "SSD",
            DriveKind.Hdd => Strings.DriveHdd,
            _ => null,
        };
        var type = string.Join(' ', new[] { drive.BusType, kind }.Where(p => p is not null));
        var parts = new[] { type, drive.Model }.Where(p => !string.IsNullOrEmpty(p)).ToArray();
        return parts.Length == 0 ? null : string.Join(" · ", parts);
    }
}
