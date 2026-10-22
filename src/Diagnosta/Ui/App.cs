using System.Globalization;
using System.Reflection;
using Diagnosta.Checks;
using Diagnosta.Cli;
using Diagnosta.Localization;
using Diagnosta.SystemInfo;
using Spectre.Console;

namespace Diagnosta.Ui;

/// <summary>
/// Turns a parsed command line into console output. Takes <see cref="IAnsiConsole"/>s
/// instead of writing to the real console so tests can capture what is shown.
/// Errors go to a separate console (stderr) so scripts can tell them apart from output.
/// <paramref name="systemInfo"/> is null on platforms Diagnosta does not support.
/// </summary>
internal sealed class App(IAnsiConsole console, IAnsiConsole errorConsole, ISystemInfoSource? systemInfo)
{
    public const int ExitSuccess = 0;
    public const int ExitUsageError = 2;
    public const int ExitUnsupportedPlatform = 3;

    private const string AuthorUrl = "https://github.com/Tyzoo7";

    public static string Version { get; } = ReadVersion();

    public int Run(CliParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (parseResult.Error is { } error)
        {
            ShowError(error);
            return ExitUsageError;
        }

        switch (parseResult.Options!.Command)
        {
            case CliCommand.Help:
                ShowHelp();
                break;
            case CliCommand.Version:
                console.WriteLine($"Diagnosta {Version}");
                break;
            default:
                return ShowOverview();
        }

        return ExitSuccess;
    }

    private int ShowOverview()
    {
        if (systemInfo is null)
        {
            errorConsole.MarkupLine($"[red]{Markup.Escape(Strings.WindowsOnly)}[/]");
            return ExitUnsupportedPlatform;
        }

        console.Write(new FigletText("Diagnosta").Color(Color.DeepSkyBlue1));
        console.MarkupLine($"[bold]{Markup.Escape(Strings.AppTagline)}[/]");
        var author = string.Format(CultureInfo.CurrentCulture, Strings.ByAuthor, $"[link={AuthorUrl}]Tyzoo7[/]");
        console.MarkupLine($"[grey]v{Markup.Escape(Version)} · {author} · {Markup.Escape(Strings.LanguageName)}[/]");
        console.WriteLine();

        var overview = systemInfo.Read();
        SystemOverviewView.Render(console, overview, SystemOverviewAnalyzer.Analyze(overview));

        console.WriteLine();
        console.MarkupLine($"[grey]{Markup.Escape(Strings.HintHelp)}[/]");
        return ExitSuccess;
    }

    private void ShowHelp()
    {
        console.MarkupLine($"[bold]Diagnosta {Markup.Escape(Version)}[/] – {Markup.Escape(Strings.AppTagline)}");
        console.WriteLine();
        console.MarkupLine($"[bold]{Markup.Escape(Strings.HelpUsage)}:[/] diagnosta [[{Markup.Escape(Strings.HelpOptionsPlaceholder)}]]");
        console.WriteLine();

        var options = new Table().NoBorder().HideHeaders().AddColumn(string.Empty).AddColumn(string.Empty);
        options.AddRow("[deepskyblue1]-h, --help[/]", Markup.Escape(Strings.HelpOptionHelp));
        options.AddRow("[deepskyblue1]-v, --version[/]", Markup.Escape(Strings.HelpOptionVersion));
        options.AddRow("[deepskyblue1]--lang <de|en>[/]", Markup.Escape(Strings.HelpOptionLanguage));

        console.MarkupLine($"[bold]{Markup.Escape(Strings.HelpOptions)}:[/]");
        console.Write(options);
    }

    private void ShowError(CliError error)
    {
        var template = error.Kind switch
        {
            CliErrorKind.MissingLanguageValue => Strings.ErrorMissingLanguageValue,
            CliErrorKind.UnsupportedLanguage => Strings.ErrorUnsupportedLanguage,
            _ => Strings.ErrorUnknownArgument,
        };

        // The argument comes from the user: escape it so text like "[red]" is shown
        // literally instead of being interpreted as Spectre.Console markup.
        var message = string.Format(CultureInfo.CurrentCulture, template, error.Argument);
        errorConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        errorConsole.MarkupLine($"[grey]{Markup.Escape(Strings.HintHelp)}[/]");
    }

    private static string ReadVersion()
    {
        var informational = typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        // The SDK appends "+<commit hash>"; users only need the version number.
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}
