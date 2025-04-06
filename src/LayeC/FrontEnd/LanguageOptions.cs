using System.Diagnostics;

namespace LayeC.FrontEnd;

public sealed class LanguageOptions
{
    public static readonly LanguageStandardKinds DefaultLanguageStandards = (LanguageStandardKind.LayeIndev, LanguageStandardKind.C23);

    public LanguageStandardKinds Standards { get; private set; }

    private LanguageFeatures CLanguageFeatures { get; set; }
    private LanguageFeatures LayeLanguageFeatures { get; set; }

    public bool CHasLineComments { get; private set; }
    public bool CIsC99 { get; private set; }
    public bool CIsC11 { get; private set; }
    public bool CIsC17 { get; private set; }
    public bool CIsC23 { get; private set; }
    public bool CIsC2Y { get; private set; }

    public bool CHasDigraphs { get; private set; }
    public bool CHasHexFloats { get; private set; }

    public bool CIsGNUMode { get; private set; }
    public bool CIsClangMode { get; private set; }
    public bool CIsMSVCMode { get; private set; }

    public bool CHasEmbeddedExtensions { get; private set; }

    public bool LayeHasEmbeddedExtensions { get; private set; }

    public void SetDefaults(CompilerContext context, LanguageStandardKinds standardKinds)
    {
        if (standardKinds is (LanguageStandardKind.Unspecified, LanguageStandardKind.Unspecified))
            standardKinds = DefaultLanguageStandards;
        else if (standardKinds.Laye == LanguageStandardKind.Unspecified)
            standardKinds = (DefaultLanguageStandards.Laye, standardKinds.C);
        else if (standardKinds.C == LanguageStandardKind.Unspecified)
            standardKinds = (standardKinds.Laye, DefaultLanguageStandards.C);

        Debug.Assert(standardKinds is not (LanguageStandardKind.Unspecified, LanguageStandardKind.Unspecified));

        Standards = standardKinds;

        SetLayeStandardDefaults(context, standardKinds.Laye);
        SetCStandardDefaults(context, standardKinds.C);
    }

    private void SetCStandardDefaults(CompilerContext context, LanguageStandardKind standardKind)
    {
        if (!LanguageStandard.TryGetStandard(standardKind, out var standard))
        {
            context.Diag.Emit(Diagnostics.DiagnosticLevel.Fatal, $"Unable to get {standardKind} language standard information.");
            throw new UnreachableException();
        }

        context.Assert(standard.Language == SourceLanguage.C, $"Should be setting the defaults for a C standard, but got a {standard.Language} standard.");

        CLanguageFeatures = standard.Features;

        CHasLineComments = standard.Features.HasFlag(LanguageFeatures.CLineComment);
        CIsC99 = standard.Features.HasFlag(LanguageFeatures.C99);
        CIsC11 = standard.Features.HasFlag(LanguageFeatures.C11);
        CIsC17 = standard.Features.HasFlag(LanguageFeatures.C17);
        CIsC23 = standard.Features.HasFlag(LanguageFeatures.C23);
        CIsC2Y = standard.Features.HasFlag(LanguageFeatures.C2Y);

        CHasDigraphs = standard.Features.HasFlag(LanguageFeatures.CDigraphs);
        CHasHexFloats = standard.Features.HasFlag(LanguageFeatures.CHexFloat);

        CIsGNUMode = standard.Features.HasFlag(LanguageFeatures.GNUMode);
        CIsClangMode = standard.Features.HasFlag(LanguageFeatures.ClangMode);
        CIsMSVCMode = standard.Features.HasFlag(LanguageFeatures.MSVCMode);

        CHasEmbeddedExtensions = standard.Features.HasFlag(LanguageFeatures.Embedded);
    }

    private void SetLayeStandardDefaults(CompilerContext context, LanguageStandardKind standardKind)
    {
        if (!LanguageStandard.TryGetStandard(standardKind, out var standard))
        {
            context.Diag.Emit(Diagnostics.DiagnosticLevel.Fatal, $"Unable to get {standardKind} language standard information.");
            throw new UnreachableException();
        }

        context.Assert(standard.Language == SourceLanguage.Laye, $"Should be setting the defaults for a Laye standard, but got a {standard.Language} standard.");

        LayeLanguageFeatures = standard.Features;

        LayeHasEmbeddedExtensions = standard.Features.HasFlag(LanguageFeatures.Embedded);
    }

    public bool TryGetCKeywordKind(StringView identifierText, out TokenKind keywordTokenKind)
    {
        if (SyntaxFacts.TryGetCKeywordInfo(identifierText, out var keywordInfo))
        {
            if (keywordInfo.Features == (CLanguageFeatures & keywordInfo.Features))
            {
                keywordTokenKind = keywordInfo.KeywordKind;
                return true;
            }
        }

        keywordTokenKind = TokenKind.Identifier;
        return false;
    }

    public bool TryGetLayeKeywordKind(StringView identifierText, out TokenKind keywordTokenKind)
    {
        if (SyntaxFacts.TryGetLayeKeywordInfo(identifierText, out var keywordInfo))
        {
            if (keywordInfo.Features == (LayeLanguageFeatures & keywordInfo.Features))
            {
                keywordTokenKind = keywordInfo.KeywordKind;
                return true;
            }
        }

        keywordTokenKind = TokenKind.Identifier;
        return false;
    }
}
