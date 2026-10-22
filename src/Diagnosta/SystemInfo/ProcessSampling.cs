namespace Diagnosta.SystemInfo;

/// <summary>One process at one moment: its memory and how much CPU time it has used so far.</summary>
internal sealed record ProcessSnapshot(int Id, string Name, long MemoryBytes, TimeSpan? CpuTime);

/// <summary>
/// Turns two snapshots taken a moment apart into per-program usage. Pure, so it is tested
/// without real processes.
/// </summary>
internal static class ProcessSampling
{
    /// <param name="before">Processes at the start of the window.</param>
    /// <param name="after">Processes at the end; memory is taken from here.</param>
    /// <param name="elapsed">Length of the window.</param>
    /// <param name="processorCount">CPU time can run on every core at once, so 100 % = all cores busy.</param>
    public static IReadOnlyList<ProcessUsage> Combine(
        IReadOnlyCollection<ProcessSnapshot> before,
        IReadOnlyCollection<ProcessSnapshot> after,
        TimeSpan elapsed,
        int processorCount)
    {
        var start = before.Where(p => p.CpuTime is not null).ToDictionary(p => (p.Id, p.Name), p => p.CpuTime!.Value);
        var capacity = elapsed.TotalMilliseconds * Math.Max(1, processorCount);

        return after
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                double? cpu = null;
                foreach (var process in group)
                {
                    // Processes that started during the window, or whose CPU time Windows
                    // won't tell us (protected system processes), count as unknown.
                    if (process.CpuTime is { } end && start.TryGetValue((process.Id, process.Name), out var begin) && capacity > 0)
                    {
                        cpu = (cpu ?? 0) + Math.Max(0, (end - begin).TotalMilliseconds) / capacity * 100;
                    }
                }

                return new ProcessUsage(group.First().Name, group.Count(), (ulong)Math.Max(0, group.Sum(p => p.MemoryBytes)), cpu is null ? null : Math.Min(100, cpu.Value));
            })
            .ToList();
    }

    /// <summary>CPU load in % from two GetSystemTimes readings (kernel time includes idle time).</summary>
    public static double? CpuUsage((long Idle, long Kernel, long User) before, (long Idle, long Kernel, long User) after)
    {
        var total = (after.Kernel - before.Kernel) + (after.User - before.User);
        var idle = after.Idle - before.Idle;
        return total <= 0 ? null : Math.Clamp(100.0 * (total - idle) / total, 0, 100);
    }
}
