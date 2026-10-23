using System.Globalization;
using System.Reflection;
using Diagnosta.Checks;
using Diagnosta.Cli;
using Diagnosta.Localization;
using Diagnosta.Report;
using Diagnosta.SystemInfo;
using Spectre.Console;

namespace Diagnosta.Ui;

/// <summary>
/// Turns a parsed command line into console output. Takes <see cref="IAnsiConsole"/>s
/// instead of writing to the real console so tests can capture what is shown.
/// Errors go to a separate console (stderr) so scripts can tell them apart from output.
/// <paramref name="systemInfo"/> is null on platforms Diagnosta does not support.
/// <paramref name="interactive"/> is null when there is no one to answer a menu, e.g. when
/// output is redirected or the run happens in CI.
/// </summary>
internal sealed class App(
    IAnsiConsole console,
    IAnsiConsole errorConsole,
    ISystemInfoSource? systemInfo,
    InteractiveOptions? interactive = null,
    IReportStore? reports = null)
{
    public const int ExitSuccess = 0;
    public const int ExitUsageError = 2;
    public const int ExitUnsupportedPlatform = 3;
    public const int ExitReportFailed = 4;

    private const string AuthorUrl = "https://github.com/Tyzoo7";

    /// <summary>Secondary text, readable on black and on PowerShell's blue background.</summary>
    private const string Quiet = "grey70";

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
                return Start(parseResult.Options);
        }

        return ExitSuccess;
    }

    /// <summary>Big logo, tagline, version and author: the first screen.</summary>
    public void WriteHeader()
    {
        console.Write(new FigletText("Diagnosta").Color(Color.DeepSkyBlue1));
        console.MarkupLine($"[bold]{Markup.Escape(Strings.AppTagline)}[/]");
        console.MarkupLine($"[{Quiet}]{VersionLine()}[/]");
        console.WriteLine();
    }

    /// <summary>One line above each result, so the result itself fits on screen.</summary>
    public void WriteCompactHeader()
    {
        console.Write(new Rule($"[bold deepskyblue1]Diagnosta[/] [{Quiet}]{VersionLine()}[/]").LeftJustified().RuleStyle(Color.Grey));
        console.WriteLine();
    }

    public void WriteMenuHint()
    {
        console.WriteLine();
        console.MarkupLine($"[{Quiet}]{Markup.Escape(Strings.MenuHint)}[/]");
    }

    /// <summary>Reads the PC fresh and shows the given parts of it.</summary>
    public void WriteCheck(IReadOnlyCollection<CheckArea> areas)
    {
        var overview = systemInfo!.Read();
        SystemOverviewView.Render(console, overview, SystemOverviewAnalyzer.Analyze(overview), areas);
    }

    /// <summary>Reads the PC, saves the report and says where it is. Returns the path, or null if saving failed.</summary>
    public string? WriteReport(bool anonymize, DateTimeOffset? now = null)
    {
        var overview = systemInfo!.Read();
        var createdAt = now ?? DateTimeOffset.Now;
        var html = SupportReport.Build(overview, SystemOverviewAnalyzer.Analyze(overview), new ReportOptions(anonymize, createdAt, Version));
        var fileName = string.Create(CultureInfo.InvariantCulture, $"{Strings.ReportFileName}-{createdAt:yyyy-MM-dd_HH-mm-ss}.html");

        try
        {
            var path = (reports ?? new DesktopReportStore()).Save(fileName, html);
            console.MarkupLine($"[green bold]✓[/] {Markup.Escape(string.Format(CultureInfo.CurrentCulture, Strings.ReportSaved, path))}");
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errorConsole.MarkupLine($"[red]{Markup.Escape(string.Format(CultureInfo.CurrentCulture, Strings.ReportSaveFailed, ex.Message))}[/]");
            return null;
        }
    }

    public void OpenReport(string path)
    {
        try
        {
            (reports ?? new DesktopReportStore()).Open(path);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No browser associated with .html: the path printed above is enough.
        }
    }

    public void WriteAbout()
    {
        var grid = new Grid().AddColumn(new GridColumn().NoWrap().PadRight(3)).AddColumn();
        grid.AddRow($"[bold]{Markup.Escape(Strings.AboutVersion)}[/]", Markup.Escape(Version));
        grid.AddEmptyRow();
        grid.AddRow($"[bold]{Markup.Escape(Strings.AboutAuthor)}[/]", $"[link={AuthorUrl}]Tyzoo7[/] [{Quiet}]({AuthorUrl})[/]");
        grid.AddEmptyRow();
        grid.AddRow($"[bold]{Markup.Escape(Strings.AboutPrivacy)}[/]", Markup.Escape(Strings.AboutPrivacyText));
        grid.AddEmptyRow();
        grid.AddRow($"[bold]{Markup.Escape(Strings.AboutLicense)}[/]", "MIT");

        console.Write(new Panel(grid)
        {
            Header = new PanelHeader($"[bold deepskyblue1] {Markup.Escape(Strings.MenuAbout)} [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0, 1, 0),
            Width = Math.Min(console.Profile.Width, 96),
        });
    }

    private int Start(CliOptions options)
    {
        if (systemInfo is null)
        {
            errorConsole.MarkupLine($"[red]{Markup.Escape(Strings.WindowsOnly)}[/]");
            return ExitUnsupportedPlatform;
        }

        if (options.Report)
        {
            return WriteReport(anonymize: true) is null ? ExitReportFailed : ExitSuccess;
        }

        if (interactive is not null && !options.FullCheck)
        {
            // An explicit --lang means the language is already chosen.
            return new InteractiveSession(this, interactive.Prompt, interactive.ClearScreen).Run(askLanguage: options.Language is null);
        }

        WriteHeader();
        WriteCheck(SystemOverviewView.AllAreas);
        console.WriteLine();
        console.MarkupLine($"[{Quiet}]{Markup.Escape(Strings.HintHelp)}[/]");
        return ExitSuccess;
    }

    private static string VersionLine()
    {
        var author = string.Format(CultureInfo.CurrentCulture, Strings.ByAuthor, $"[link={AuthorUrl}]Tyzoo7[/]");
        return $"v{Markup.Escape(Version)} · {author} · {Markup.Escape(Strings.LanguageName)}";
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
        options.AddRow("[deepskyblue1]-f, --full[/]", Markup.Escape(Strings.HelpOptionFull));
        options.AddRow("[deepskyblue1]-r, --report[/]", Markup.Escape(Strings.HelpOptionReport));
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

    private static string ReadVersion() =>
        FormatVersion(typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0");

    /// <summary>
    /// The SDK stores "0.5.0+&lt;full commit hash&gt;". We show "0.5.0 (abc1234)" so two builds
    /// with the same version number can still be told apart.
    /// </summary>
    internal static string FormatVersion(string informationalVersion)
    {
        ArgumentNullException.ThrowIfNull(informationalVersion);

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return informationalVersion;
        }

        var commit = informationalVersion[(plus + 1)..];
        var shortCommit = commit.Length > 7 ? commit[..7] : commit;
        return shortCommit.Length == 0
            ? informationalVersion[..plus]
            : $"{informationalVersion[..plus]} ({shortCommit})";
    }
}
