using Diagnosta.SystemInfo;

namespace Diagnosta.Tests;

/// <summary>Pretends to be a PC with exactly the values a test needs.</summary>
internal sealed class FakeSystemInfoSource(SystemOverview overview) : ISystemInfoSource
{
    public const ulong GB = 1024UL * 1024 * 1024;

    public static SystemOverview HealthyPc { get; } = new(
        Cpu: new CpuInfo("AMD Ryzen 5 7600X 6-Core Processor", PhysicalCores: 6, LogicalProcessors: 12, UsagePercent: 12, BaseMhz: 4700),
        Memory: new MemoryInfo(TotalBytes: 32 * GB, AvailableBytes: 20 * GB),
        MemoryModules: new MemoryLayout(
            [
                new MemoryModuleInfo(16 * GB, "DDR5", 6000, "Kingston", "KF560C36-16"),
                new MemoryModuleInfo(16 * GB, "DDR5", 6000, "Kingston", "KF560C36-16"),
            ],
            SlotCount: 4),
        Gpus: [new GpuInfo("NVIDIA GeForce RTX 4070", 12 * GB, 16 * GB, "32.0.15.6094", new DateOnly(2026, 8, 20))],
        Drives:
        [
            new VolumeInfo("C:", null, 931 * GB, 412 * GB, IsSystemDrive: true, DriveKind.Ssd, "NVMe", "Samsung SSD 980 PRO 1TB", DiskNumber: 0),
            new VolumeInfo("D:", "Games", 1863 * GB, 900 * GB, IsSystemDrive: false, DriveKind.Hdd, "SATA", "WDC WD20EZRZ-00Z5HB0", DiskNumber: 1),
        ],
        PhysicalDisks:
        [
            new PhysicalDiskInfo("Samsung SSD 980 PRO 1TB", DriveKind.Ssd, DiskHealth.Healthy, DiskNumber: 0),
            new PhysicalDiskInfo("WDC WD20EZRZ-00Z5HB0", DriveKind.Hdd, DiskHealth.Healthy, DiskNumber: 1),
        ],
        StartupApps:
        [
            new StartupEntry("OneDrive", "OneDrive.exe", StartupScope.CurrentUser, Enabled: true),
            new StartupEntry("SecurityHealth", "SecurityHealthSystray.exe", StartupScope.AllUsers, Enabled: true),
            new StartupEntry("Spotify", "Spotify.exe", StartupScope.CurrentUser, Enabled: false),
        ],
        Processes:
        [
            new ProcessUsage("chrome", 14, 3 * GB, 4.5),
            new ProcessUsage("Discord", 6, GB, 0.8),
            new ProcessUsage("explorer", 1, GB / 4, 0.2),
            new ProcessUsage("System", 1, GB / 8, null),
        ],
        RestartPending: false,
        Windows: new WindowsInfo("Windows 11", "Pro", "24H2", Build: 26100, UpdateRevision: 2314),
        Uptime: TimeSpan.FromHours(5.5),
        FastStartupEnabled: false);

    /// <summary>A PC where Windows answered nothing at all.</summary>
    public static SystemOverview SilentPc { get; } = new(
        Cpu: new CpuInfo(Name: null, PhysicalCores: null, LogicalProcessors: 4),
        Memory: null,
        MemoryModules: null,
        Gpus: null,
        Drives: null,
        PhysicalDisks: null,
        StartupApps: null,
        Processes: null,
        RestartPending: null,
        Windows: null,
        Uptime: TimeSpan.Zero,
        FastStartupEnabled: null);

    public SystemOverview Read() => overview;
}
