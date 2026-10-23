using Diagnosta.Localization;

namespace Diagnosta.Cli;

/// <summary>
/// Parses the command line. Deliberately hand-written: the option set is tiny,
/// and avoiding a parsing library keeps the dependency list (and attack surface) small.
/// </summary>
internal static class CliParser
{
    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        var command = CliCommand.Run;
        string? language = null;
        var fullCheck = false;
        var report = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h" or "--help" or "/?":
                    command = CliCommand.Help;
                    break;

                case "-v" or "--version":
                    // Help wins if both are given: it is the more useful answer.
                    if (command != CliCommand.Help)
                    {
                        command = CliCommand.Version;
                    }

                    break;

                case "-f" or "--full":
                    fullCheck = true;
                    break;

                case "-r" or "--report":
                    report = true;
                    break;

                case "--lang":
                    if (i + 1 >= args.Count)
                    {
                        return CliParseResult.Failure(CliErrorKind.MissingLanguageValue, arg);
                    }

                    i++;
                    language = args[i];
                    break;

                default:
                    if (arg.StartsWith("--lang=", StringComparison.Ordinal))
                    {
                        language = arg["--lang=".Length..];
                        break;
                    }

                    return CliParseResult.Failure(CliErrorKind.UnknownArgument, arg);
            }
        }

        if (language is not null)
        {
            if (language.Length == 0)
            {
                return CliParseResult.Failure(CliErrorKind.MissingLanguageValue, "--lang");
            }

            if (!LanguageResolver.IsSupported(language))
            {
                return CliParseResult.Failure(CliErrorKind.UnsupportedLanguage, language);
            }

            language = language.ToLowerInvariant();
        }

        return CliParseResult.Success(new CliOptions(command, language, fullCheck, report));
    }
}
