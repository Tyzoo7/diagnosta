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
