using System.Globalization;
using System.Reflection;
using Diagnosta.Cli;
using Diagnosta.Localization;
using Spectre.Console;

namespace Diagnosta.Ui;

/// <summary>
/// Turns a parsed command line into console output. Takes <see cref="IAnsiConsole"/>s
/// instead of writing to the real console so tests can capture what is shown.
/// Errors go to a separate console (stderr) so scripts can tell them apart from output.
/// </summary>
internal sealed class App(IAnsiConsole console, IAnsiConsole errorConsole)
{
    public const int ExitSuccess = 0;
    public const int ExitUsageError = 2;

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
                ShowWelcome();
                break;
        }

        return ExitSuccess;
    }

    private void ShowWelcome()
    {
        console.Write(new FigletText("Diagnosta").Color(Color.DeepSkyBlue1));
        console.MarkupLine($"[grey]{Markup.Escape(Strings.AppTagline)}[/]");
        console.WriteLine();

        var info = new Grid().AddColumn().AddColumn();
        info.AddRow($"[bold]{Markup.Escape(Strings.VersionLabel)}[/]", Markup.Escape(Version));
        info.AddRow($"[bold]{Markup.Escape(Strings.LanguageLabel)}[/]", Markup.Escape(Strings.LanguageName));
        console.Write(new Panel(info).RoundedBorder().BorderColor(Color.Grey));

        console.WriteLine();
        console.MarkupLine($"[yellow]{Markup.Escape(Strings.WelcomeComingSoon)}[/]");
        console.MarkupLine($"[grey]{Markup.Escape(Strings.HintHelp)}[/]");
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
