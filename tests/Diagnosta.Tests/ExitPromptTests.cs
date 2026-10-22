using Diagnosta.Ui;

namespace Diagnosta.Tests;

public class ExitPromptTests
{
    private static ConsoleKeyInfo Key(ConsoleKey key, char c = '\0', bool shift = false) =>
        new(c, key, shift, alt: false, control: false);

    [Fact]
    public void ScreenshotShortcutAndOtherKeys_DoNotClose_OnlyEscDoes()
    {
        var pressed = new Queue<ConsoleKeyInfo>([
            Key(ConsoleKey.S, 'S', shift: true), // what Win+Shift+S lets through
            Key(ConsoleKey.PrintScreen),
            Key(ConsoleKey.Enter, '\r'),
            Key(ConsoleKey.Spacebar, ' '),
            Key(ConsoleKey.Escape),
        ]);

        ExitPrompt.WaitForEscape(pressed.Dequeue);

        Assert.Empty(pressed); // every key up to and including Esc was read, then it stopped
    }

    [Fact]
    public void Esc_ClosesImmediately()
    {
        var reads = 0;

        ExitPrompt.WaitForEscape(() =>
        {
            reads++;
            return Key(ConsoleKey.Escape);
        });

        Assert.Equal(1, reads);
    }
}
