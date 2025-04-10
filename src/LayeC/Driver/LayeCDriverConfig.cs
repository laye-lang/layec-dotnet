using System.Diagnostics;

using LayeC.Diagnostics;

namespace LayeC.Driver;

public sealed class LayeCDriverConfig(string programName, DiagnosticConsumerProvider diagProvider, LayeCDriverConfigOptions options)
    : CompilerDriver(programName, new(diagProvider(options.OutputColoring)))
{
    public LayeCDriverConfigOptions Options { get; } = options;

    public override int ShowHelp()
    {
        Console.Error.Write(
            @$"Gets the value of a local tooling config key, or sets its value.

Usage: {ProgramName} config <key> [value] [options...]

Options:
    --help                   Display this information.
    --version                Display compiler version information.
    --verbose                Emit additional information about the compilation to stderr.
    --color <arg>            Specify how compiler output should be colored.
                             one of: 'auto', 'always', 'never'

    --list                   List available config key names.
"
        );
        return 0;
    }

    public override int Execute()
    {
        if (Options.ShowHelp)
            return ShowHelp();

        if (Options.List)
        {
            Diag.Emit(DiagnosticLevel.Warning, "Gotta figure out how to list all available options.");
            return 0;
        }

        Debug.Assert(Options.ConfigKey is not null);

        if (Options.ConfigValue is string configValue)
        {
            if (!ToolingOptions.SetConfigValue(Options.ConfigKey, configValue))
                Diag.Emit(DiagnosticLevel.Error, $"No such config key '{Options.ConfigKey}'.");
            else ToolingOptions.SaveToFile();
        }
        else
        {
            if (ToolingOptions.TryGetRawKeyValue(Options.ConfigKey, out bool isPresent) is string value)
            {
                if (!isPresent)
                    Console.Write('*');
                Console.WriteLine(value);
            }
            else Diag.Emit(DiagnosticLevel.Error, $"No such config key '{Options.ConfigKey}'.");
        }

        return 0;
    }
}

public sealed class LayeCDriverConfigOptions
    : LayeCSharedDriverOptions<LayeCDriverConfigOptions>
{
    public string? ConfigKey { get; set; }
    public string? ConfigValue { get; set; }

    public bool List { get; set; } = false;

    protected override void Validate(DiagnosticEngine diag, BaseCompilerDriverParseState state)
    {
        base.Validate(diag, state);

        if (ConfigKey is null && !List && !ShowHelp && !ShowVersion)
        {
            diag.Emit(DiagnosticLevel.Error, "A config key is required.");
            diag.Emit(DiagnosticLevel.Note, "Use '--list' to see available configs.");
        }
    }

    protected override void HandleValue(string value, DiagnosticEngine diag, CliArgumentIterator args, BaseCompilerDriverParseState state)
    {
        if (ConfigKey is null)
            ConfigKey = value;
        else if (ConfigValue is null)
            ConfigValue = value;
        else
        {
            // do nothing??
        }
    }

    protected override void HandleArgument(string arg, DiagnosticEngine diag, CliArgumentIterator args, BaseCompilerDriverParseState state)
    {
        switch (arg)
        {
            default: base.HandleArgument(arg, diag, args, state); break;

            case "--list": List = true; break;
        }
    }
}
