using LayeC.Source;

namespace LayeC.FrontEnd;

public sealed class Preprocessor(Lexer lexer)
{
    private readonly Lexer _lexer = lexer;
    
    private CompilerContext Context => _lexer.Context;
    private SourceText Source => _lexer.Source;

    private LanguageOptions LanguageOptions => _lexer.LanguageOptions;
    private SourceLanguage Language => _lexer.Language;

    public Preprocessor(CompilerContext context, SourceText source, LanguageOptions languageOptions, SourceLanguage language = SourceLanguage.Laye)
        : this(new Lexer(context, source, languageOptions, language))
    {
    }

    public Token ReadToken()
    {
        var ppToken = _lexer.ReadNextPPToken();

        if (ppToken.Kind == TokenKind.Hash || (ppToken is { Language: SourceLanguage.Laye, Kind: TokenKind.KWPragma }))
        {
            Context.Diag.Emit(Diagnostics.DiagnosticLevel.Remark, Source, ppToken.Location, "Should probably switch to the preprocessor.");
        }

        if (ppToken.Language == SourceLanguage.C && ppToken.Kind == TokenKind.CPPIdentifier)
        {
            if (LanguageOptions.TryGetCKeywordKind(ppToken.StringValue, out var keywordTokenKind))
                ppToken.Kind = keywordTokenKind;
        }

        if (ppToken.Kind == TokenKind.CPPIdentifier)
            ppToken.Kind = TokenKind.Identifier;

        return ppToken;
    }
}
