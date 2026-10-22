using Diagnosta.Cli;
using Diagnosta.SystemInfo;
using Diagnosta.Ui;
using Spectre.Console;

namespace Diagnosta.Tests;

public class AppTests
{
    private sealed class Captured
    {
        public StringWriter Out { get; } = new();

        public StringWriter Error { get; } = new();

        public App CreateApp(ISystemInfoSource? systemInfo) => new(Create(Out), Create(Error), systemInfo);

        private static IAnsiConsole Create(TextWriter writer)
        {
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(writer),
            });
            console.Profile.Width = 120;
            return console;
        }
    }

    private static (int ExitCode, string Out, string Error) Run(string culture, params string[] args) =>
        RunOn(new FakeSystemInfoSource(FakeSystemInfoSource.HealthyPc), culture, args);

    private static (int ExitCode, string Out, string Error) RunOn(ISystemInfoSource? systemInfo, string culture, params string[] args)
    {
        using var _ = new CultureScope(culture);
        var captured = new Captured();
        var exitCode = captured.CreateApp(systemInfo).Run(CliParser.Parse(args));
        return (exitCode, captured.Out.ToString(), captured.Error.ToString());
    }

    [Fact]
    public void Version_PrintsVersionNumber()
    {
        var (exitCode, output, _) = Run("en", "--version");

        Assert.Equal(App.ExitSuccess, exitCode);
        Assert.Equal($"Diagnosta {App.Version}", output.Trim());
        Assert.Matches(@"^\d+\.\d+\.\d+", App.Version);
    }

    [Theory]
    [InlineData("0.5.0+18529c1f00d1e2a3b4c5d6e7f8091a2b3c4d5e6f", "0.5.0 (18529c1)")]
    [InlineData("0.5.0+abc", "0.5.0 (abc)")]
    [InlineData("0.5.0+", "0.5.0")]
    [InlineData("0.5.0", "0.5.0")]
    public void FormatVersion_ShowsShortCommit(string informational, string expected)
    {
        Assert.Equal(expected, App.FormatVersion(informational));
    }

    [Fact]
    public void Help_ListsAllOptions()
    {
        var (exitCode, output, _) = Run("en", "--help");

        Assert.Equal(App.ExitSuccess, exitCode);
        Assert.Contains("--help", output, StringComparison.Ordinal);
        Assert.Contains("--version", output, StringComparison.Ordinal);
        Assert.Contains("--lang", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en", "Usage")]
    [InlineData("de", "Verwendung")]
    public void Help_IsShownInTheChosenLanguage(string culture, string expectedWord)
    {
        var (_, output, _) = Run(culture, "--help");

        Assert.Contains(expectedWord, output, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_ShowsVersionLanguageAndOverview()
    {
        var (exitCode, output, _) = Run("de");

        Assert.Equal(App.ExitSuccess, exitCode);
        Assert.Contains(App.Version, output, StringComparison.Ordinal);
        Assert.Contains("Deutsch", output, StringComparison.Ordinal);
        Assert.Contains("von Tyzoo7", output, StringComparison.Ordinal);
        Assert.Contains("Laufwerke", output, StringComparison.Ordinal);
        Assert.Contains("NVIDIA GeForce RTX 4070", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_OnUnsupportedPlatform_ExplainsAndFails()
    {
        var (exitCode, output, error) = RunOn(null, "en");

        Assert.Equal(App.ExitUnsupportedPlatform, exitCode);
        Assert.Empty(output);
        Assert.Contains("Windows 10 and 11", error, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpAndVersion_WorkWithoutWindows()
    {
        Assert.Equal(App.ExitSuccess, RunOn(null, "en", "--help").ExitCode);
        Assert.Equal(App.ExitSuccess, RunOn(null, "en", "--version").ExitCode);
    }

    [Fact]
    public void InvalidArgument_GoesToErrorOutput_WithUsageExitCode()
    {
        var (exitCode, output, error) = Run("en", "--bogus");

        Assert.Equal(App.ExitUsageError, exitCode);
        Assert.Empty(output);
        Assert.Contains("--bogus", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[red]")]
    [InlineData("[/]")]
    [InlineData("[[")]
    public void UserInput_IsNotInterpretedAsMarkup(string malicious)
    {
        // Unescaped, "[/]" would make Spectre.Console throw and "[red]" would change colors.
        var (exitCode, _, error) = Run("en", malicious);

        Assert.Equal(App.ExitUsageError, exitCode);
        Assert.Contains(malicious, error, StringComparison.Ordinal);
    }
}
