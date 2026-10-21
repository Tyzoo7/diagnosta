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

    public static IReadOnlyList<Finding> Analyze(SystemOverview overview)
    {
        ArgumentNullException.ThrowIfNull(overview);

        var findings = new List<Finding>();

        if (overview.Windows?.IsWindows10 == true)
        {
            findings.Add(new Finding(Severity.Warning, Strings.FindingWindows10));
        }

        if (overview.Uptime >= LongUptime)
        {
            findings.Add(new Finding(Severity.Warning, Format(Strings.FindingLongUptime, (int)overview.Uptime.TotalDays)));

            // Explains the classic "but I shut it down every evening!" situation.
            if (overview.FastStartupEnabled == true)
            {
                findings.Add(new Finding(Severity.Info, Strings.FindingFastStartup));
            }
        }

        if (overview.Memory is { } memory)
        {
            if (memory.TotalBytes > 0 && memory.TotalBytes < LowMemoryBytes)
            {
                findings.Add(new Finding(Severity.Info, Format(Strings.FindingLowMemory, Formatting.Bytes(memory.TotalBytes))));
            }

            if (memory.UsedFraction >= MemoryNearlyFullFraction)
            {
                findings.Add(new Finding(Severity.Warning, Format(Strings.FindingMemoryNearlyFull, Formatting.Percent(memory.UsedFraction))));
            }
        }

        if (findings.Count == 0)
        {
            findings.Add(new Finding(Severity.Ok, Strings.FindingAllGood));
        }

        return findings;
    }

    private static string Format(string template, object value) =>
        string.Format(CultureInfo.CurrentCulture, template, value);
}
