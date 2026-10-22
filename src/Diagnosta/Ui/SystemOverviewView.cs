using System.Globalization;
using Diagnosta.Checks;
using Diagnosta.Localization;
using Diagnosta.SystemInfo;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Diagnosta.Ui;

/// <summary>
/// Renders checks as stacked panels of equal width: a one-line status first (the answer to
/// "is my PC okay?"), then the details, then what to do about them. Each item has a main
/// line and a quieter detail line, with a blank line between items so it reads easily.
/// </summary>
internal static class SystemOverviewView
{
    public static IReadOnlyList<CheckArea> AllAreas { get; } = Enum.GetValues<CheckArea>();

    private const int MaxWidth = 96;
    private const int BarWidth = 30;

    /// <summary>Secondary text: readable on black and on PowerShell's blue background.</summary>
    private static readonly Color Quiet = Color.Grey70;

    public static void Render(IAnsiConsole console, SystemOverview overview, IReadOnlyList<Finding> findings) =>
        Render(console, overview, findings, AllAreas);

    public static void Render(IAnsiConsole console, SystemOverview overview, IReadOnlyList<Finding> findings, IReadOnlyCollection<CheckArea> areas)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(overview);

        var width = Math.Min(console.Profile.Width, MaxWidth);
        var relevant = findings.Where(f => areas.Contains(f.Area)).ToList();

        var status = StatusPanel(relevant);
        status.Width = width;
        console.Write(status);

        if (SystemGrid(overview, areas) is { } system)
        {
            console.Write(Section(Strings.SectionSystem, system, width));
        }

        if (areas.Contains(CheckArea.Drives))
        {
            console.Write(Section(Strings.SectionDrives, DrivesGrid(overview.Drives, overview.PhysicalDisks), width));
        }

        if (relevant.Count > 0)
        {
            console.Write(Section(Strings.FindingsTitle, FindingsGrid(relevant), width));
        }
    }

    private static Panel StatusPanel(List<Finding> findings)
    {
        var text = findings.Count switch
        {
            0 => Strings.StatusAllGood,
            1 => Strings.StatusOneFinding,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.StatusFindings, findings.Count),
        };

        // The status takes the look of the most serious finding.
        var (symbol, color) = findings.Count == 0
            ? ("✓", Color.Green)
            : Look(findings.Max(f => f.Severity));

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

    private static Grid? SystemGrid(SystemOverview overview, IReadOnlyCollection<CheckArea> areas)
    {
        var grid = NewTwoColumnGrid();
        var rows = 0;

        void Item(string label, string? main, IRenderable? detail = null)
        {
            if (rows++ > 0)
            {
                grid.AddEmptyRow();
            }

            // Graphics has no single main line, only its per-GPU lines (passed as detail).
            IRenderable cell = (main, detail) switch
            {
                (null, not null) => detail,
                (_, null) => Text(main),
                _ => new Rows(Text(main), detail),
            };
            grid.AddRow(Label(label), cell);
        }

        if (areas.Contains(CheckArea.Windows))
        {
            var (name, detail) = WindowsText(overview.Windows);
            Item(Strings.LabelWindows, name, QuietText(detail));
            Item(Strings.LabelUptime, Formatting.Uptime(overview.Uptime));
        }

        if (areas.Contains(CheckArea.Processor))
        {
            Item(Strings.LabelCpu, overview.Cpu.Name ?? Strings.Unknown, CpuDetails(overview.Cpu));
        }

        if (areas.Contains(CheckArea.Memory))
        {
            var (main, details) = MemoryLines(overview.Memory, overview.MemoryModules);
            Item(Strings.LabelMemory, main, details);
        }

        if (areas.Contains(CheckArea.Graphics))
        {
            Item(Strings.LabelGpu, null, GpuLines(overview.Gpus));
        }

        return rows == 0 ? null : grid;
    }

    private static IRenderable DrivesGrid(IReadOnlyList<VolumeInfo>? drives, IReadOnlyList<PhysicalDiskInfo>? disks)
    {
        if (drives is null || drives.Count == 0)
        {
            return Text(drives is null ? Strings.Unknown : Strings.DrivesNone);
        }

        var grid = NewTwoColumnGrid();
        for (var i = 0; i < drives.Count; i++)
        {
            if (i > 0)
            {
                grid.AddEmptyRow();
            }

            var drive = drives[i];
            var disk = disks?.FirstOrDefault(d => d.DiskNumber is not null && d.DiskNumber == drive.DiskNumber);
            var details = string.Join(" · ", new[] { drive.IsSystemDrive ? "Windows" : drive.Label, DeviceText(drive) }
                .Where(p => !string.IsNullOrEmpty(p)));
            var free = string.Format(CultureInfo.CurrentCulture, Strings.DriveFree, Formatting.Bytes(drive.FreeBytes), Formatting.Bytes(drive.TotalBytes), Formatting.Percent(drive.UsedFraction));
            var color = SystemOverviewAnalyzer.IsNearlyFull(drive) ? Color.Red : drive.UsedFraction >= 0.75 ? Color.Yellow : Color.Green;

            var firstLine = Markup.Escape(details.Length == 0 ? Strings.Unknown : details)
                + (disk is null ? string.Empty : "  " + HealthBadge(disk.Health));

            grid.AddRow(
                Label(drive.Letter),
                new Rows(
                    new Markup(firstLine),
                    new Columns(UsageBar(drive.UsedFraction, color), Text(free)).Collapse()));
        }

        // Disks without a drive letter of their own (or whose volumes span several disks).
        var listed = drives.Select(d => d.DiskNumber).Where(n => n is not null).ToHashSet();
        foreach (var disk in disks?.Where(d => d.DiskNumber is null || !listed.Contains(d.DiskNumber)) ?? [])
        {
            grid.AddEmptyRow();
            grid.AddRow(Label(Strings.LabelHealth), new Markup($"{Markup.Escape(disk.Name ?? Strings.DiskUnnamed)}  {HealthBadge(disk.Health)}"));
        }

        return grid;
    }

    private static string HealthBadge(DiskHealth health) => health switch
    {
        DiskHealth.Healthy => $"[green bold]✓ {Markup.Escape(Strings.HealthHealthy)}[/]",
        DiskHealth.Warning => $"[yellow bold]! {Markup.Escape(Strings.HealthWarning)}[/]",
        DiskHealth.Unhealthy => $"[red bold]✗ {Markup.Escape(Strings.HealthUnhealthy)}[/]",
        _ => $"[{Quiet.ToMarkup()}]? {Markup.Escape(Strings.Unknown)}[/]",
    };

    private static Grid FindingsGrid(List<Finding> findings)
    {
        // A grid keeps wrapped lines aligned under the text instead of under the symbol.
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(1))
            .AddColumn();

        for (var i = 0; i < findings.Count; i++)
        {
            if (i > 0)
            {
                grid.AddEmptyRow();
            }

            var (symbol, color) = Look(findings[i].Severity);
            grid.AddRow(new Markup($"[{color.ToMarkup()} bold]{symbol}[/]"), Text(findings[i].Message));
        }

        return grid;
    }

    private static (string Symbol, Color Color) Look(Severity severity) => severity switch
    {
        Severity.Critical => ("✗", Color.Red),
        Severity.Warning => ("!", Color.Yellow),
        _ => ("i", Color.DeepSkyBlue1),
    };

    private static Grid NewTwoColumnGrid() =>
        new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(3))
            .AddColumn();

    private static Markup Label(string label) => new($"[bold]{Markup.Escape(label)}[/]");

    private static Markup Text(string? text) => new(Markup.Escape(text ?? Strings.Unknown));

    private static Markup? QuietText(string? text) =>
        string.IsNullOrEmpty(text) ? null : new Markup($"[{Quiet.ToMarkup()}]{Markup.Escape(text)}[/]");

    private static BreakdownChart UsageBar(double usedFraction, Color color) =>
        new BreakdownChart()
            .Width(BarWidth)
            .HideTags()
            .AddItem(Strings.MemoryUsed, Math.Round(usedFraction * 100, 1), color)
            .AddItem(Strings.MemoryFree, Math.Round((1 - usedFraction) * 100, 1), Color.Grey23);

    /// <summary>"Windows 11 Pro" and "Version 24H2 · Build 26100.2314".</summary>
    private static (string? Name, string? Detail) WindowsText(WindowsInfo? windows)
    {
        if (windows is null)
        {
            return (null, null);
        }

        var name = string.Join(' ', new[] { windows.Name, windows.Edition }.Where(p => !string.IsNullOrEmpty(p)));
        var build = windows.UpdateRevision is { } ubr
            ? $"{windows.Build}.{ubr}"
            : windows.Build.ToString(CultureInfo.InvariantCulture);
        var parts = new[]
        {
            windows.DisplayVersion is null ? null : string.Format(CultureInfo.CurrentCulture, Strings.WindowsVersion, windows.DisplayVersion),
            string.Format(CultureInfo.CurrentCulture, Strings.WindowsBuild, build),
        };
        return (name, string.Join(" · ", parts.Where(p => p is not null)));
    }

    /// <summary>"4 cores · 8 threads · base clock 1.60 GHz", then the current load with a bar.</summary>
    private static Rows CpuDetails(CpuInfo cpu)
    {
        var counts = cpu.PhysicalCores is { } cores
            ? string.Format(CultureInfo.CurrentCulture, Strings.CpuCoresThreads, cores, cpu.LogicalProcessors)
            : string.Format(CultureInfo.CurrentCulture, Strings.CpuThreads, cpu.LogicalProcessors);
        var clock = cpu.BaseMhz is { } mhz and > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.CpuBaseClock, Formatting.Gigahertz(mhz))
            : null;

        var lines = new List<IRenderable> { QuietText(JoinDots(counts, clock))! };
        if (cpu.UsagePercent is { } usage)
        {
            var fraction = usage / 100;
            var color = usage >= SystemOverviewAnalyzer.CpuBusyPercent ? Color.Yellow : Color.Green;
            lines.Add(new Columns(
                UsageBar(fraction, color),
                QuietText(string.Format(CultureInfo.CurrentCulture, Strings.CpuUsage, Formatting.Percent(fraction)))!).Collapse());
        }

        return new Rows(lines);
    }

    private static (string Main, IRenderable? Details) MemoryLines(MemoryInfo? memory, MemoryLayout? layout)
    {
        if (memory is null || memory.TotalBytes == 0)
        {
            return (Strings.Unknown, null);
        }

        var text = string.Format(
            CultureInfo.CurrentCulture,
            Strings.MemoryUsage,
            Formatting.Bytes(memory.UsedBytes),
            Formatting.Bytes(memory.TotalBytes),
            Formatting.Percent(memory.UsedFraction));

        var color = memory.UsedFraction >= SystemOverviewAnalyzer.MemoryNearlyFullFraction ? Color.Yellow : Color.Green;
        var lines = new List<IRenderable> { UsageBar(memory.UsedFraction, color) };

        if (layout is { Modules.Count: > 0 })
        {
            lines.Add(QuietText(ModulesText(layout.Modules))!);
            lines.Add(QuietText(string.Format(CultureInfo.CurrentCulture, Strings.MemorySlots, layout.Modules.Count, layout.SlotCount))!);
        }

        return (text, new Rows(lines));
    }

    /// <summary>"2 modules: 8 GB + 16 GB · DDR4 · 2667 MT/s".</summary>
    private static string ModulesText(IReadOnlyList<MemoryModuleInfo> modules)
    {
        var sizes = string.Join(" + ", modules.Select(m => Formatting.Bytes(m.SizeBytes)));
        var count = modules.Count == 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.MemoryModulesOne, sizes)
            : string.Format(CultureInfo.CurrentCulture, Strings.MemoryModulesMany, modules.Count, sizes);
        var types = string.Join("/", modules.Select(m => m.Type).OfType<string>().Distinct());
        // All modules run at the speed of the slowest one.
        var speed = modules.Select(m => m.SpeedMts).OfType<int>().DefaultIfEmpty().Min() is var s and > 0
            ? string.Create(CultureInfo.CurrentCulture, $"{s} MT/s")
            : null;
        return JoinDots(count, types.Length == 0 ? null : types, speed);
    }

    private static string JoinDots(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));

    /// <summary>One main line per GPU with a quiet detail line below it.</summary>
    private static IRenderable GpuLines(IReadOnlyList<GpuInfo>? gpus)
    {
        if (gpus is null || gpus.Count == 0)
        {
            return Text(gpus is null ? Strings.Unknown : Strings.GpuNone);
        }

        var lines = new List<IRenderable>();
        foreach (var gpu in gpus)
        {
            var detail = gpu switch
            {
                { IsIntegrated: true, SharedMemoryBytes: { } shared } =>
                    string.Format(CultureInfo.CurrentCulture, Strings.GpuIntegratedUpTo, Formatting.Bytes(shared)),
                { IsIntegrated: true } => Strings.GpuIntegrated,
                _ => string.Format(CultureInfo.CurrentCulture, Strings.GpuMemory, Formatting.Bytes(gpu.DedicatedMemoryBytes!.Value)),
            };
            lines.Add(Text(gpu.Name));
            lines.Add(QuietText(detail)!);

            if (gpu.DriverVersion is { } version)
            {
                lines.Add(QuietText(gpu.DriverDate is { } date
                    ? string.Format(CultureInfo.CurrentCulture, Strings.GpuDriver, version, date.ToString("d", CultureInfo.CurrentCulture))
                    : string.Format(CultureInfo.CurrentCulture, Strings.GpuDriverVersionOnly, version))!);
            }
        }

        return new Rows(lines);
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
