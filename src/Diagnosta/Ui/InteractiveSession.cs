using System.Globalization;
using Diagnosta.Checks;
using Diagnosta.Localization;

namespace Diagnosta.Ui;

/// <summary>What the user can pick in the main menu.</summary>
internal enum MenuAction
{
    FullCheck,
    Windows,
    Processor,
    Memory,
    Graphics,
    Drives,
    About,
    ChangeLanguage,
    Quit,
}

/// <summary>
/// The menu shown when Diagnosta runs in an interactive terminal (e.g. after a double-click):
/// pick a language, then pick checks with the arrow keys until "Quit".
/// Each result stays on screen with the menu right below it.
/// </summary>
internal sealed class InteractiveSession(App app, IChoicePrompt prompt, Action clearScreen)
{
    private static readonly MenuAction[] MenuActions = Enum.GetValues<MenuAction>();

    public int Run(bool askLanguage)
    {
        clearScreen();
        if (askLanguage)
        {
            ChooseLanguage();
            clearScreen();
        }

        app.WriteHeader();

        while (true)
        {
            app.WriteMenuHint();
            var action = prompt.Choose(Strings.MenuTitle, MenuActions, Label);
            clearScreen();

            switch (action)
            {
                case MenuAction.Quit:
                    return App.ExitSuccess;

                case MenuAction.ChangeLanguage:
                    ChooseLanguage();
                    clearScreen();
                    app.WriteHeader();
                    break;

                case MenuAction.About:
                    app.WriteCompactHeader();
                    app.WriteAbout();
                    break;

                default:
                    app.WriteCompactHeader();
                    app.WriteCheck(AreasFor(action));
                    break;
            }
        }
    }

    internal static IReadOnlyCollection<CheckArea> AreasFor(MenuAction action) => action switch
    {
        MenuAction.Windows => [CheckArea.Windows],
        MenuAction.Processor => [CheckArea.Processor],
        MenuAction.Memory => [CheckArea.Memory],
        MenuAction.Graphics => [CheckArea.Graphics],
        MenuAction.Drives => [CheckArea.Drives],
        _ => SystemOverviewView.AllAreas,
    };

    private void ChooseLanguage()
    {
        // The current (Windows) language comes first, so Enter keeps it.
        var current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var languages = LanguageResolver.SupportedLanguages.OrderBy(l => l == current ? 0 : 1).ToList();

        var chosen = prompt.Choose("Sprache / Language", languages, l => l == LanguageResolver.German ? "Deutsch" : "English");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(chosen);
    }

    private static string Label(MenuAction action) => action switch
    {
        MenuAction.FullCheck => Strings.MenuFullCheck,
        MenuAction.Windows => Strings.LabelWindows,
        MenuAction.Processor => Strings.LabelCpu,
        MenuAction.Memory => Strings.LabelMemory,
        MenuAction.Graphics => Strings.LabelGpu,
        MenuAction.Drives => Strings.SectionDrives,
        MenuAction.About => Strings.MenuAbout,
        MenuAction.ChangeLanguage => Strings.MenuChangeLanguage,
        _ => Strings.MenuQuit,
    };
}
