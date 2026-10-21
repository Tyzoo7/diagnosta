namespace Diagnosta.SystemInfo;

/// <summary>
/// Works out the real Windows name. The registry value "ProductName" still says
/// "Windows 10" on Windows 11, so the build number is the only reliable signal.
/// </summary>
internal static class WindowsNaming
{
    public const string Windows10 = "Windows 10";
    public const string Windows11 = "Windows 11";

    /// <summary>First build of Windows 11 (21H2).</summary>
    public const int FirstWindows11Build = 22000;

    public static string NameFor(int build) => build >= FirstWindows11Build ? Windows11 : Windows10;

    /// <summary>Extracts the edition, e.g. "Windows 10 Pro" → "Pro".</summary>
    public static string? EditionFrom(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return null;
        }

        foreach (var prefix in new[] { Windows10, Windows11 })
        {
            if (productName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var edition = productName[prefix.Length..].Trim();
                return edition.Length == 0 ? null : edition;
            }
        }

        return productName.Trim();
    }
}
