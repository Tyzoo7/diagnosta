using System.Globalization;
using Diagnosta.Localization;
using Diagnosta.SystemInfo;
using Diagnosta.Ui;

namespace Diagnosta.Checks;

/// <summary>Turns raw system values into advice a non-technical user can act on.</summary>
internal static class SystemOverviewAnalyzer
{
    /// <summary>After a week without restart, pending updates and slowdowns become likely.</summary>
    public static readonly TimeSpan LongUptime = TimeSpan.FromDays(7);

    /// <summary>
    /// "8 GB" PCs report a bit less (memory reserved for built-in graphics),
    /// so the warning starts clearly below that.
    /// </summary>
    public const ulong LowMemoryBytes = 7UL * 1024 * 1024 * 1024;

    public const double MemoryNearlyFullFraction = 0.9;

    public const double CpuBusyPercent = 90;

    /// <summary>Below this, adding a module is a cheap and noticeable upgrade.</summary>
    public const ulong UpgradeWorthwhileBelowBytes = 16UL * 1024 * 1024 * 1024;

    /// <summary>Graphics drivers this old often miss fixes for current games and Windows.</summary>
    public const int OldDriverYears = 2;

    /// <summary>From this many enabled startup apps on, sign-in gets noticeably slower.</summary>
    public const int ManyStartupApps = 10;

    public const double DriveNearlyFullFraction = 0.9;

    /// <summary>Below this, Windows updates and temp files start to run out of room.</summary>
    public const ulong SystemDriveMinFreeBytes = 10UL * 1024 * 1024 * 1024;

    /// <summary>Returns only things worth mentioning, warnings first. Empty means "all good".</summary>
    public static IReadOnlyList<Finding> Analyze(SystemOverview overview, DateOnly? today = null)
    {
        ArgumentNullException.ThrowIfNull(overview);
        var now = today ?? DateOnly.FromDateTime(DateTime.Now);

        var findings = new List<Finding>();

        if (overview.Windows?.IsWindows10 == true)
        {
            findings.Add(new Finding(Severity.Warning, CheckArea.Windows, Strings.FindingWindows10));
        }

        if (overview.Uptime >= LongUptime)
        {
            findings.Add(new Finding(Severity.Warning, CheckArea.Windows, Format(Strings.FindingLongUptime, (int)overview.Uptime.TotalDays)));

            // Explains the classic "but I shut it down every evening!" situation.
            if (overview.FastStartupEnabled == true)
            {
                findings.Add(new Finding(Severity.Info, CheckArea.Windows, Strings.FindingFastStartup));
            }
        }

        if (overview.RestartPending == true)
        {
            findings.Add(new Finding(Severity.Warning, CheckArea.Windows, Strings.FindingRestartPending));
        }

        if (overview.Cpu.UsagePercent is >= CpuBusyPercent and var usage)
        {
            findings.Add(new Finding(Severity.Warning, CheckArea.Processor, Format(Strings.FindingCpuBusy, Formatting.Percent(usage / 100))));

            // Name the culprit, so the user knows what to close.
            if (overview.Processes?.Where(p => p.CpuPercent > 0).MaxBy(p => p.CpuPercent) is { } top)
            {
                findings.Add(new Finding(Severity.Info, CheckArea.Processes, Format(Strings.FindingTopCpuApp, top.Name, Formatting.Percent(top.CpuPercent!.Value / 100))));
            }
        }

        if (overview.StartupApps?.Count(a => a.Enabled) is >= ManyStartupApps and var startupCount)
        {
            findings.Add(new Finding(Severity.Info, CheckArea.Startup, Format(Strings.FindingManyStartupApps, startupCount)));
        }

        if (overview.Memory is { TotalBytes: > 0 and < UpgradeWorthwhileBelowBytes } && overview.MemoryModules is { FreeSlots: > 0 })
        {
            findings.Add(new Finding(Severity.Info, CheckArea.Memory, Strings.FindingMemoryUpgrade));
        }

        foreach (var gpu in overview.Gpus ?? [])
        {
            if (gpu.DriverDate is { } date && date.AddYears(OldDriverYears) < now)
            {
                findings.Add(new Finding(Severity.Info, CheckArea.Graphics, Format(Strings.FindingGpuDriverOld, gpu.Name, date.Year)));
            }
        }

        if (overview.Memory is { } memory)
        {
            if (memory.TotalBytes > 0 && memory.TotalBytes < LowMemoryBytes)
            {
                findings.Add(new Finding(Severity.Info, CheckArea.Memory, Format(Strings.FindingLowMemory, Formatting.Bytes(memory.TotalBytes))));
            }

            if (memory.UsedFraction >= MemoryNearlyFullFraction)
            {
                findings.Add(new Finding(Severity.Warning, CheckArea.Memory, Format(Strings.FindingMemoryNearlyFull, Formatting.Percent(memory.UsedFraction))));

                if (overview.Processes?.MaxBy(p => p.MemoryBytes) is { MemoryBytes: > 0 } top)
                {
                    findings.Add(new Finding(Severity.Info, CheckArea.Processes, Format(Strings.FindingTopMemoryApp, top.Name, Formatting.Bytes(top.MemoryBytes))));
                }
            }
        }

        foreach (var disk in overview.PhysicalDisks ?? [])
        {
            var name = disk.Name ?? Strings.DiskUnnamed;
            switch (disk.Health)
            {
                case DiskHealth.Unhealthy:
                    findings.Add(new Finding(Severity.Critical, CheckArea.Drives, Format(Strings.FindingDiskUnhealthy, name)));
                    break;
                case DiskHealth.Warning:
                    findings.Add(new Finding(Severity.Warning, CheckArea.Drives, Format(Strings.FindingDiskWarning, name)));
                    break;
            }
        }

        foreach (var drive in overview.Drives ?? [])
        {
            if (IsNearlyFull(drive))
            {
                var template = drive.IsSystemDrive ? Strings.FindingSystemDriveNearlyFull : Strings.FindingDriveNearlyFull;
                findings.Add(new Finding(Severity.Warning, CheckArea.Drives, Format(template, drive.Letter, Formatting.Bytes(drive.FreeBytes))));
            }
        }

        // OrderByDescending is stable, so findings keep their order within a severity.
        return findings.OrderByDescending(f => f.Severity).ToList();
    }

    public static bool IsNearlyFull(VolumeInfo drive)
    {
        ArgumentNullException.ThrowIfNull(drive);
        return drive.TotalBytes > 0
            && (drive.UsedFraction >= DriveNearlyFullFraction
                || (drive.IsSystemDrive && drive.FreeBytes < SystemDriveMinFreeBytes));
    }

    private static string Format(string template, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, template, values);
}
