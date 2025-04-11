using LayeC.Diagnostics;

namespace LayeC.Driver;

public sealed class LayeCDriver
{
    public static int RunWithArgs(DiagnosticConsumerProvider diagProvider, string[] args, string programName = "dnlayec")
    {
        using var driver = CreateDriver();
        return driver?.Execute() ?? 1;

        CompilerDriver? CreateDriver()
        {
            using var parserDiag = diagProvider(ToolingOptions.OutputColoring != Trilean.False);
            using var diag = new DiagnosticEngine(parserDiag);

            CompilerDriver driver = args switch
            {
                ["config", .. string[] rest] => new LayeCDriverConfig(programName, diagProvider, LayeCDriverConfigOptions.Parse(diag, new CliArgumentIterator(rest))),
                ["doc", .. string[] rest] => new LayeCDriverDoc(programName, diagProvider, LayeCDriverDocOptions.Parse(diag, new CliArgumentIterator(rest))),
                _ => LayeCDriverCompiler.Create(diagProvider, LayeCDriverCompilerOptions.Parse(diag, new CliArgumentIterator(args)), programName),
            };

            if (diag.HasEmittedErrors)
                return null;

            return driver;
        }
    }
}

public abstract class LayeCSharedDriverOptions<TSelf, TParseState>
    : BaseCompilerDriverOptions<TSelf, TParseState>
    where TSelf : LayeCSharedDriverOptions<TSelf, TParseState>, new()
    where TParseState : BaseCompilerDriverParseState, new()
{
}

public abstract class LayeCSharedDriverOptions<TSelf>
    : BaseCompilerDriverOptions<TSelf, BaseCompilerDriverParseState>
    where TSelf : LayeCSharedDriverOptions<TSelf>, new()
{
}
