using System.Globalization;
using Diagnosta.Localization;
using Diagnosta.SystemInfo;

namespace Diagnosta.Ui;

/// <summary>
/// Turns system values into the plain-language text shown to the user. Shared by the
/// terminal view and the support report, so both always say exactly the same thing.
/// </summary>
internal static class Describe
{
    public static string JoinDots(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));

    public static string Status(int findingCount) => findingCount switch
    {
        0 => Strings.StatusAllGood,
        1 => Strings.StatusOneFinding,
        _ => Format(Strings.StatusFindings, findingCount),
    };

    /// <summary>"Windows 11 Pro" and "Version 24H2 · Build 26100.2314".</summary>
    public static (string? Name, string? Detail) Windows(WindowsInfo? windows)
    {
        if (windows is null)
        {
            return (null, null);
        }

        var name = string.Join(' ', new[] { windows.Name, windows.Edition }.Where(p => !string.IsNullOrEmpty(p)));
        var build = windows.UpdateRevision is { } ubr
            ? $"{windows.Build}.{ubr}"
            : windows.Build.ToString(CultureInfo.InvariantCulture);
        return (name, JoinDots(
            windows.DisplayVersion is null ? null : Format(Strings.WindowsVersion, windows.DisplayVersion),
            Format(Strings.WindowsBuild, build)));
    }

    public static string Restart(bool pending) => pending ? Strings.RestartPending : Strings.RestartNotNeeded;

    /// <summary>"4 cores · 8 threads · base clock 1.6 GHz".</summary>
    public static string CpuSummary(CpuInfo cpu) => JoinDots(
        cpu.PhysicalCores is { } cores
            ? Format(Strings.CpuCoresThreads, cores, cpu.LogicalProcessors)
            : Format(Strings.CpuThreads, cpu.LogicalProcessors),
        cpu.BaseMhz is { } mhz and > 0 ? Format(Strings.CpuBaseClock, Formatting.Gigahertz(mhz)) : null);

    public static string CpuUsage(double usagePercent) => Format(Strings.CpuUsage, Formatting.Percent(usagePercent / 100));

    public static string MemoryUsage(MemoryInfo memory) => Format(
        Strings.MemoryUsage,
        Formatting.Bytes(memory.UsedBytes),
        Formatting.Bytes(memory.TotalBytes),
        Formatting.Percent(memory.UsedFraction));

    /// <summary>"2 modules: 8 GB + 16 GB · DDR4 · 2667 MT/s".</summary>
    public static string Modules(IReadOnlyList<MemoryModuleInfo> modules)
    {
        var sizes = string.Join(" + ", modules.Select(m => Formatting.Bytes(m.SizeBytes)));
        var count = modules.Count == 1
            ? Format(Strings.MemoryModulesOne, sizes)
            : Format(Strings.MemoryModulesMany, modules.Count, sizes);
        var types = string.Join("/", modules.Select(m => m.Type).OfType<string>().Distinct());
        // All modules run at the speed of the slowest one.
        var speed = modules.Select(m => m.SpeedMts).OfType<int>().DefaultIfEmpty().Min() is var s and > 0
            ? string.Create(CultureInfo.CurrentCulture, $"{s} MT/s")
            : null;
        return JoinDots(count, types.Length == 0 ? null : types, speed);
    }

    public static string Slots(MemoryLayout layout) => Format(Strings.MemorySlots, layout.Modules.Count, layout.SlotCount);

    /// <summary>"Integrated graphics, shares system memory (up to 11.9 GB)" or "Dedicated graphics card with 12 GB …".</summary>
    public static string GpuKind(GpuInfo gpu) => gpu switch
    {
        { IsIntegrated: true, SharedMemoryBytes: { } shared } => Format(Strings.GpuIntegratedUpTo, Formatting.Bytes(shared)),
        { IsIntegrated: true } => Strings.GpuIntegrated,
        _ => Format(Strings.GpuMemory, Formatting.Bytes(gpu.DedicatedMemoryBytes!.Value)),
    };

    public static string? GpuDriver(GpuInfo gpu) => gpu.DriverVersion is not { } version
        ? null
        : gpu.DriverDate is { } date
            ? Format(Strings.GpuDriver, version, date.ToString("d", CultureInfo.CurrentCulture))
            : Format(Strings.GpuDriverVersionOnly, version);

    /// <summary>
    /// "Windows · NVMe SSD · Samsung SSD 980 PRO 1TB". With <paramref name="hideLabel"/> the
    /// user's own drive name (which may contain a person's name) is left out.
    /// </summary>
    public static string DriveTitle(VolumeInfo drive, bool hideLabel = false)
    {
        var kind = drive.Kind switch
        {
            DriveKind.Ssd => "SSD",
            DriveKind.Hdd => Strings.DriveHdd,
            _ => null,
        };
        var type = string.Join(' ', new[] { drive.BusType, kind }.Where(p => p is not null));
        var label = drive.IsSystemDrive ? "Windows" : hideLabel ? null : drive.Label;
        var title = JoinDots(label, type, drive.Model);
        return title.Length == 0 ? Strings.Unknown : title;
    }

    public static string DriveFree(VolumeInfo drive) => Format(
        Strings.DriveFree,
        Formatting.Bytes(drive.FreeBytes),
        Formatting.Bytes(drive.TotalBytes),
        Formatting.Percent(drive.UsedFraction));

    public static string Health(DiskHealth health) => health switch
    {
        DiskHealth.Healthy => Strings.HealthHealthy,
        DiskHealth.Warning => Strings.HealthWarning,
        DiskHealth.Unhealthy => Strings.HealthUnhealthy,
        _ => Strings.Unknown,
    };

    public static string BatteryCharge(BatteryInfo battery) => JoinDots(
        battery.ChargePercent is { } percent ? Formatting.Percent(percent / 100.0) : Strings.Unknown,
        battery.PluggedIn switch
        {
            true => Strings.BatteryPluggedIn,
            false => Strings.BatteryOnBattery,
            null => null,
        });

    public static string BatteryHealth(double health) => Format(Strings.BatteryHealthValue, Formatting.Percent(health));

    /// <summary>"46.0 Wh of originally 50.0 Wh · 120 charge cycles · Li-Ion".</summary>
    public static string BatteryDetails(BatteryInfo battery) => JoinDots(
        battery is { FullChargeCapacity: { } full, DesignedCapacity: { } design }
            ? Format(Strings.BatteryCapacity, Formatting.WattHours(full), Formatting.WattHours(design))
            : null,
        battery.CycleCount is { } count ? Format(Strings.BatteryCycles, count) : null,
        battery.Chemistry);

    public static string NetworkKindName(NetworkKind kind) => kind switch
    {
        NetworkKind.WiFi => Strings.NetworkWiFi,
        NetworkKind.Ethernet => Strings.NetworkEthernet,
        _ => Strings.NetworkOther,
    };

    public static string NetworkStatus(NetworkAdapterInfo adapter) =>
        JoinDots(Strings.NetworkConnected, adapter.SpeedBitsPerSecond is { } speed ? Formatting.BitRate(speed) : null);

    /// <summary>"IP 192.168.1.23 · Router 192.168.1.1 · DNS 192.168.1.1"; anonymised, the PC's own address ends in "xxx".</summary>
    public static string? NetworkAddresses(NetworkAdapterInfo adapter, bool anonymize = false)
    {
        var addresses = anonymize ? adapter.IPv4Addresses.Select(MaskAddress) : adapter.IPv4Addresses;
        var text = JoinDots(
            adapter.IPv4Addresses.Count > 0 ? Format(Strings.NetworkIp, string.Join(", ", addresses)) : null,
            adapter.Gateway is { } gateway ? Format(Strings.NetworkRouter, gateway) : null,
            adapter.DnsServers.Count > 0 ? Format(Strings.NetworkDns, string.Join(", ", adapter.DnsServers)) : null);
        return text.Length == 0 ? null : text;
    }

    /// <summary>192.168.1.23 → 192.168.1.xxx: enough to see the network, not the device.</summary>
    public static string MaskAddress(string address)
    {
        var lastDot = address.LastIndexOf('.');
        return lastDot < 0 ? "xxx" : address[..(lastDot + 1)] + "xxx";
    }

    public static string StartupSummary(IReadOnlyList<StartupEntry> apps)
    {
        var enabled = apps.Count(a => a.Enabled);
        var disabled = apps.Count - enabled;
        return JoinDots(
            Format(enabled == 1 ? Strings.StartupSummaryOne : Strings.StartupSummary, enabled),
            disabled > 0 ? Format(Strings.StartupDisabledCount, disabled) : null);
    }

    public static string StartupDetail(StartupEntry app) => JoinDots(
        app.FileName,
        app.Scope == StartupScope.CurrentUser ? Strings.StartupScopeUser : Strings.StartupScopeAll,
        app.Enabled ? null : Strings.StartupDisabled);

    /// <summary>Below 1 % a rounded "0 %" would look like the program does nothing at all.</summary>
    public static string ProcessCpu(double percent) => percent < 1 ? "< 1 %" : Formatting.Percent(percent / 100);

    private static string Format(string template, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, template, values);
}
