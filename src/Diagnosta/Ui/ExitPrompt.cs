namespace Diagnosta.Ui;

/// <summary>Keeps a double-clicked console window open until the user really wants to leave.</summary>
internal static class ExitPrompt
{
    /// <summary>
    /// Waits for Esc and ignores every other key, so shortcuts like Win+Shift+S for a
    /// screenshot don't close the window. Closing it with the X button always works too.
    /// </summary>
    public static void WaitForEscape(Func<ConsoleKeyInfo> readKey)
    {
        ArgumentNullException.ThrowIfNull(readKey);

        while (readKey().Key != ConsoleKey.Escape)
        {
        }
    }
}
