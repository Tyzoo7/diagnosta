namespace Diagnosta.Ui;

/// <summary>Lets the user pick one item from a list. Tests replace it with scripted answers.</summary>
internal interface IChoicePrompt
{
    T Choose<T>(string title, IReadOnlyList<T> choices, Func<T, string> display)
        where T : notnull;
}
