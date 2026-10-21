using System.Globalization;

namespace Diagnosta.Tests;

/// <summary>Switches the UI language for the duration of a test.</summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo previousUi = CultureInfo.CurrentUICulture;
    private readonly CultureInfo previous = CultureInfo.CurrentCulture;

    public CultureScope(string culture)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = previousUi;
        CultureInfo.CurrentCulture = previous;
    }
}
