using System.Globalization;
using Diagnosta.Localization;

namespace Diagnosta.Tests;

public class LanguageResolverTests
{
    [Theory]
    [InlineData("de-DE")]
    [InlineData("de-CH")]
    [InlineData("de-AT")]
    public void GermanSystems_GetGerman(string systemCulture)
    {
        var culture = LanguageResolver.Resolve(null, CultureInfo.GetCultureInfo(systemCulture));

        Assert.Equal("de", culture.Name);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("")] // invariant culture
    public void OtherSystems_FallBackToEnglish(string systemCulture)
    {
        var culture = LanguageResolver.Resolve(null, CultureInfo.GetCultureInfo(systemCulture));

        Assert.Equal("en", culture.Name);
    }

    [Fact]
    public void ExplicitChoice_WinsOverSystemLanguage()
    {
        var culture = LanguageResolver.Resolve("en", CultureInfo.GetCultureInfo("de-CH"));

        Assert.Equal("en", culture.Name);
    }
}
