using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

/// <summary>Pretends to be a PC with exactly the values a test needs.</summary>
internal sealed class FakeSystemInfoSource(SystemOverview overview) : ISystemInfoSource
{
    public const ulong GB = 1024UL * 1024 * 1024;

    public static SystemOverview HealthyPc { get; } = new(
        Cpu: new CpuInfo("AMD Ryzen 5 7600X 6-Core Processor", PhysicalCores: 6, LogicalProcessors: 12),
        Memory: new MemoryInfo(TotalBytes: 32 * GB, AvailableBytes: 20 * GB),
        Gpus: [new GpuInfo("NVIDIA GeForce RTX 4070", 12 * GB)],
        Windows: new WindowsInfo("Windows 11", "Pro", "24H2", Build: 26100, UpdateRevision: 2314),
        Uptime: TimeSpan.FromHours(5.5),
        FastStartupEnabled: false);

    /// <summary>A PC where Windows answered nothing at all.</summary>
    public static SystemOverview SilentPc { get; } = new(
        Cpu: new CpuInfo(Name: null, PhysicalCores: null, LogicalProcessors: 4),
        Memory: null,
        Gpus: null,
        Windows: null,
        Uptime: TimeSpan.Zero,
        FastStartupEnabled: null);

    public SystemOverview Read() => overview;
}
