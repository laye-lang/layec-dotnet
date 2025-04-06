using System.Diagnostics;

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

    public LanguageStandardKinds Standards { get; set; }

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

            case string when arg.StartsWith("-std="): ParseStd(arg[5..]); break;
            case string when arg.StartsWith("--std="): ParseStd(arg[6..]); break;
            case string when arg.StartsWith("-cstd="): ParseCStd(arg[6..]); break;
            case string when arg.StartsWith("--cstd="): ParseCStd(arg[7..]); break;
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
