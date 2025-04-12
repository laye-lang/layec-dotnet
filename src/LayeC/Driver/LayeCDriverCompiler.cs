using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using LayeC.Diagnostics;
using LayeC.FrontEnd;
using LayeC.Source;

namespace LayeC.Driver;

public sealed class LayeCDriverCompiler
    : CompilerDriverWithContext
{
    public static LayeCDriverCompiler Create(DiagnosticConsumerProvider diagProvider, LayeCDriverCompilerOptions options, string programName = "dnlayec")
    {
        var context = new CompilerContext(diagProvider(options.OutputColoring), Target.X86_64)
        {
            IncludePaths = options.IncludePaths,
        };

        return new LayeCDriverCompiler(programName, context, options);
    }

    public LayeCDriverCompilerOptions Options { get; set; }

    private LayeCDriverCompiler(string programName, CompilerContext context, LayeCDriverCompilerOptions options)
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
    --language <kind>, -x <kind>
                             Specify the kind of subsequent input files, or 'default' to infer it from the extension.
                             one of: 'default', 'laye', 'c', 'module'
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

    --no-corelib             Do not link against the the default Laye core libraries.
    --no-rt0                 Do not link against the the default Laye runtime/entry library.

    -iquote <dir>            Adds <dir> to the end of the list of QUOTE include search paths.
    -I <dir>, -I<dir>        Adds <dir> to the end of the list of include search paths.
    -L <lib-dir>             Adds <dir> to the end of the list of library search paths.
                             Directories are searched in the order they are provided, and values
                             provided through the CLI are searched after built-in and environment-
                             specified search paths.

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

    --tokens                 Print token information to stderr, implying '--preprocess'.
    --include-comments       When running with '--preprocess', include comments in the output.
                             Comments are otherwise stripped from preprocessed output by default.
    --ast                    Print ASTs to stderr when, implying '--sema' unless '--parse' is provided.
                             The difference is only present in Laye files, where parsing can happen without analysis.
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
        languageOptions.SetDefaults(Context, Options.Standards);

        if (Options.DriverStage == DriverStage.Preprocess)
            return PreprocessOnly();

        Context.Diag.Emit(DiagnosticLevel.Warning, "The full compilation pipeline is not yet implemented. Stopping early.");
        Context.Diag.Emit(DiagnosticLevel.Note, "You can use options such as '-E', '--parse' etc. to explicitly stop at an earlier stage.");
        Context.Diag.Emit(DiagnosticLevel.Note, $"See '{ProgramName} --help' for information on the available options.");
        return 0;

        bool OpenOutputStream([NotNullWhen(true)] out Stream? stream)
        {
            Context.Assert(Options.OutputFile is not null or "-", "Can only open output file stream when a file path is present and not '-'.");
            try
            {
                stream = File.OpenWrite(Options.OutputFile);
                return true;
            }
            catch (Exception ex)
            {
                stream = null;
                Context.Diag.Emit(DiagnosticLevel.Error, $"Could not open output file '{Options.OutputFile}': {ex.Message}.");
                return false;
            }
        }

        int PreprocessOnly()
        {
            TextWriter outputWriter;
            if (Options.OutputFile is null or "-")
                outputWriter = Console.Out;
            else if (OpenOutputStream(out var outputStream))
                outputWriter = new StreamWriter(outputStream);
            else return 1;

            var debugPrinter = new SyntaxDebugTreePrinter(Options.OutputColoring);
            var ppOutputWriter = new PreprocessedOutputWriter(outputWriter, Options.IncludeCommentsInPreprocessedOutput);

            SourceText sourceText;
            SourceLanguage sourceLanguage = SourceLanguage.Laye;

            // TODO(local): I think we want to pre-load all the text as a step in argument validation so we don't repeat this logic.
            if (Options.InputFiles is [{ } singleFile] && !Options.ReadFromStandardInput)
            {
                if (!singleFile.Kind.CanPreprocess())
                {
                    Context.Diag.Emit(DiagnosticLevel.Error, "Can't preprocess this input.");
                    return 1;
                }

                sourceLanguage = singleFile.Kind.SourceLanguage();
                if (sourceLanguage == SourceLanguage.None)
                {
                    Context.Diag.Emit(DiagnosticLevel.Error, "Can't preprocess a non-source input.");
                    return 1;
                }

                sourceText = new SourceText(singleFile.InputName, singleFile.FileInfo.ReadAllText());
            }
            else if (Options.ReadFromStandardInput && Options.InputFiles.Count == 0)
            {
                if (Options.StandardInputKind != InputFileKind.Unknown && !Options.StandardInputKind.CanPreprocess())
                {
                    Context.Diag.Emit(DiagnosticLevel.Error, "Can't preprocess this input.");
                    return 1;
                }

                sourceLanguage = Options.StandardInputKind.SourceLanguage();
                if (sourceLanguage == SourceLanguage.None && Options.StandardInputKind != InputFileKind.Unknown)
                {
                    Context.Diag.Emit(DiagnosticLevel.Error, "Can't preprocess a non-source input.");
                    return 1;
                }
                else sourceLanguage = SourceLanguage.Laye;

                sourceText = new SourceText("<stdin>", Console.In.ReadToEnd());
            }
            else
            {
                Context.Diag.Emit(DiagnosticLevel.Error, "Only a single input source file is allowed when preprocessing.");
                return 1;
            }

            var preprocessor = new Preprocessor(Context, languageOptions);
            var tokens = preprocessor.PreprocessSource(sourceText, sourceLanguage);

            if (Context.Diag.HasEmittedErrors)
                return 1;

            if (Options.PrintTokens)
                tokens.ForEach(debugPrinter.PrintToken);
            else tokens.ForEach(ppOutputWriter.WriteToken);

            return 0;
        }
    }
}

public sealed class LayeCInputFile
{
    public required InputFileKind Kind { get; init; }
    public required string InputName { get; init; }
    public required FileInfo FileInfo { get; init; }
}

public record class LayeCDriverCompilerOptionsParseState
    : BaseCompilerDriverParseState
{
    public InputFileKind InputFileKind { get; set; } = InputFileKind.Unknown;
}

public sealed class LayeCDriverCompilerOptions
    : LayeCSharedDriverOptions<LayeCDriverCompilerOptions, LayeCDriverCompilerOptionsParseState>
{
    public DriverStage DriverStage { get; set; } = DriverStage.Assemble;
    public AssemblerFormat AssemblerFormat { get; set; } = AssemblerFormat.Default;

    public string? OutputFile { get; set; } = null;
    public bool ReadFromStandardInput { get; set; } = false;
    public InputFileKind StandardInputKind { get; set; } = InputFileKind.Unknown;
    public List<LayeCInputFile> InputFiles { get; set; } = [];

    public LanguageStandardKinds Standards { get; set; }

    public IncludePaths IncludePaths { get; set; } = new();

    public bool PrintTokens { get; set; } = false;
    public bool IncludeCommentsInPreprocessedOutput { get; set; } = false;

    protected override void Validate(DiagnosticEngine diag, LayeCDriverCompilerOptionsParseState state)
    {
        base.Validate(diag, state);

        if (ReadFromStandardInput && InputFiles.Count != 0)
            diag.Emit(DiagnosticLevel.Error, "Cannot read from standard input while input files are also specified.");

        if (!ReadFromStandardInput && InputFiles.Count == 0)
            diag.Emit(DiagnosticLevel.Error, "No source files provided.");
    }

    protected override void Finalize(DiagnosticEngine diag, LayeCDriverCompilerOptionsParseState state)
    {
        base.Finalize(diag, state);
    }

    protected override void HandleValue(string value, DiagnosticEngine diag, CliArgumentIterator args, LayeCDriverCompilerOptionsParseState state)
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
            case ".laye": inputKind = InputFileKind.LayeSource; break;
            case ".c": inputKind = InputFileKind.CSource; break;
            case ".h" or ".inc": inputKind = InputFileKind.CHeader; break;

            default:
            {
                diag.Emit(DiagnosticLevel.Error, $"Unrecognized input file extension '{fileInfo.Extension}'.");
                diag.Emit(DiagnosticLevel.Note, "Use a recognized extension, or see the '-x' option in '--help'.");
                diag.Emit(DiagnosticLevel.Note, "Assuming this is a Laye source file.");
            } break;
        }

        InputFiles.Add(new LayeCInputFile()
        {
            Kind = inputKind,
            InputName = value,
            FileInfo = fileInfo,
        });
    }

    protected override void HandleArgument(string arg, DiagnosticEngine diag, CliArgumentIterator args, LayeCDriverCompilerOptionsParseState state)
    {
        switch (arg)
        {
            default: base.HandleArgument(arg, diag, args, state); break;

            case "--preprocess" or "-E": DriverStage = DriverStage.Preprocess; break;
            case "--parse" or "-fsyntax-only": DriverStage = DriverStage.Parse; break;
            case "--sema": DriverStage = DriverStage.Sema; break;
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

            case string when arg.StartsWith("-std="): ParseStd(arg[5..]); break;
            case string when arg.StartsWith("--std="): ParseStd(arg[6..]); break;
            case string when arg.StartsWith("-cstd="): ParseCStd(arg[6..]); break;
            case string when arg.StartsWith("--cstd="): ParseCStd(arg[7..]); break;

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

            case "--tokens": DriverStage = DriverStage.Preprocess; PrintTokens = true; break;
            case "--include-comments": IncludeCommentsInPreprocessedOutput = true; break;
        }

        void ParseCStd(string shortName)
        {
            var standardKind = LanguageStandardAlias.GetStandardKind(shortName);
            var standardLanguage = standardKind.GetSourceLanguage();

            if (standardKind == LanguageStandardKind.Unspecified || standardLanguage != SourceLanguage.C)
            {
                diag.Emit(DiagnosticLevel.Error, $"Invalid C language standard '{shortName}' in '{arg}'.");
                foreach (var alias in LanguageStandardAlias.PrimaryCAliases)
                    diag.Emit(DiagnosticLevel.Note, $"Use '{alias.ShortName}' for '{alias.Standard.Description}'.");
            }
            else Standards = (Standards.Laye, standardKind);
        }

        void ParseStd(string shortName)
        {
            if (shortName.Split(',') is [string, string] shortNames)
            {
                var standardKinds = new[] { LanguageStandardAlias.GetStandardKind(shortNames[0]), LanguageStandardAlias.GetStandardKind(shortNames[1]) };

                Standards = (LanguageStandardKind.Unspecified, LanguageStandardKind.Unspecified);
                if (standardKinds is [LanguageStandardKind.Unspecified, LanguageStandardKind.Unspecified])
                {
                    diag.Emit(DiagnosticLevel.Error, $"Invalid language standards '{shortNames[0]}' and '{shortNames[1]}' in '{arg}'.");
                    foreach (var alias in LanguageStandardAlias.PrimaryAliases)
                        diag.Emit(DiagnosticLevel.Note, $"Use '{alias.ShortName}' for '{alias.Standard.Description}'.");
                    return;
                }

                var standardLanguages = new[] { standardKinds[0].GetSourceLanguage(), standardKinds[1].GetSourceLanguage() };
                if (standardLanguages is [not SourceLanguage.None, SourceLanguage.None])
                {
                    var inferredLanguage = standardLanguages[0] == SourceLanguage.Laye ? SourceLanguage.C : SourceLanguage.Laye;
                    diag.Emit(DiagnosticLevel.Error, $"Invalid {inferredLanguage} language standards '{shortNames[0]}' and '{shortNames[1]}' in '{arg}'.");
                    foreach (var alias in inferredLanguage == SourceLanguage.Laye ? LanguageStandardAlias.PrimaryLayeAliases : LanguageStandardAlias.PrimaryCAliases)
                        diag.Emit(DiagnosticLevel.Note, $"Use '{alias.ShortName}' for '{alias.Standard.Description}'.");
                    return;
                }
                else if (standardLanguages is [SourceLanguage.None, not SourceLanguage.None])
                {
                    var inferredLanguage = standardLanguages[1] == SourceLanguage.Laye ? SourceLanguage.C : SourceLanguage.Laye;
                    diag.Emit(DiagnosticLevel.Error, $"Invalid {inferredLanguage} language standards '{shortNames[0]}' and '{shortNames[1]}' in '{arg}'.");
                    foreach (var alias in inferredLanguage == SourceLanguage.Laye ? LanguageStandardAlias.PrimaryLayeAliases : LanguageStandardAlias.PrimaryCAliases)
                        diag.Emit(DiagnosticLevel.Note, $"Use '{alias.ShortName}' for '{alias.Standard.Description}'.");
                    return;
                }

                Debug.Assert(standardKinds is not [LanguageStandardKind.Unspecified, LanguageStandardKind.Unspecified]);
                Debug.Assert(standardLanguages is [not SourceLanguage.None, not SourceLanguage.None]);

                if (standardLanguages[0] == standardLanguages[1])
                {
                    diag.Emit(DiagnosticLevel.Error, $"Two standards for the {standardLanguages[0]} language provided.");
                    diag.Emit(DiagnosticLevel.Note, "Provide exactly one each for Laye and for C.");
                    return;
                }

                if (standardLanguages is not [SourceLanguage.Laye, SourceLanguage.C])
                {
                    standardKinds = [standardKinds[1], standardKinds[0]];
                    standardLanguages = [standardLanguages[1], standardLanguages[0]];
                }

                Debug.Assert(standardLanguages is [SourceLanguage.Laye, SourceLanguage.C]);
                Standards = (standardKinds[0], standardKinds[1]);
            }
            else
            {
                var standardKind = LanguageStandardAlias.GetStandardKind(shortName);
                var standardLanguage = standardKind.GetSourceLanguage();

                if (standardKind == LanguageStandardKind.Unspecified || standardLanguage != SourceLanguage.Laye)
                {
                    diag.Emit(DiagnosticLevel.Error, $"Invalid Laye language standard '{shortName}' in '{arg}'.");
                    foreach (var alias in LanguageStandardAlias.PrimaryLayeAliases)
                        diag.Emit(DiagnosticLevel.Note, $"Use '{alias.ShortName}' for '{alias.Standard.Description}'.");
                    diag.Emit(DiagnosticLevel.Note, "To provide a C language standard, use either '--cstd=<standard>' or pass a comma-separated pair of Laye and C standards to '--std=<standard>'.");
                }
                else Standards = (standardKind, Standards.C);
            }
        }
    }
}
