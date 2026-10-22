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

/// <summary>
/// The laptop battery. <paramref name="Present"/> is false on desktops. Capacities are in mWh.
/// </summary>
internal sealed record BatteryInfo(
    bool Present,
    int? ChargePercent = null,
    bool? PluggedIn = null,
    uint? DesignedCapacity = null,
    uint? FullChargeCapacity = null,
    uint? CycleCount = null,
    string? Chemistry = null)
{
    public static BatteryInfo None { get; } = new(Present: false);

    /// <summary>Capacity left compared to new, 0–1. Brand-new batteries can exceed their design; that counts as 100 %.</summary>
    public double? Health => DesignedCapacity is > 0 && FullChargeCapacity is > 0
        ? Math.Min(1, (double)FullChargeCapacity.Value / DesignedCapacity.Value)
        : null;
}

internal enum NetworkKind
{
    Other,
    Ethernet,
    WiFi,
}

/// <summary>A connected network adapter. The MAC address is deliberately left out: it identifies the device.</summary>
internal sealed record NetworkAdapterInfo(
    string Description,
    NetworkKind Kind,
    long? SpeedBitsPerSecond,
    IReadOnlyList<string> IPv4Addresses,
    string? Gateway,
    IReadOnlyList<string> DnsServers)
{
    /// <summary>169.254.x.x: Windows made up an address because no router (DHCP) answered.</summary>
    public bool HasSelfAssignedAddress => IPv4Addresses.Any(a => a.StartsWith("169.254.", StringComparison.Ordinal));
}

/// <summary>Where an autostart entry comes from.</summary>
internal enum StartupScope
{
    /// <summary>Only for the signed-in user.</summary>
    CurrentUser,

    /// <summary>For everyone who signs in to this PC.</summary>
    AllUsers,
}

/// <summary>A program that starts with Windows, as listed in Task Manager's "Startup apps".</summary>
/// <param name="FileName">Just the program file, e.g. "OneDrive.exe": full paths can contain the user name.</param>
internal sealed record StartupEntry(string Name, string? FileName, StartupScope Scope, bool Enabled);

/// <summary>All running processes of one program added together (e.g. every chrome.exe).</summary>
/// <param name="CpuPercent">Share of the whole CPU over the measuring window, 0–100.</param>
internal sealed record ProcessUsage(string Name, int Instances, ulong MemoryBytes, double? CpuPercent);

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
    IReadOnlyList<StartupEntry>? StartupApps,
    IReadOnlyList<ProcessUsage>? Processes,
    bool? RestartPending,
    BatteryInfo? Battery,
    IReadOnlyList<NetworkAdapterInfo>? Network,
    WindowsInfo? Windows,
    TimeSpan Uptime,
    bool? FastStartupEnabled);
