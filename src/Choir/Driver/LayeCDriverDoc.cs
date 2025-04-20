﻿using Choir.Diagnostics;

namespace Choir.Driver;

public sealed class LayeCDriverDoc(string programName, DiagnosticConsumerProvider diagProvider, LayeCDriverDocOptions options)
    : CompilerDriver(programName, new(diagProvider(options.OutputColoring)))
{
    public LayeCDriverDocOptions Options { get; } = options;

    public override int ShowHelp()
    {
        return 0;
    }

    public override int Execute()
    {
        return 0;
    }
}

public sealed class LayeCDriverDocOptions
    : LayeCSharedDriverOptions<LayeCDriverDocOptions>
{
    public string? FeatureName { get; set; }

    protected override void Validate(DiagnosticEngine diag, BaseCompilerDriverParseState state)
    {
        base.Validate(diag, state);

        if (FeatureName is null)
            diag.Emit(DiagnosticLevel.Error, "Exactly one feature name is expected.");
    }

    protected override void HandleValue(string value, DiagnosticEngine diag, CliArgumentIterator args, BaseCompilerDriverParseState state)
    {
        if (FeatureName is not null)
            diag.Emit(DiagnosticLevel.Error, "Exactly one feature name is expected.");
        else FeatureName = value;
    }

    protected override void HandleArgument(string arg, DiagnosticEngine diag, CliArgumentIterator args, BaseCompilerDriverParseState state)
    {
        switch (arg)
        {
            default: base.HandleArgument(arg, diag, args, state); break;
        }
    }
}
