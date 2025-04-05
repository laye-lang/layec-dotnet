using LayeC.Diagnostics;
using LayeC.Driver;
using LayeC.Source;

namespace LayeC.FrontEnd;

public sealed class LayeDriver
    : ICompilerDriver
{
    public static int RunWithArgs(DiagnosticConsumerProvider diagProvider, string[] args, string programName = "layec")
    {
        LayeDriverOptions options;
        using (var parserDiag = diagProvider(true))
        using (var diag = new DiagnosticEngine(parserDiag))
        {
            options = LayeDriverOptions.Parse(diag, new(args));
            if (diag.HasEmittedErrors)
                return 1;
        }

        if (options.ShowVersion)
        {
            Console.WriteLine("TODO: Show Laye compiler version text");
            return 0;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine("TODO: Show Laye compiler help text");
            return 0;
        }

        using var driverDiag = diagProvider(options.OutputColoring);
        var driver = Create(driverDiag, options, programName);
        return driver.Execute();
    }

    public static LayeDriver Create(IDiagnosticConsumer diagConsumer, LayeDriverOptions options, string programName = "chc")
    {
        return new LayeDriver(programName, diagConsumer, options);
    }

    public string ProgramName { get; set; }
    public CompilerContext Context { get; set; }
    public LayeDriverOptions Options { get; set; }

    private LayeDriver(string programName, IDiagnosticConsumer diagConsumer, LayeDriverOptions options)
    {
        ProgramName = programName;
        Context = new CompilerContext(diagConsumer, Target.X86_64);
        Options = options;
    }

    public int Execute()
    {
        var sema = new Sema(Context);
        foreach (var (fileName, file) in Options.InputFiles)
        {
            var source = new SourceText(fileName, File.ReadAllText(file.FullName));
            var parser = new Parser(Context, sema, source, SourceLanguage.Laye);
            var node1 = parser.ParseTopLevelSyntax();
            var node2 = parser.ParseTopLevelSyntax();
        }

        return 0;
    }
}
