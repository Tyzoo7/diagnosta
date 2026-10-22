using Diagnosta.Cli;

namespace Diagnosta.Tests;

public class CliParserTests
{
    [Fact]
    public void NoArguments_RunsTheApp()
    {
        var result = CliParser.Parse([]);

        Assert.Null(result.Error);
        Assert.Equal(new CliOptions(CliCommand.Run, null), result.Options);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("/?")]
    public void HelpFlags_ShowHelp(string flag)
    {
        Assert.Equal(CliCommand.Help, CliParser.Parse([flag]).Options?.Command);
    }

    [Theory]
    [InlineData("-v")]
    [InlineData("--version")]
    public void VersionFlags_ShowVersion(string flag)
    {
        Assert.Equal(CliCommand.Version, CliParser.Parse([flag]).Options?.Command);
    }

    [Theory]
    [InlineData("-f")]
    [InlineData("--full")]
    public void FullFlag_RequestsTheFullCheckWithoutMenu(string flag)
    {
        Assert.Equal(new CliOptions(CliCommand.Run, null, FullCheck: true), CliParser.Parse([flag]).Options);
    }

    [Theory]
    [InlineData("--version", "--help")]
    [InlineData("--help", "--version")]
    public void HelpWinsOverVersion_RegardlessOfOrder(string first, string second)
    {
        Assert.Equal(CliCommand.Help, CliParser.Parse([first, second]).Options?.Command);
    }

    [Theory]
    [InlineData(new[] { "--lang", "de" }, "de")]
    [InlineData(new[] { "--lang", "EN" }, "en")]
    [InlineData(new[] { "--lang=de" }, "de")]
    [InlineData(new[] { "--help", "--lang", "de" }, "de")]
    public void LanguageOption_IsParsedAndNormalized(string[] args, string expected)
    {
        Assert.Equal(expected, CliParser.Parse(args).Options?.Language);
    }

    [Theory]
    [InlineData("--lang")]
    [InlineData("--lang=")]
    public void LanguageOption_WithoutValue_IsAnError(string arg)
    {
        Assert.Equal(CliErrorKind.MissingLanguageValue, CliParser.Parse([arg]).Error?.Kind);
    }

    [Fact]
    public void UnsupportedLanguage_IsAnError()
    {
        var result = CliParser.Parse(["--lang", "fr"]);

        Assert.Null(result.Options);
        Assert.Equal(new CliError(CliErrorKind.UnsupportedLanguage, "fr"), result.Error);
    }

    [Fact]
    public void UnknownArgument_IsReportedAsGiven()
    {
        var result = CliParser.Parse(["--bogus"]);

        Assert.Equal(new CliError(CliErrorKind.UnknownArgument, "--bogus"), result.Error);
    }
}
