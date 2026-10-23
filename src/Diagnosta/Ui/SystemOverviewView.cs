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
    private const int TopProcesses = 5;

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

        // In the full check a desktop PC simply has no battery panel; asked directly, it says so.
        if (areas.Contains(CheckArea.Battery) && (areas.Count == 1 || overview.Battery is not { Present: false }))
        {
            console.Write(Section(Strings.SectionBattery, BatteryGrid(overview.Battery), width));
        }

        if (areas.Contains(CheckArea.Network))
        {
            console.Write(Section(Strings.SectionNetwork, NetworkGrid(overview.Network), width));
        }

        if (areas.Contains(CheckArea.Startup))
        {
            console.Write(Section(Strings.SectionStartup, StartupGrid(overview.StartupApps), width));
        }

        if (areas.Contains(CheckArea.Processes))
        {
            console.Write(Section(Strings.SectionProcesses, ProcessesGrid(overview.Processes), width));
        }

        if (relevant.Count > 0)
        {
            console.Write(Section(Strings.FindingsTitle, FindingsGrid(relevant), width));
        }
    }

    private static IRenderable BatteryGrid(BatteryInfo? battery)
    {
        if (battery is null)
        {
            return Text(Strings.Unknown);
        }

        if (!battery.Present)
        {
            return Text(Strings.BatteryNone);
        }

        var grid = NewTwoColumnGrid();
        grid.AddRow(Label(Strings.BatteryCharge), Text(Describe.BatteryCharge(battery)));

        grid.AddEmptyRow();
        if (battery.Health is { } health)
        {
            var color = health < SystemOverviewAnalyzer.BatteryReplaceHealth ? Color.Red
                : health < SystemOverviewAnalyzer.BatteryWornHealth ? Color.Yellow
                : Color.Green;
            grid.AddRow(
                Label(Strings.BatteryHealth),
                new Rows(
                    Text(Describe.BatteryHealth(health)),
                    UsageBar(health, color),
                    QuietText(Describe.BatteryDetails(battery)) ?? BlankLine()));
        }
        else
        {
            grid.AddRow(Label(Strings.BatteryHealth), Text(Strings.Unknown));
        }

        return grid;
    }

    private static IRenderable NetworkGrid(IReadOnlyList<NetworkAdapterInfo>? adapters)
    {
        if (adapters is null)
        {
            return Text(Strings.Unknown);
        }

        if (adapters.Count == 0)
        {
            return Text(Strings.NetworkNone);
        }

        var grid = NewTwoColumnGrid();
        for (var i = 0; i < adapters.Count; i++)
        {
            if (i > 0)
            {
                grid.AddEmptyRow();
            }

            var adapter = adapters[i];
            var lines = new List<IRenderable> { Text(adapter.Description), QuietText(Describe.NetworkStatus(adapter))! };
            if (QuietText(Describe.NetworkAddresses(adapter)) is { } addressLine)
            {
                lines.Add(addressLine);
            }

            grid.AddRow(Label(Describe.NetworkKindName(adapter.Kind)), new Rows(lines));
        }

        return grid;
    }

    /// <summary>How many start with Windows, then each one; disabled ones in quiet grey.</summary>
    private static IRenderable StartupGrid(IReadOnlyList<StartupEntry>? apps)
    {
        if (apps is null)
        {
            return Text(Strings.Unknown);
        }

        var summary = Describe.StartupSummary(apps);

        // Headings sit above the grid, so they don't stretch the name column.
        var grid = NewTwoColumnGrid();
        foreach (var app in apps.OrderByDescending(a => a.Enabled))
        {
            grid.AddRow(
                new Markup(app.Enabled ? Markup.Escape(app.Name) : $"[{Quiet.ToMarkup()}]{Markup.Escape(app.Name)}[/]"),
                QuietText(Describe.StartupDetail(app)) ?? Text(string.Empty));
        }

        return new Rows(new Markup($"[bold]{Markup.Escape(summary)}[/]"), BlankLine(), grid);
    }

    /// <summary>The five programs using the most memory, then the five using the most CPU.</summary>
    private static IRenderable ProcessesGrid(IReadOnlyList<ProcessUsage>? processes)
    {
        if (processes is null || processes.Count == 0)
        {
            return Text(Strings.Unknown);
        }

        var memory = NewTwoColumnGrid();
        foreach (var p in processes.OrderByDescending(p => p.MemoryBytes).Take(TopProcesses))
        {
            memory.AddRow(ProcessName(p), Text(Formatting.Bytes(p.MemoryBytes)));
        }

        var busy = processes.Where(p => p.CpuPercent >= 0.1).OrderByDescending(p => p.CpuPercent).Take(TopProcesses).ToList();
        IRenderable cpu = QuietText(Strings.ProcessesAllIdle)!;
        if (busy.Count > 0)
        {
            var grid = NewTwoColumnGrid();
            foreach (var p in busy)
            {
                grid.AddRow(ProcessName(p), Text(Describe.ProcessCpu(p.CpuPercent!.Value)));
            }

            cpu = grid;
        }

        return new Rows(
            new Markup($"[bold]{Markup.Escape(Strings.ProcessesTopMemory)}[/]"),
            memory,
            BlankLine(),
            new Markup($"[bold]{Markup.Escape(Strings.ProcessesTopCpu)}[/]"),
            cpu);
    }

    /// <summary>"chrome (12×)" when a program runs as several processes.</summary>
    private static Markup ProcessName(ProcessUsage process) =>
        new(process.Instances > 1
            ? $"{Markup.Escape(process.Name)} [{Quiet.ToMarkup()}]({process.Instances}×)[/]"
            : Markup.Escape(process.Name));

    private static Panel StatusPanel(List<Finding> findings)
    {
        var text = Describe.Status(findings.Count);

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
            var (name, detail) = Describe.Windows(overview.Windows);
            Item(Strings.LabelWindows, name, QuietText(detail));
            Item(Strings.LabelUptime, Formatting.Uptime(overview.Uptime));
            if (overview.RestartPending is { } pending)
            {
                Item(Strings.LabelUpdates, Describe.Restart(pending));
            }
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
            var free = Describe.DriveFree(drive);
            var color = SystemOverviewAnalyzer.IsNearlyFull(drive) ? Color.Red : drive.UsedFraction >= 0.75 ? Color.Yellow : Color.Green;

            var firstLine = Markup.Escape(Describe.DriveTitle(drive))
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

    private static string HealthBadge(DiskHealth health)
    {
        var (symbol, color) = health switch
        {
            DiskHealth.Healthy => ("✓", "green bold"),
            DiskHealth.Warning => ("!", "yellow bold"),
            DiskHealth.Unhealthy => ("✗", "red bold"),
            _ => ("?", Quiet.ToMarkup()),
        };
        return $"[{color}]{symbol} {Markup.Escape(Describe.Health(health))}[/]";
    }

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

    /// <summary>An empty Markup renders no line at all; a single space does.</summary>
    private static Markup BlankLine() => new(" ");

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
    /// <summary>"4 cores · 8 threads · base clock 1.6 GHz", then the current load with a bar.</summary>
    private static Rows CpuDetails(CpuInfo cpu)
    {
        var lines = new List<IRenderable> { QuietText(Describe.CpuSummary(cpu))! };
        if (cpu.UsagePercent is { } usage)
        {
            var color = usage >= SystemOverviewAnalyzer.CpuBusyPercent ? Color.Yellow : Color.Green;
            lines.Add(new Columns(UsageBar(usage / 100, color), QuietText(Describe.CpuUsage(usage))!).Collapse());
        }

        return new Rows(lines);
    }

    private static (string Main, IRenderable? Details) MemoryLines(MemoryInfo? memory, MemoryLayout? layout)
    {
        if (memory is null || memory.TotalBytes == 0)
        {
            return (Strings.Unknown, null);
        }

        var color = memory.UsedFraction >= SystemOverviewAnalyzer.MemoryNearlyFullFraction ? Color.Yellow : Color.Green;
        var lines = new List<IRenderable> { UsageBar(memory.UsedFraction, color) };

        if (layout is { Modules.Count: > 0 })
        {
            lines.Add(QuietText(Describe.Modules(layout.Modules))!);
            lines.Add(QuietText(Describe.Slots(layout))!);
        }

        return (Describe.MemoryUsage(memory), new Rows(lines));
    }

    /// <summary>One main line per GPU with quiet detail lines below it.</summary>
    private static IRenderable GpuLines(IReadOnlyList<GpuInfo>? gpus)
    {
        if (gpus is null || gpus.Count == 0)
        {
            return Text(gpus is null ? Strings.Unknown : Strings.GpuNone);
        }

        var lines = new List<IRenderable>();
        foreach (var gpu in gpus)
        {
            lines.Add(Text(gpu.Name));
            lines.Add(QuietText(Describe.GpuKind(gpu))!);
            if (QuietText(Describe.GpuDriver(gpu)) is { } driver)
            {
                lines.Add(driver);
            }
        }

        return new Rows(lines);
    }
}
