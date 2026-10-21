using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Diagnosta.Localization;

namespace Diagnosta.Tests;

/// <summary>Guards against a text being added in one language but forgotten in the other.</summary>
public partial class LocalizationTests
{
    private static Dictionary<string, string> Load(string culture) =>
        Strings.ResourceManager
            .GetResourceSet(CultureInfo.GetCultureInfo(culture), createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!);

    [Fact]
    public void GermanAndEnglish_HaveTheSameKeys()
    {
        var english = Load(string.Empty); // neutral resources are English
        var german = Load(LanguageResolver.German);

        Assert.NotEmpty(english);
        Assert.Equal(english.Keys.Order(), german.Keys.Order());
    }

    [Fact]
    public void NoTranslation_IsEmpty()
    {
        foreach (var (key, value) in Load(string.Empty).Concat(Load(LanguageResolver.German)))
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"'{key}' is empty");
        }
    }

    [Fact]
    public void Placeholders_MatchBetweenLanguages()
    {
        var english = Load(string.Empty);
        var german = Load(LanguageResolver.German);

        foreach (var (key, value) in english)
        {
            Assert.Equal(Placeholders(value), Placeholders(german[key]));
        }
    }

    private static string[] Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Value).Order().ToArray();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();
}
