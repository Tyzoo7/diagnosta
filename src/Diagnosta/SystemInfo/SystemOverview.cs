namespace Diagnosta.SystemInfo;

// Every value is nullable: if Windows does not tell us something, we show "unknown"
// instead of guessing or crashing.

internal sealed record CpuInfo(string? Name, int? PhysicalCores, int LogicalProcessors);

internal sealed record MemoryInfo(ulong TotalBytes, ulong AvailableBytes)
{
    public ulong UsedBytes => TotalBytes - Math.Min(AvailableBytes, TotalBytes);

    public double UsedFraction => TotalBytes == 0 ? 0 : (double)UsedBytes / TotalBytes;
}

internal sealed record GpuInfo(string Name, ulong? DedicatedMemoryBytes);

internal sealed record WindowsInfo(string Name, string? Edition, string? DisplayVersion, int Build, int? UpdateRevision)
{
    public bool IsWindows10 => Name == WindowsNaming.Windows10;
}

internal sealed record SystemOverview(
    CpuInfo Cpu,
    MemoryInfo? Memory,
    IReadOnlyList<GpuInfo>? Gpus,
    WindowsInfo? Windows,
    TimeSpan Uptime,
    bool? FastStartupEnabled);
