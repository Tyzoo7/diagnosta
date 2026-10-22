using System.Globalization;
using System.Text;
using Diagnosta.Cli;
using Diagnosta.Localization;
using Diagnosta.SystemInfo;
using Diagnosta.SystemInfo.Native;
using Diagnosta.Ui;
using Spectre.Console;

// Without UTF-8, umlauts and box-drawing characters break in older Windows consoles.
Console.OutputEncoding = Encoding.UTF8;

var parseResult = CliParser.Parse(args);

var culture = LanguageResolver.Resolve(parseResult.Options?.Language, CultureInfo.CurrentUICulture);
CultureInfo.CurrentUICulture = culture;

var console = AnsiConsole.Console;
var errorConsole = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });

// When output is redirected (e.g. "diagnosta > report.txt"), some platforms report
// a width of -1 and Spectre.Console would then render nothing at all.
foreach (var c in new[] { console, errorConsole })
{
    if (c.Profile.Width <= 0)
    {
        c.Profile.Width = 80;
    }
}

ISystemInfoSource? systemInfo = OperatingSystem.IsWindows() ? new WindowsSystemInfoSource() : null;

// The menu needs someone at the keyboard: not when input or output is redirected,
// and not in CI, where Spectre.Console reports the console as non-interactive.
var canShowMenu = !Console.IsInputRedirected && !Console.IsOutputRedirected && console.Profile.Capabilities.Interactive;
var interactive = canShowMenu ? new InteractiveOptions(new SpectreChoicePrompt(console), console.Clear) : null;

var exitCode = new App(console, errorConsole, systemInfo, interactive).Run(parseResult);

// Without the menu (e.g. "--full" after a double-click) the window would close at once.
var usedMenu = interactive is not null && parseResult.Options is { Command: CliCommand.Run, FullCheck: false } && systemInfo is not null;
if (OperatingSystem.IsWindows() && !usedMenu && !Console.IsInputRedirected && Kernel32.IsAloneInConsoleWindow())
{
    console.WriteLine();
    console.MarkupLine($"[grey]{Markup.Escape(Strings.PressEscToExit)}[/]");
    ExitPrompt.WaitForEscape(() => Console.ReadKey(intercept: true));
}

return exitCode;
