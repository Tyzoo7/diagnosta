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

    public const double DriveNearlyFullFraction = 0.9;

    /// <summary>Below this, Windows updates and temp files start to run out of room.</summary>
    public const ulong SystemDriveMinFreeBytes = 10UL * 1024 * 1024 * 1024;

    /// <summary>Returns only things worth mentioning, warnings first. Empty means "all good".</summary>
    public static IReadOnlyList<Finding> Analyze(SystemOverview overview)
    {
        ArgumentNullException.ThrowIfNull(overview);

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

        if (overview.Memory is { } memory)
        {
            if (memory.TotalBytes > 0 && memory.TotalBytes < LowMemoryBytes)
            {
                findings.Add(new Finding(Severity.Info, CheckArea.Memory, Format(Strings.FindingLowMemory, Formatting.Bytes(memory.TotalBytes))));
            }

            if (memory.UsedFraction >= MemoryNearlyFullFraction)
            {
                findings.Add(new Finding(Severity.Warning, CheckArea.Memory, Format(Strings.FindingMemoryNearlyFull, Formatting.Percent(memory.UsedFraction))));
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
