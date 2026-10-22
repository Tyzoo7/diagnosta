namespace Diagnosta.SystemInfo;

// Every value is nullable: if Windows does not tell us something, we show "unknown"
// instead of guessing or crashing.

/// <param name="UsagePercent">Average load over a short sample, 0–100.</param>
/// <param name="BaseMhz">Base clock. The turbo maximum is not reliably available from Windows.</param>
internal sealed record CpuInfo(string? Name, int? PhysicalCores, int LogicalProcessors, double? UsagePercent = null, int? BaseMhz = null);

/// <summary>One installed RAM module. Its serial number is deliberately never read.</summary>
internal sealed record MemoryModuleInfo(ulong SizeBytes, string? Type, int? SpeedMts, string? Manufacturer, string? PartNumber);

/// <summary>Installed modules and how many slots the mainboard has (soldered memory counts as a slot).</summary>
internal sealed record MemoryLayout(IReadOnlyList<MemoryModuleInfo> Modules, int SlotCount)
{
    public int FreeSlots => Math.Max(0, SlotCount - Modules.Count);
}

internal sealed record MemoryInfo(ulong TotalBytes, ulong AvailableBytes)
{
    public ulong UsedBytes => TotalBytes - Math.Min(AvailableBytes, TotalBytes);

    public double UsedFraction => TotalBytes == 0 ? 0 : (double)UsedBytes / TotalBytes;
}

/// <param name="SharedMemoryBytes">How much normal RAM the GPU may borrow.</param>
internal sealed record GpuInfo(
    string Name,
    ulong? DedicatedMemoryBytes,
    ulong? SharedMemoryBytes = null,
    string? DriverVersion = null,
    DateOnly? DriverDate = null)
{
    /// <summary>
    /// Built-in graphics only reserves a small slice of memory (typically 128–512 MB)
    /// and borrows the rest from RAM. Dedicated cards have at least 2 GB nowadays.
    /// </summary>
    public const ulong IntegratedThresholdBytes = 1024UL * 1024 * 1024;

    public bool IsIntegrated => DedicatedMemoryBytes is null || DedicatedMemoryBytes < IntegratedThresholdBytes;
}

internal enum DriveKind
{
    Unknown,
    Ssd,
    Hdd,
}

/// <summary>What Windows' storage service says about a disk (MSFT_PhysicalDisk.HealthStatus).</summary>
internal enum DiskHealth
{
    Unknown,
    Healthy,

    /// <summary>"May be failing some reads" according to Microsoft: back up soon.</summary>
    Warning,

    /// <summary>"Failing reads and writes or no longer responding": back up now.</summary>
    Unhealthy,
}

/// <summary>
/// A physical disk (not a drive letter), e.g. the SSD itself. <paramref name="DiskNumber"/>
/// is Windows' disk number ("Disk 0" in Disk Management) and links it to its drive letters.
/// </summary>
internal sealed record PhysicalDiskInfo(string? Name, DriveKind Kind, DiskHealth Health, int? DiskNumber = null);

/// <summary>A drive letter such as C:. The serial number is deliberately never read (privacy).</summary>
internal sealed record VolumeInfo(
    string Letter,
    string? Label,
    ulong TotalBytes,
    ulong FreeBytes,
    bool IsSystemDrive,
    DriveKind Kind,
    string? BusType,
    string? Model,
    int? DiskNumber = null)
{
    public double UsedFraction => TotalBytes == 0 ? 0 : (double)(TotalBytes - Math.Min(FreeBytes, TotalBytes)) / TotalBytes;
}

internal sealed record WindowsInfo(string Name, string? Edition, string? DisplayVersion, int Build, int? UpdateRevision)
{
    public bool IsWindows10 => Name == WindowsNaming.Windows10;
}

internal sealed record SystemOverview(
    CpuInfo Cpu,
    MemoryInfo? Memory,
    MemoryLayout? MemoryModules,
    IReadOnlyList<GpuInfo>? Gpus,
    IReadOnlyList<VolumeInfo>? Drives,
    IReadOnlyList<PhysicalDiskInfo>? PhysicalDisks,
    WindowsInfo? Windows,
    TimeSpan Uptime,
    bool? FastStartupEnabled);
