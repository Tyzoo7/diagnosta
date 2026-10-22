using System.Globalization;
using Diagnosta.Checks;
using Diagnosta.Localization;
using Diagnosta.SystemInfo;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Diagnosta.Ui;

/// <summary>Renders the system overview table and the findings below it.</summary>
internal static class SystemOverviewView
{
    public static void Render(IAnsiConsole console, SystemOverview overview, IReadOnlyList<Finding> findings)
    {
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.Grey)
            .Title($"[bold]{Markup.Escape(Strings.OverviewTitle)}[/]")
            .HideHeaders()
            .AddColumn(string.Empty)
            .AddColumn(string.Empty);

        AddRow(table, Strings.LabelWindows, WindowsText(overview.Windows));
        AddRow(table, Strings.LabelCpu, CpuText(overview.Cpu));
        table.AddRow(Label(Strings.LabelMemory), MemoryCell(overview.Memory));
        AddRow(table, Strings.LabelGpu, GpuText(overview.Gpus));
        AddRow(table, Strings.LabelUptime, Formatting.Uptime(overview.Uptime));

        console.Write(table);
        console.WriteLine();
        console.MarkupLine($"[bold]{Markup.Escape(Strings.FindingsTitle)}[/]");

        // A grid keeps wrapped lines aligned under the text instead of under the symbol.
        var list = new Grid().AddColumn(new GridColumn().NoWrap().Padding(1, 0, 1, 0)).AddColumn();
        foreach (var finding in findings)
        {
            var (symbol, color) = finding.Severity switch
            {
                Severity.Warning => ("!", "yellow"),
                Severity.Info => ("i", "deepskyblue1"),
                _ => ("✓", "green"),
            };
            list.AddRow(new Markup($"[{color} bold]{symbol}[/]"), new Markup(Markup.Escape(finding.Message)));
        }

        console.Write(list);
    }

    private static void AddRow(Table table, string label, string? value) =>
        table.AddRow(Label(label), new Markup(Markup.Escape(value ?? Strings.Unknown)));

    private static Markup Label(string label) => new($"[bold]{Markup.Escape(label)}[/]");

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
            return new Markup(Markup.Escape(Strings.Unknown));
        }

        var text = string.Format(
            CultureInfo.CurrentCulture,
            Strings.MemoryUsage,
            Formatting.Bytes(memory.UsedBytes),
            Formatting.Bytes(memory.TotalBytes),
            Formatting.Percent(memory.UsedFraction));

        var color = memory.UsedFraction >= SystemOverviewAnalyzer.MemoryNearlyFullFraction ? Color.Yellow : Color.Green;
        var bar = new BreakdownChart()
            .Width(30)
            .HideTags()
            .AddItem(Strings.MemoryUsed, Math.Round(memory.UsedFraction * 100, 1), color)
            .AddItem(Strings.MemoryFree, Math.Round((1 - memory.UsedFraction) * 100, 1), Color.Grey23);

        return new Rows(new Markup(Markup.Escape(text)), bar);
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

        return string.Join(
            Environment.NewLine,
            gpus.Select(gpu => gpu.DedicatedMemoryBytes is { } vram
                ? $"{gpu.Name} ({string.Format(CultureInfo.CurrentCulture, Strings.GpuMemory, Formatting.Bytes(vram))})"
                : gpu.Name));
    }
}
