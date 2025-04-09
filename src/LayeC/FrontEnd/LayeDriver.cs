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
        Options = options;

        Context = new CompilerContext(diagConsumer, Target.X86_64)
        {
            IncludePaths = Options.IncludePaths,
        };
    }

    public int Execute()
    {
        var languageOptions = new LanguageOptions();
        languageOptions.SetDefaults(Context, Options.Standards);

        var debugPrinter = new SyntaxDebugTreePrinter(Options.OutputColoring);
        var ppOutputWriter = new PreprocessedOutputWriter(Console.Out, false);

        foreach (var (fileName, file) in Options.InputFiles)
        {
            var preprocessor = new Preprocessor(Context, languageOptions);

            var source = new SourceText(fileName, File.ReadAllText(file.FullName));
            var sourceLanguage = file.Extension is ".c" or ".h" ? SourceLanguage.C : SourceLanguage.Laye;

            var tokens = preprocessor.PreprocessSource(source, sourceLanguage);
            //foreach (var token in tokens) debugPrinter.PrintToken(token);
            foreach (var token in tokens) ppOutputWriter.WriteToken(token);
        }

        return 0;
    }
}
