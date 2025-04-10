using LayeC.Diagnostics;
using LayeC.Source;

namespace LayeC.Driver;

public sealed class CDriver
    : CompilerDriverWithContext
{
    public static int RunWithArgs(DiagnosticConsumerProvider diagProvider, string[] args, string programName = "dncc")
    {
        using var parserDiag = diagProvider(ToolingOptions.OutputColoring != Trilean.False);
        using var diag = new DiagnosticEngine(parserDiag);
        using var driver = Create(diagProvider, CDriverOptions.Parse(diag, new CliArgumentIterator(args)), programName);
        return driver.Execute();
    }

    public static CDriver Create(DiagnosticConsumerProvider diagProvider, CDriverOptions options, string programName = "dncc")
    {
        var context = new CompilerContext(diagProvider(options.OutputColoring), Target.X86_64)
        {
            IncludePaths = options.IncludePaths,
        };

        return new CDriver(programName, context, options);
    }

    public CDriverOptions Options { get; set; }

    private CDriver(string programName, CompilerContext context, CDriverOptions options)
        : base(programName, context)
    {
        Options = options;
    }

    public override int ShowHelp()
    {
        return 0;
    }

    public override int Execute()
    {
        foreach (var (fileName, file) in Options.InputFiles)
        {
            var source = new SourceText(fileName, File.ReadAllText(file.FullName));
        }

        return 0;
    }
}
