namespace Diagnosta.Ui;

/// <summary>What the menu needs: a way to ask the user and a way to clear the screen.</summary>
internal sealed record InteractiveOptions(IChoicePrompt Prompt, Action ClearScreen);
