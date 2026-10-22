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

var exitCode = new App(console, errorConsole, systemInfo).Run(parseResult);

// Started by double-click: keep the window open, or the user never sees the result.
if (OperatingSystem.IsWindows() && !Console.IsInputRedirected && Kernel32.IsAloneInConsoleWindow())
{
    console.WriteLine();
    console.MarkupLine($"[grey]{Markup.Escape(Strings.PressAnyKeyToExit)}[/]");
    Console.ReadKey(intercept: true);
}

return exitCode;
