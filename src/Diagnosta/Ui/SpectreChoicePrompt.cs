using Spectre.Console;

namespace Diagnosta.Ui;

/// <summary>Arrow-key selection list in the terminal.</summary>
internal sealed class SpectreChoicePrompt(IAnsiConsole console) : IChoicePrompt
{
    public T Choose<T>(string title, IReadOnlyList<T> choices, Func<T, string> display)
        where T : notnull =>
        console.Prompt(new SelectionPrompt<T>()
            .Title($"[bold]{Markup.Escape(title)}[/]")
            .PageSize(20)
            .HighlightStyle(new Style(Color.DeepSkyBlue1, decoration: Decoration.Bold))
            .UseConverter(choice => Markup.Escape(display(choice)))
            .AddChoices(choices));
}
