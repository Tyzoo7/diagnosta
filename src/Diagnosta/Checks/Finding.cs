namespace Diagnosta.Checks;

internal enum Severity
{
    Ok,
    Info,
    Warning,
}

/// <summary>One plain-language observation about the PC.</summary>
internal sealed record Finding(Severity Severity, string Message);
