using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Diagnosta.SystemInfo;

/// <summary>
/// Finds a graphics card's driver version and date. Windows keeps one registry key per
/// installed display driver under the "Display adapters" device class; we pick the one
/// whose hardware ID matches the card DXGI reported (vendor and device ID).
/// </summary>
internal static class GpuDrivers
{
    private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    [SupportedOSPlatform("windows")]
    public static (string? Version, DateOnly? Date) Find(uint vendorId, uint deviceId)
    {
        var hardwareId = string.Create(CultureInfo.InvariantCulture, $"ven_{vendorId:x4}&dev_{deviceId:x4}");

        using var displayClass = Registry.LocalMachine.OpenSubKey(DisplayClassKey);
        if (displayClass is null)
        {
            return (null, null);
        }

        foreach (var name in displayClass.GetSubKeyNames())
        {
            // Driver keys are numbered 0000, 0001, …; "Properties" is not readable anyway.
            if (name.Length != 4 || !name.All(char.IsAsciiDigit))
            {
                continue;
            }

            try
            {
                using var driver = displayClass.OpenSubKey(name);
                if (driver?.GetValue("MatchingDeviceId") is string matching
                    && matching.Contains(hardwareId, StringComparison.OrdinalIgnoreCase))
                {
                    return (driver.GetValue("DriverVersion") as string, ParseDriverDate(driver.GetValue("DriverDate") as string));
                }
            }
            catch (System.Security.SecurityException)
            {
                // Some keys are not readable without admin rights; skip them.
            }
        }

        return (null, null);
    }

    /// <summary>The registry stores the driver date as "M-D-YYYY", e.g. "5-12-2023".</summary>
    internal static DateOnly? ParseDriverDate(string? value) =>
        DateOnly.TryParseExact(value, ["M-d-yyyy", "MM-dd-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
