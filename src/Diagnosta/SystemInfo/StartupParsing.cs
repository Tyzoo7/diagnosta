namespace Diagnosta.SystemInfo;

/// <summary>Pure helpers for reading autostart entries, kept apart from the registry so they can be tested.</summary>
internal static class StartupParsing
{
    /// <summary>
    /// Task Manager stores its on/off switch under "StartupApproved" as binary data whose
    /// first byte is even (02, 06) for enabled and odd (03) for disabled. No entry means
    /// the user never touched it, so it is enabled.
    /// </summary>
    public static bool IsEnabled(byte[]? approved) =>
        approved is not { Length: > 0 } || (approved[0] & 1) == 0;

    /// <summary>
    /// Extracts the program file from a command line such as
    /// <c>"C:\Program Files\App\app.exe" --minimized</c> → <c>app.exe</c>.
    /// </summary>
    public static string? FileNameFromCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var text = Environment.ExpandEnvironmentVariables(command.Trim());
        string path;
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            path = end > 0 ? text[1..end] : text[1..];
        }
        else
        {
            // Unquoted: up to ".exe" if present (paths may contain spaces), else the first word.
            var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            path = exe >= 0 ? text[..(exe + 4)] : text.Split(' ', 2)[0];
        }

        var name = path.Replace('/', '\\').Split('\\')[^1].Trim();
        return name.Length == 0 ? null : name;
    }
}
