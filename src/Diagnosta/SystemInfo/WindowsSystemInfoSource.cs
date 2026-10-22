using System.Runtime.Versioning;
using Diagnosta.SystemInfo.Native;
using Microsoft.Win32;

namespace Diagnosta.SystemInfo;

/// <summary>
/// Reads the system overview using only what Windows already provides: the registry,
/// kernel32 and DXGI. No admin rights and no extra packages needed.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsSystemInfoSource : ISystemInfoSource
{
    private const string CpuKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string SessionPowerKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";
    private const string PowerKey = @"SYSTEM\CurrentControlSet\Control\Power";

    /// <summary>Long enough to smooth out spikes, short enough not to make the menu feel slow.</summary>
    private static readonly TimeSpan CpuSample = TimeSpan.FromMilliseconds(500);

    public SystemOverview Read() => new(
        Cpu: new CpuInfo(
            Name: Try(() => (ReadValue(CpuKey, "ProcessorNameString") as string)?.Trim(), null),
            PhysicalCores: Try(Kernel32.CountPhysicalCores, null),
            LogicalProcessors: Environment.ProcessorCount,
            UsagePercent: Try(() => Kernel32.MeasureCpuUsage(CpuSample), null),
            BaseMhz: Try(() => ReadValue(CpuKey, "~MHz") as int?, null)),
        Memory: Try(Kernel32.ReadMemory, null),
        MemoryModules: Try(() => Kernel32.ReadSmbiosTable() is { } table ? SmbiosParser.ParseMemory(table) : null, null),
        Gpus: Try(Dxgi.ReadGpus, null),
        Drives: Try(ReadDrives, null),
        PhysicalDisks: Try(Wmi.ReadPhysicalDisks, null),
        Windows: Try(ReadWindows, null),
        // Counts since the last real boot, including sleep, hibernation and Fast Startup.
        Uptime: TimeSpan.FromMilliseconds(Environment.TickCount64),
        FastStartupEnabled: Try(ReadFastStartup, null));

    private static WindowsInfo ReadWindows()
    {
        var build = Environment.OSVersion.Version.Build;
        return new WindowsInfo(
            Name: WindowsNaming.NameFor(build),
            Edition: WindowsNaming.EditionFrom(ReadValue(CurrentVersionKey, "ProductName") as string),
            DisplayVersion: ReadValue(CurrentVersionKey, "DisplayVersion") as string,
            Build: build,
            UpdateRevision: ReadValue(CurrentVersionKey, "UBR") as int?);
    }

    /// <summary>Internal drives only: USB sticks and network drives would just add noise.</summary>
    private static IReadOnlyList<VolumeInfo> ReadDrives()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        var drives = new List<VolumeInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
            {
                continue;
            }

            var letter = drive.Name.TrimEnd('\\');
            var (kind, busType, model, diskNumber) = Try(() => Storage.Describe(letter), (DriveKind.Unknown, null, null, null));

            drives.Add(new VolumeInfo(
                Letter: letter,
                Label: string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel,
                TotalBytes: (ulong)drive.TotalSize,
                FreeBytes: (ulong)drive.TotalFreeSpace,
                IsSystemDrive: string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase),
                Kind: kind,
                BusType: busType,
                Model: model,
                DiskNumber: diskNumber));
        }

        return drives;
    }

    /// <summary>
    /// Fast Startup ("Schnellstart") is active when it is switched on and hibernation
    /// is available, because it works by hibernating the Windows kernel.
    /// </summary>
    private static bool? ReadFastStartup()
    {
        if (ReadValue(SessionPowerKey, "HiberbootEnabled") is not int hiberboot)
        {
            return null;
        }

        var hibernateAvailable = ReadValue(PowerKey, "HibernateEnabled") is not 0;
        return hiberboot == 1 && hibernateAvailable;
    }

    private static object? ReadValue(string keyPath, string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        return key?.GetValue(name);
    }

    /// <summary>One failing source must never break the whole overview: show "unknown" instead.</summary>
    private static T Try<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
#pragma warning disable CA1031 // Any failure here simply means "unknown".
        catch (Exception)
#pragma warning restore CA1031
        {
            return fallback;
        }
    }
}
