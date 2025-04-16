using LayeC.Diagnostics;
using LayeC.FrontEnd;

namespace LayeC.Driver;

public enum CCompilerCommand
{
    Compile,
    Run,
    Format,
    LanguageServer,
}

public sealed class CDriverOptions
    : BaseCompilerDriverOptions<CDriverOptions, BaseCompilerDriverParseState>
{
    public CCompilerCommand Command { get; set; } = CCompilerCommand.Compile;

    public List<(string Name, FileInfo File)> InputFiles { get; set; } = [];

    public LanguageStandardKinds Standards { get; set; }
    public Triple Triple { get; set; } = Triple.DefaultTripleForHost();

    public IncludePaths IncludePaths { get; set; } = new();

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

            case "--run": Command = CCompilerCommand.Run; break;
            case "--format": Command = CCompilerCommand.Format; break;
            case "--lsp" or "--language-server": Command = CCompilerCommand.LanguageServer; break;

            case string when arg.StartsWith("-std="): SetStandard(arg[5..]); break;
            case string when arg.StartsWith("--std="): SetStandard(arg[6..]); break;
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
            else Standards = (LanguageStandardKind.Unspecified, standardKind);
        }
    }
}
