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
                ["doc", .. string[] rest] => throw new NotImplementedException(),
                _ => LayeCDriverCompiler.Create(diagProvider, LayeCDriverCompilerOptions.Parse(diag, new CliArgumentIterator(args)), programName),
            };

            if (diag.HasEmittedErrors)
                return null;

            return driver;
        }
    }
}

public abstract class LayeCSharedDriverOptions<TSelf>
    : BaseCompilerDriverOptions<TSelf, BaseCompilerDriverParseState>
    where TSelf : LayeCSharedDriverOptions<TSelf>, new()
{
}
