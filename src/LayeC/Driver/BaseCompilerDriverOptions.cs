using LayeC.Diagnostics;

namespace LayeC.Driver;

public interface IBaseCompilerDriverOptions
{
    public bool ShowVersion { get; set; }
    public bool ShowHelp { get; set; }
    public bool ShowVerboseOutput { get; set; }
    public bool OutputColoring { get; set; }
}

public record class BaseCompilerDriverParseState
{
    public Trilean OutputColoring { get; set; } = Trilean.Unknown;
}

public abstract class BaseCompilerDriverOptions<TSelf, TParseState>
    : IBaseCompilerDriverOptions
    where TSelf : BaseCompilerDriverOptions<TSelf, TParseState>, new()
    where TParseState : BaseCompilerDriverParseState, new()
{
    /// <summary>
    /// The `--version` flag.
    /// When specified, the driver prints the program version, then exits.
    /// </summary>
    public bool ShowVersion { get; set; }

    /// <summary>
    /// The `--help` flag.
    /// When specified, the driver prints the help text, then exits.
    /// </summary>
    public bool ShowHelp { get; set; }

    /// <summary>
    /// The `--verbose` flag.
    /// Allows emitting verbose information about the compilation to stderr.
    /// </summary>
    public bool ShowVerboseOutput { get; set; }

    /// <summary>
    /// True if the compiler output should be colored.
    /// Determined by the `--color` flag.
    /// </summary>
    public bool OutputColoring { get; set; }

    protected virtual void Validate(DiagnosticEngine diag, TParseState state)
    {
    }

    protected virtual void Finalize(DiagnosticEngine diag, TParseState state)
    {
        Trilean outputColoring = state.OutputColoring & ToolingOptions.OutputColoring;

        if (outputColoring == Trilean.Unknown)
            outputColoring = (Console.IsOutputRedirected || Console.IsErrorRedirected) ? Trilean.False : Trilean.True;

        OutputColoring = outputColoring == Trilean.True;
    }

    protected virtual void HandleValue(string value, DiagnosticEngine diag, CliArgumentIterator args, TParseState state)
    {
        diag.Emit(DiagnosticLevel.Fatal, $"Unhandled positional argument '{value}'. The compiler driver option parsers should always handle these themselves. {GetType().Name} did not.");
    }

    protected virtual void HandleArgument(string arg, DiagnosticEngine diag, CliArgumentIterator args, TParseState state)
    {
        switch (arg)
        {
            default:
            {
                diag.Emit(DiagnosticLevel.Error, $"Unknown argument '{arg}'.");
            } break;

            case "--help": ShowHelp = true; break;
            case "--version": ShowVersion = true; break;
            case "--verbose": ShowVerboseOutput = true; break;

            case "--color":
            {
                if (!args.Shift(out string? color))
                    diag.Emit(DiagnosticLevel.Error, $"Argument to '{arg}' is missing; expected 1 value.");
                else
                {
                    switch (color.ToLower())
                    {
                        default: diag.Emit(DiagnosticLevel.Error, $"Color mode '{color}' not recognized."); break;
                        case "auto": state.OutputColoring = Trilean.Unknown; break;
                        case "always": state.OutputColoring = Trilean.True; break;
                        case "never": state.OutputColoring = Trilean.False; break;
                    }
                }
            } break;
        }
    }

    public static TSelf Parse(DiagnosticEngine diag, CliArgumentIterator args)
    {
        var options = new TSelf();
        var state = new TParseState();

        while (args.Shift(out string arg))
        {
            if (arg.StartsWith('-'))
                options.HandleArgument(arg, diag, args, state);
            else options.HandleValue(arg, diag, args, state);
        }

        options.Validate(diag, state);
        options.Finalize(diag, state);

        return options;
    }
}
