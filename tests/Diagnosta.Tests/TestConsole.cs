using Spectre.Console;

namespace Diagnosta.Tests;

/// <summary>A console that renders plain text into a writer, the same on every machine.</summary>
internal static class TestConsole
{
    public static IAnsiConsole Create(TextWriter writer, int width = 120)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
            // Spectre.Console detects CI systems such as GitHub Actions and turns ANSI
            // styling back on, which would put escape codes into the text under test.
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
        });
        console.Profile.Width = width;
        return console;
    }
}
