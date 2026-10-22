namespace Diagnosta.Checks;

internal enum Severity
{
    Info,
    Warning,

    /// <summary>Act now, e.g. a failing disk. Shown in red.</summary>
    Critical,
}

/// <summary>One plain-language observation about the PC.</summary>
internal sealed record Finding(Severity Severity, string Message);
