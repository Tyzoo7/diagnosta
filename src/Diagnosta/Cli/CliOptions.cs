namespace Diagnosta.Cli;

/// <summary>What the user asked the program to do.</summary>
internal enum CliCommand
{
    Run,
    Help,
    Version,
}

/// <summary>Why the command line could not be understood.</summary>
internal enum CliErrorKind
{
    UnknownArgument,
    MissingLanguageValue,
    UnsupportedLanguage,
}

internal sealed record CliOptions(CliCommand Command, string? Language);

internal sealed record CliError(CliErrorKind Kind, string Argument);

/// <summary>Either valid <see cref="CliOptions"/> or a <see cref="CliError"/>, never both.</summary>
internal sealed record CliParseResult
{
    private CliParseResult(CliOptions? options, CliError? error)
    {
        Options = options;
        Error = error;
    }

    public CliOptions? Options { get; }

    public CliError? Error { get; }

    public static CliParseResult Success(CliOptions options) => new(options, null);

    public static CliParseResult Failure(CliErrorKind kind, string argument) => new(null, new CliError(kind, argument));
}
