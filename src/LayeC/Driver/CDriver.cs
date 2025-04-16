using System.Runtime.InteropServices;

using LayeC.Diagnostics;
using LayeC.FrontEnd;

namespace LayeC.Driver;

public sealed class CDriver
    : CompilerDriverWithContext
{
    public static int RunWithArgs(DiagnosticConsumerProvider diagProvider, string[] args, string programName = "dncc")
    {
        using var driver = CreateDriver();
        return driver?.Execute() ?? 1;

        CompilerDriver? CreateDriver()
        {
            using var parserDiag = diagProvider(ToolingOptions.OutputColoring != Trilean.False);
            using var diag = new DiagnosticEngine(parserDiag);

            var driver = Create(diagProvider, CDriverOptions.Parse(diag, new CliArgumentIterator(args)), programName);

            if (diag.HasEmittedErrors)
                return null;

            return driver;
        }
    }

    public static CDriver Create(DiagnosticConsumerProvider diagProvider, CDriverOptions options, string programName = "dncc")
    {
        var context = new CompilerContext(diagProvider(options.OutputColoring), Target.X86_64, options.Triple)
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
        Console.Error.Write(
            @$"Compiles a list of Laye source files of the same module into a module object file

Usage: {ProgramName} [command] [options] file...

Options:
    --help                   Display this information.
    --version                Display compiler version information.
    --verbose                Emit additional information about the compilation to stderr.
    --color <arg>            Specify how compiler output should be colored.
                             one of: 'auto', 'always', 'never'

    -i                       Read source text from stdin rather than a list of source files.
    --kind <kind>, -x <kind>
                             Specify the ""kind"" of subsequent input files, or 'default'
                             to infer it from the extension.
                             one of: 'default', 'c'
    -o <path>                Override the output module object file path.
                             To emit output to stdout, specify a path of '-'.
                             default: '<module-name>.mod'
    --emit <flavor>, --emit=<flavor>
                             Emit a specific ""flavor"" of assembler when compiling with '--compile'.
                             When not provided, a default is chosen that seems suitable.
                             Behaves the same as '-emit-<flavor>', which are provided to feel more
                             familiar to GCC/Clang options.
                             one of: 'gas', 'nasm', 'fasm', 'qbe', 'llvm'
    -emit-gas                Emit GNU-flavored Assembler when compiling with '--compile'.
    -emit-nasm               Emit NASM-flavored Assembler when compiling with '--compile'.
    -emit-fasm               Emit FASM-flavored Assembler when compiling with '--compile'.
    -emit-qbe                Emit QBE instead of Assembler when compiling with '--compile'.
    -emit-llvm               Emit LLVM instead of Assembler when compiling with '--compile'.

    -iquote <dir>            Adds <dir> to the end of the list of QUOTE include search paths.
    -I <dir>, -I<dir>        Adds <dir> to the end of the list of include search paths.
    -L <lib-dir>             Adds <dir> to the end of the list of library search paths.
                             Directories are searched in the order they are provided, and values
                             provided through the CLI are searched after built-in and environment-
                             specified search paths.

    --lex                    Only lex the source files, without preprocessing, then exit.
    --preprocess, -E         Only lex and preprocess the source files, then exit.
                             Can be used with '--include-comments' to keep comments in the output.
    --parse, -fsyntax-only   Only lex and parse the source files, then exit.
                             For C source text, this also implies '--sema'.
    --sema                   Only lex, parse and analyse the source files, then exit.
    --codegen                Only lex, parse, analyse and generate code for the source files, then exit.
    --compile, -S            Only lex, parse, analyse, generate and emit code for the source files, then exit.
                             The result of this step is an assembler file.
                             The format of assembler output can be controled with the '--emit-*' options.
    --assemble, -c           Perform the entire compilation pipeline, resulting in an object file, then exit.

    --include-comments       When running with '--preprocess', include comments in the output.
                             Comments are otherwise stripped from preprocessed output by default.
    --no-lower               Do not lower the AST during semantic analysis when used alongside '--sema'.
                             Otherwise, this option has no effect as lowering is a required step to continue.
    --ir                     Print IR to stderr, implying '--codegen'.
"
        );

        return 0;
    }

    public override int Execute()
    {
        if (Options.ShowHelp)
            return ShowHelp();

        var languageOptions = new LanguageOptions();
        languageOptions.SetDefaultsOnlyC(Context, Options.Standard);

        Context.Todo("C compiler driver impl");

        return 0;
    }
}

public record class CDriverOptionsParseState
    : BaseCompilerDriverParseState
{
    public InputFileKind InputFileKind { get; set; } = InputFileKind.Unknown;
}

public sealed class CDriverOptions
    : BaseCompilerDriverOptions<CDriverOptions, CDriverOptionsParseState>
{
    public DriverStage DriverStage { get; set; } = DriverStage.Assemble;
    public AssemblerFormat AssemblerFormat { get; set; } = AssemblerFormat.Default;

    public string? OutputFile { get; set; } = null;
    public bool ReadFromStandardInput { get; set; } = false;
    public InputFileKind StandardInputKind { get; set; } = InputFileKind.Unknown;
    public List<LayeCInputFile> InputFiles { get; set; } = [];

    public LanguageStandardKind Standard { get; set; }
    public Triple Triple { get; set; } = Triple.DefaultTripleForHost();

    public IncludePaths IncludePaths { get; set; } = new();

    public bool PrintTokens { get; set; } = false;
    public bool IncludeCommentsInPreprocessedOutput { get; set; } = false;

    protected override void Validate(DiagnosticEngine diag, CDriverOptionsParseState state)
    {
        base.Validate(diag, state);

        if (!ShowHelp)
        {
            if (ReadFromStandardInput && InputFiles.Count != 0)
                diag.Emit(DiagnosticLevel.Error, "Cannot read from standard input while input files are also specified.");

            if (!ReadFromStandardInput && InputFiles.Count == 0)
                diag.Emit(DiagnosticLevel.Error, "No source files provided.");
        }
    }

    protected override void Finalize(DiagnosticEngine diag, CDriverOptionsParseState state)
    {
        base.Finalize(diag, state);

        if (AssemblerFormat == AssemblerFormat.Default)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                AssemblerFormat = AssemblerFormat.LLVM;
            else AssemblerFormat = AssemblerFormat.QBE;
        }
    }

    protected override void HandleValue(string value, DiagnosticEngine diag, CliArgumentIterator args, CDriverOptionsParseState state)
    {
        var fileInfo = new FileInfo(value);
        if (!fileInfo.Exists)
            diag.Emit(DiagnosticLevel.Error, $"No such file or directory '{value}'.");

        var inputKind = state.InputFileKind;

        string fileExtension = fileInfo.Extension;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            fileExtension = fileExtension.ToLower();

        switch (fileExtension)
        {
            case ".c": inputKind = InputFileKind.CSource; break;
            case ".h" or ".inc": inputKind = InputFileKind.CHeader; break;

            case ".laye" or ".mod":
            {
                diag.Emit(DiagnosticLevel.Error, "The Laye toolchain's C compiler does cannot operate on Laye source files or module binaries.");
            } break;

            case ".C" or ".cc" or ".cpp" or ".cxx" or ".c++" or ".H" or ".hh" or ".hpp" or ".h++" or ".hxx" or ".ixx" or ".ccm" or ".cppm" or ".cxxm" or ".c++m" or ".mpp":
            {
                diag.Emit(DiagnosticLevel.Error, "The Laye toolchain is not a C++ compiler.");
                diag.Emit(DiagnosticLevel.Note, "Use your favorite C++ compiler to generate objects to link against instead.");
            } break;

            case ".S" or ".s" or ".asm" or ".qbe" or ".ll":
            {
                diag.Emit(DiagnosticLevel.Error, "The Laye toolchain's C compiler does not provide any assembler functionality.");
                diag.Emit(DiagnosticLevel.Note, "Use the appriopriate tool to generate objects to link against instead.");
                diag.Emit(DiagnosticLevel.Note, "The Laye compiler driver may be able to identify these tools automatically for you.");
            } break;

            default:
            {
                diag.Emit(DiagnosticLevel.Error, $"Unrecognized input file extension '{fileInfo.Extension}'.");
                diag.Emit(DiagnosticLevel.Note, "Use a recognized extension, or see the '-x' option in '--help'.");
            } break;
        }

        InputFiles.Add(new LayeCInputFile()
        {
            Kind = inputKind,
            InputName = value,
            FileInfo = fileInfo,
        });
    }

    protected override void HandleArgument(string arg, DiagnosticEngine diag, CliArgumentIterator args, CDriverOptionsParseState state)
    {
        switch (arg)
        {
            default: base.HandleArgument(arg, diag, args, state); break;

            case "--tokens": DriverStage = DriverStage.Lex; break;
            case "--preprocess" or "-E": DriverStage = DriverStage.Preprocess; break;
            case "--parse" or "-fsyntax-only" or "--sema": DriverStage = DriverStage.Sema; break;
            case "--codegen": DriverStage = DriverStage.Codegen; break;
            case "--compile" or "-S": DriverStage = DriverStage.Compile; break;
            case "--assemble" or "-c": DriverStage = DriverStage.Assemble; break;

            case "--emit-gas" or "-emit-gas": AssemblerFormat = AssemblerFormat.GAS; break;
            case "--emit-nasm" or "-emit-nasm": AssemblerFormat = AssemblerFormat.NASM; break;
            case "--emit-fasm" or "-emit-fasm": AssemblerFormat = AssemblerFormat.FASM; break;
            case "--emit-qbe" or "-emit-qbe": AssemblerFormat = AssemblerFormat.QBE; break;
            case "--emit-llvm" or "-emit-llvm": AssemblerFormat = AssemblerFormat.LLVM; break;

            case "-o":
            {
                if (!args.Shift(out string includePath))
                    diag.Emit(DiagnosticLevel.Error, "Option '-o' requires an argument.");
                else OutputFile = includePath;
            } break;

            case string when arg.StartsWith("-o="):
            {
                string includePath = arg[3..];
                if (string.IsNullOrWhiteSpace(includePath))
                    diag.Emit(DiagnosticLevel.Error, "Option '-o' requires an argument.");
                else OutputFile = includePath;
            } break;

            case string when arg.StartsWith("-std="): SetStandard(arg[5..]); break;
            case string when arg.StartsWith("--std="): SetStandard(arg[6..]); break;

            case "-isystem":
            {
                if (!args.Shift(out string includePath))
                    diag.Emit(DiagnosticLevel.Error, "Option '-isystem' requires an argument.");
                else IncludePaths.AddSystemIncludePath(includePath);
            } break;

            case "-I":
            {
                if (!args.Shift(out string includePath))
                    diag.Emit(DiagnosticLevel.Error, "Option '-I' requires an argument.");
                else IncludePaths.AddIncludePath(includePath);
            } break;

            case string when arg.StartsWith("-I"):
            {
                string includePath = arg[2..];
                if (string.IsNullOrWhiteSpace(includePath))
                    diag.Emit(DiagnosticLevel.Error, "Option '-I' requires an argument.");
                else IncludePaths.AddIncludePath(includePath);
            } break;

            case "-iquote":
            {
                if (!args.Shift(out string includePath))
                    diag.Emit(DiagnosticLevel.Error, "Option '-iquote' requires an argument.");
                else IncludePaths.AddQuoteIncludePath(includePath);
            } break;

            case string when arg.StartsWith("-iquote="):
            {
                string includePath = arg[8..];
                if (string.IsNullOrWhiteSpace(includePath))
                    diag.Emit(DiagnosticLevel.Error, "Option '-iquote' requires an argument.");
                else IncludePaths.AddIncludePath(includePath);
            } break;

            case "--include-comments": IncludeCommentsInPreprocessedOutput = true; break;
        }

        void SetStandard(string shortName)
        {
            var standardKind = LanguageStandardAlias.GetStandardKind(shortName);
            var standardLanguage = standardKind.GetSourceLanguage();

            if (standardKind == LanguageStandardKind.Unspecified || standardLanguage != SourceLanguage.C)
            {
                diag.Emit(DiagnosticLevel.Error, $"Invalid C language standard '{shortName}' in '{arg}'.");
                foreach (var alias in LanguageStandardAlias.PrimaryCAliases)
                    diag.Emit(DiagnosticLevel.Note, $"Use '{alias.ShortName}' for '{alias.Standard.Description}'.");
            }
            else Standard = standardKind;
        }
    }
}
