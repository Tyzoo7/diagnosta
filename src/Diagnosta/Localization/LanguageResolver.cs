using System.Globalization;

namespace Diagnosta.Localization;

/// <summary>Decides which UI language to use.</summary>
internal static class LanguageResolver
{
    public const string English = "en";
    public const string German = "de";

    public static IReadOnlyList<string> SupportedLanguages { get; } = [English, German];

    public static bool IsSupported(string languageCode) =>
        SupportedLanguages.Contains(languageCode, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// An explicit choice (<c>--lang</c>) always wins. Otherwise German systems
    /// (de-DE, de-CH, de-AT, …) get German and everything else falls back to English.
    /// </summary>
    public static CultureInfo Resolve(string? requestedLanguage, CultureInfo systemCulture)
    {
        ArgumentNullException.ThrowIfNull(systemCulture);

        if (requestedLanguage is not null && IsSupported(requestedLanguage))
        {
            return CultureInfo.GetCultureInfo(requestedLanguage.ToLowerInvariant());
        }

        return systemCulture.TwoLetterISOLanguageName == German
            ? CultureInfo.GetCultureInfo(German)
            : CultureInfo.GetCultureInfo(English);
    }
}
