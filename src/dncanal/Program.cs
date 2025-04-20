using System.Diagnostics;

using Choir;
using Choir.Diagnostics;
using Choir.Driver;
using Choir.Source;

namespace Canal;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Any(arg => arg == "--help"))
        {
            ShowHelp();
            return 0;
        }

        using var diag = new DiagnosticEngine(new FormattedDiagnosticWriter(Console.Out, true));
        var options = CanalOptions.Parse(diag, new(args));

        if (diag.HasEmittedErrors)
            return 1;

        Debug.Assert(options.CheckFile is not null);
        Debug.Assert(options.CheckFile.Exists);

        var checkSource = new SourceText(options.CheckFile.FullName, options.CheckFile.ReadAllText(), Choir.SourceLanguage.None);
        var context = new CanalContext(diag, options, checkSource);

        if (options.Prefix.IsNullOrWhiteSpace())
        {
            var prefix = ((StringView)checkSource.Text)
                .DropUntil(CanalContext.PrefixDirectiveName)
                .DropUntil(char.IsWhiteSpace)
                .TrimStart()
                .TakeUntil('\n')
                .TrimEnd();

            if (prefix.Length == 0)
            {
                diag.Emit(DiagnosticLevel.Error, $"No prefix provided and not '{CanalContext.PrefixDirectiveName}' directive found in the check file.");
                return 1;
            }

            options.Prefix = (string)prefix;
        }

        Debug.Assert(!options.Prefix.IsNullOrWhiteSpace());
        var prefixState = context.CreatePrefix(options.Prefix);
        context.CollectDirectives(prefixState);
        context.Process();

        return diag.HasEmittedErrors ? 1 : 0;
    }

    public static void ShowHelp()
    {
    }
}

public sealed class CanalOptions
{
    public static CanalOptions Parse(DiagnosticEngine diag, CliArgumentIterator args)
    {
        var options = new CanalOptions();

        bool valuesOnly = false;
        while (args.Shift(out string arg))
        {
            if (!valuesOnly && arg == "--")
            {
                valuesOnly = true;
                continue;
            }

            switch (arg)
            {
                case "--help": break;
                case "--verbose": options.VerboseOutput = true; break;

                case "--prefix":
                {
                    if (!args.Shift(out string? prefix))
                        diag.Emit(DiagnosticLevel.Error, "Argument to '--prefix' is missing; expected 1 value.");
                    else options.Prefix = prefix;
                } break;

                case string when arg.StartsWith("--prefix="):
                {
                    options.Prefix = arg[9..];
                    if (options.Prefix.IsNullOrWhiteSpace())
                        diag.Emit(DiagnosticLevel.Error, "Argument to '--prefix' is missing; expected 1 value.");
                } break;

                case "-l":
                {
                    if (!args.Shift(out string? literal))
                        diag.Emit(DiagnosticLevel.Error, "Argument to '-l' is missing; expected 1 value.");
                    else if (literal.Length != 1)
                        diag.Emit(DiagnosticLevel.Error, "Argument to '-l' must be a single character.");
                    else options.LiteralCharacters.Add(literal[0]);
                } break;

                case "-D":
                {
                    if (!args.Shift(out string? define))
                        diag.Emit(DiagnosticLevel.Error, "Argument to '-D' is missing; expected 1 value.");
                    else if (!define.Contains('='))
                        diag.Emit(DiagnosticLevel.Error, "Argument to '-D' must be a string in the form 'key=value'.");
                    else
                    {
                        int eqIndex = define.IndexOf('=');
                        options.DefinedConstants["%" + define[..eqIndex].Trim()] = define[(eqIndex + 1)..].Trim();
                    }
                } break;

                case string when arg.StartsWith("-D"):
                {
                    string define = arg[2..];
                    if (define.IsNullOrWhiteSpace())
                        diag.Emit(DiagnosticLevel.Error, "Argument to '-D' is missing; expected 1 value.");
                    else if (!define.Contains('='))
                        diag.Emit(DiagnosticLevel.Error, "Argument to '-D' must be a string in the form 'key=value'.");
                    else
                    {
                        int eqIndex = define.IndexOf('=');
                        options.DefinedConstants[define[..eqIndex].Trim()] = define[(eqIndex + 1)..].Trim();
                    }
                } break;

                case "--stdout": options.StandardOutput = true; break;
                case "-a": options.AbortOnFirstFailedCheck = true; break;
                case "--no-builtin": options.NoBuiltIn = true; break;

                case "-p":
                {
                    if (!args.Shift(out string? pragma))
                        diag.Emit(DiagnosticLevel.Error, "Argument to '-p' is missing; expected 1 value.");
                    else
                    {
                        switch (pragma)
                        {
                            case "re": options.ForceRegex = true; break;
                            case "nocap": options.NoCapture = true; break;
                            case "captype": options.CaptureTypes = true; break;
                            default: diag.Emit(DiagnosticLevel.Error, $"Unknown pragma '{pragma}'."); break;
                        }
                    }
                } break;

                default:
                {
                    if (arg.StartsWith('-'))
                    {
                        diag.Emit(DiagnosticLevel.Error, $"Unknown option '{arg}'.");
                        break;
                    }

                    if (options.CheckFile is null)
                        options.CheckFile = new(arg);
                    else diag.Emit(DiagnosticLevel.Error, "Only a single input check file is expected.");
                } break;
            }
        }

        if (options.CheckFile is null)
            diag.Emit(DiagnosticLevel.Error, "No input check file.");
        else if (!options.CheckFile.Exists)
            diag.Emit(DiagnosticLevel.Error, $"Check file '{options.CheckFile.FullName}' does not exist.");

        return options;
    }

    public bool VerboseOutput { get; set; }

    public FileInfo? CheckFile { get; set; }

    public string? Prefix { get; set; }
    public List<char> LiteralCharacters { get; } = [];
    public Dictionary<string, string> DefinedConstants { get; } = [];
    public bool StandardOutput { get; set; }
    public bool AbortOnFirstFailedCheck { get; set; }
    public bool NoBuiltIn { get; set; }

    public bool ForceRegex { get; set; }
    public bool NoCapture { get; set; }
    public bool CaptureTypes { get; set; }
}
