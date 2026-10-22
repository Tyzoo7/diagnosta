namespace Diagnosta.Checks;

internal enum Severity
{
    Info,
    Warning,

    /// <summary>Act now, e.g. a failing disk. Shown in red.</summary>
    Critical,
}

/// <summary>The parts of the PC Diagnosta looks at; each one is also a menu item.</summary>
internal enum CheckArea
{
    Windows,
    Processor,
    Memory,
    Graphics,
    Drives,
    Startup,
    Processes,
}

/// <summary>One plain-language observation about the PC.</summary>
internal sealed record Finding(Severity Severity, CheckArea Area, string Message);
