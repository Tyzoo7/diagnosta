using Diagnosta.Ui;

namespace Diagnosta.Tests;

/// <summary>Answers menu questions from a script instead of the keyboard, and records what was asked.</summary>
internal sealed class ScriptedPrompt(params string[] answers) : IChoicePrompt
{
    private readonly Queue<string> answers = new(answers);

    public List<(string Title, List<string> Choices)> Asked { get; } = [];

    /// <summary>Picks the choice whose displayed text equals the next scripted answer.</summary>
    public T Choose<T>(string title, IReadOnlyList<T> choices, Func<T, string> display)
        where T : notnull
    {
        Asked.Add((title, choices.Select(display).ToList()));
        var answer = answers.Dequeue();
        return choices.Single(c => display(c) == answer);
    }
}
