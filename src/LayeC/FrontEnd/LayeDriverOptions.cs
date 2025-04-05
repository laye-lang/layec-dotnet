using LayeC.Diagnostics;
using LayeC.Driver;

namespace LayeC.FrontEnd;

public enum LayeCompilerCommand
{
    Compile,
    Run,
    Format,
    LanguageServer,
}

public sealed class LayeDriverOptions
    : BaseCompilerDriverOptions<LayeDriverOptions, BaseCompilerDriverParseState>
{
    public LayeCompilerCommand Command { get; set; } = LayeCompilerCommand.Compile;

    public List<(string Name, FileInfo File)> InputFiles { get; set; } = [];

    protected override void HandleValue(string value, DiagnosticEngine diag, CliArgumentIterator args, BaseCompilerDriverParseState state)
    {
        var inputFile = new FileInfo(value);
        if (!inputFile.Exists)
            diag.Emit(DiagnosticLevel.Error, $"No such file or directory '{value}'.");

        InputFiles.Add((value, inputFile));
    }

    protected override void HandleArgument(string arg, DiagnosticEngine diag, CliArgumentIterator args, BaseCompilerDriverParseState state)
    {
        switch (arg)
        {
            default: base.HandleArgument(arg, diag, args, state); break;

            case "--run": Command = LayeCompilerCommand.Run; break;
            case "--format": Command = LayeCompilerCommand.Format; break;
            case "--lsp" or "--language-server": Command = LayeCompilerCommand.LanguageServer; break;
        }
    }
}
