using System.Globalization;
using Diagnosta.Localization;

namespace Diagnosta.Ui;

/// <summary>Formats values the way Windows shows them (1 GB = 1024³ bytes, like Task Manager).</summary>
internal static class Formatting
{
    private const double Gigabyte = 1024d * 1024 * 1024;
    private const double Megabyte = 1024d * 1024;

    public static string Bytes(ulong bytes) =>
        bytes >= Gigabyte
            ? string.Format(CultureInfo.CurrentCulture, "{0:0.#} GB", bytes / Gigabyte)
            : string.Format(CultureInfo.CurrentCulture, "{0:0} MB", bytes / Megabyte);

    /// <summary>
    /// 1608 MHz → "1.6 GHz" (or "1,6 GHz" in German). Windows measures the clock at boot and
    /// stores it slightly off, so one decimal is all that is honest.
    /// </summary>
    public static string Gigahertz(int megahertz) =>
        string.Format(CultureInfo.CurrentCulture, "{0:0.0} GHz", megahertz / 1000.0);

    /// <summary>866 000 000 → "866 Mbit/s", 1 000 000 000 → "1 Gbit/s" (network speeds use powers of 1000).</summary>
    public static string BitRate(long bitsPerSecond) =>
        bitsPerSecond >= 1_000_000_000
            ? string.Format(CultureInfo.CurrentCulture, "{0:0.#} Gbit/s", bitsPerSecond / 1e9)
            : string.Format(CultureInfo.CurrentCulture, "{0:0} Mbit/s", bitsPerSecond / 1e6);

    /// <summary>39 200 mWh → "39.2 Wh".</summary>
    public static string WattHours(uint milliwattHours) =>
        string.Format(CultureInfo.CurrentCulture, "{0:0.0} Wh", milliwattHours / 1000.0);

    public static string Percent(double fraction) =>
        string.Format(CultureInfo.CurrentCulture, "{0:0} %", fraction * 100);

    public static string Uptime(TimeSpan uptime)
    {
        if (uptime < TimeSpan.Zero)
        {
            uptime = TimeSpan.Zero;
        }

        var days = (int)uptime.TotalDays;
        var template = days switch
        {
            0 => Strings.UptimeHoursMinutes,
            1 => Strings.UptimeOneDay,
            _ => Strings.UptimeDays,
        };

        return days == 0
            ? string.Format(CultureInfo.CurrentCulture, template, uptime.Hours, uptime.Minutes)
            : string.Format(CultureInfo.CurrentCulture, template, days, uptime.Hours);
    }
}
