using Diagnosta.Cli;
using Diagnosta.Ui;

namespace Diagnosta.Tests;

public class InteractiveSessionTests
{
    private sealed record Result(int ExitCode, string Output, ScriptedPrompt Prompt, int Clears);

    private static Result Run(string systemCulture, string[] args, params string[] answers)
    {
        using var _ = new CultureScope(systemCulture);
        var writer = new StringWriter();
        var console = TestConsole.Create(writer, width: 140);
        var prompt = new ScriptedPrompt(answers);
        var clears = 0;
        var app = new App(console, console, new FakeSystemInfoSource(FakeSystemInfoSource.HealthyPc), new InteractiveOptions(prompt, () => clears++));

        var exitCode = app.Run(CliParser.Parse(args));
        return new Result(exitCode, writer.ToString(), prompt, clears);
    }

    [Fact]
    public void Start_AsksForLanguage_WithTheWindowsLanguageFirst()
    {
        var result = Run("de-CH", [], "Deutsch", "Beenden");

        var (title, choices) = result.Prompt.Asked[0];
        Assert.Equal("Sprache / Language", title);
        Assert.Equal(["Deutsch", "English"], choices);
        Assert.Equal(App.ExitSuccess, result.ExitCode);
    }

    [Fact]
    public void ChosenLanguage_IsUsedForTheMenu()
    {
        var result = Run("de-DE", [], "English", "Quit");

        var (title, choices) = result.Prompt.Asked[1];
        Assert.Equal("What would you like to do?", title);
        Assert.Contains("Full PC check", choices);
    }

    [Fact]
    public void Menu_OffersEveryCheckAndTheExtras()
    {
        var result = Run("en-US", [], "English", "Quit");

        Assert.Equal(
            ["Full PC check", "Windows", "Processor", "Memory (RAM)", "Graphics", "Drives", "Startup apps", "Running programs", "About Diagnosta", "Change language", "Quit"],
            result.Prompt.Asked[1].Choices);
    }

    [Fact]
    public void SeveralChecks_CanBeRunOneAfterAnother_UntilQuit()
    {
        var result = Run("en-US", [], "English", "Drives", "Processor", "Full PC check", "Quit");

        Assert.Equal(App.ExitSuccess, result.ExitCode);
        Assert.Equal(5, result.Prompt.Asked.Count); // language + 4 menu choices
        Assert.Contains("Samsung SSD 980 PRO 1TB", result.Output, StringComparison.Ordinal);
        Assert.Contains("AMD Ryzen 5 7600X", result.Output, StringComparison.Ordinal);
        Assert.True(result.Clears >= 4, "every new screen starts clean");
    }

    [Fact]
    public void About_ShowsVersionAuthorAndPrivacy()
    {
        var result = Run("en-US", [], "English", "About Diagnosta", "Quit");

        Assert.Contains(App.Version, result.Output, StringComparison.Ordinal);
        Assert.Contains("Tyzoo7", result.Output, StringComparison.Ordinal);
        Assert.Contains("Privacy", result.Output, StringComparison.Ordinal);
        Assert.Contains("Diagnosta only reads information", result.Output, StringComparison.Ordinal);
        Assert.Contains("MIT", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Language_CanBeChangedFromTheMenu()
    {
        var result = Run("en-US", [], "English", "Change language", "Deutsch", "Beenden");

        Assert.Equal("What would you like to do?", result.Prompt.Asked[1].Title);
        Assert.Equal("Sprache / Language", result.Prompt.Asked[2].Title);
        Assert.Equal("Was möchtest du tun?", result.Prompt.Asked[3].Title);
    }

    [Fact]
    public void ExplicitLanguageOption_SkipsTheLanguageQuestion()
    {
        // Program.cs applies --lang before the app starts; "de-DE" stands in for that here.
        var result = Run("de-DE", ["--lang", "de"], "Beenden");

        Assert.Single(result.Prompt.Asked);
        Assert.Equal("Was möchtest du tun?", result.Prompt.Asked[0].Title);
    }

    [Fact]
    public void FullOption_PrintsEverythingWithoutAnyQuestion()
    {
        var result = Run("en-US", ["--full"]);

        Assert.Empty(result.Prompt.Asked);
        Assert.Contains("Drives", result.Output, StringComparison.Ordinal);
        Assert.Contains("Processor", result.Output, StringComparison.Ordinal);
    }
}
