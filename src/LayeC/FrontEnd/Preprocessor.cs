using System.Diagnostics;

using LayeC.Source;

namespace LayeC.FrontEnd;

public sealed class Preprocessor
{
    private readonly Stack<ITokenStream> _tokenStreams = [];
    private readonly Queue<Token> _lookAheadTokens = [];

    private CompilerContext Context { get; }
    private SourceText Source { get; }

    private LanguageOptions LanguageOptions { get; }
    private SourceLanguage Language => _tokenStreams.Peek().Language;

    private bool IsLexingFile => _tokenStreams.TryPeek(out var ts) && ts is LexerBackedTokenStream;

    public Preprocessor(Lexer lexer)
    {
        _tokenStreams.Push(new LexerBackedTokenStream(lexer));

        Context = lexer.Context;
        Source = lexer.Source;
        LanguageOptions = lexer.LanguageOptions;
    }

    public Preprocessor(CompilerContext context, SourceText source, LanguageOptions languageOptions, SourceLanguage language = SourceLanguage.Laye)
        : this(new Lexer(context, source, languageOptions, language))
    {
    }

    private void PopTokenStream()
    {
        Context.Assert(_tokenStreams.Count > 0, "Can't pop a token stream because there aren't any left.");
        var tokenStream = _tokenStreams.Pop();

        switch (tokenStream)
        {
            case LexerBackedTokenStream lexerStream:
            {
            } break;

            case ArrayBackedTokenStream arrayStream:
            {
            } break;
        }
    }

    public Token ReadToken()
    {
        var ppToken = ReadAndExpandToken();
        return ConvertPPToken(ppToken);
    }

    public Token ReadAndExpandToken()
    {
        var ppToken = ReadTokenRaw();
        return Preprocess(ppToken);
    }

    public Token ReadTokenRaw()
    {
        var ppToken = ReadTokenRawImpl(true);
        if (ppToken.Kind == TokenKind.CPPIdentifier)
        {
            // TODO(local): see if we should explicitly block macro expansion for this token
        }

        return ppToken;
    }

    private Token ReadTokenRawImpl(bool includeLookAhead)
    {
        if (includeLookAhead && _lookAheadTokens.Count > 0)
            return _lookAheadTokens.Dequeue();

        while (_tokenStreams.Count > 0 && _tokenStreams.Peek().IsAtEnd)
        {
            var ts = _tokenStreams.Peek();
            if (ts is ArrayBackedTokenStream arrayStream && arrayStream.KeepWhenEmpty)
                return Token.AnyEndOfFile;

            _tokenStreams.Pop();
        }

        if (_tokenStreams.Count == 0)
            return Token.AnyEndOfFile;

        var ppToken = _tokenStreams.Peek().Read();
        if (ppToken.Kind == TokenKind.EndOfFile)
            return ReadTokenRawImpl(includeLookAhead);

        return ppToken;
    }

    public Token Preprocess(Token ppToken)
    {
        if (ppToken.Language == SourceLanguage.C && ppToken is { Kind: TokenKind.Hash, IsAtStartOfLine: true } && IsLexingFile)
        {
            var lexer = ((LexerBackedTokenStream)_tokenStreams.Peek()).Lexer;
            using var lexerPreprocessorState = lexer.PushMode(lexer.State | LexerState.CPPWithinDirective);

            var directiveToken = ReadTokenRaw();
            return DispatchDirectiveParser(SourceLanguage.C, directiveToken);
        }

        if (ppToken.Language == SourceLanguage.Laye && ppToken is { Kind: TokenKind.KWPragma or TokenKind.Hash, IsAtStartOfLine: true } && IsLexingFile)
        {
            if (ppToken.Kind == TokenKind.Hash)
                Context.ErrorCStylePreprocessingDirective(ppToken.Source, ppToken.Location);

            var lexer = ((LexerBackedTokenStream)_tokenStreams.Peek()).Lexer;
            using var lexerPreprocessorState = lexer.PushMode(SourceLanguage.C, lexer.State | LexerState.CPPWithinDirective);

            var directiveToken = ReadTokenRaw();
            if (directiveToken.Kind == TokenKind.LiteralString && directiveToken.StringValue == "C")
            {
                Context.Todo("`pragma \"C\"` in Laye source files.");
                throw new UnreachableException();
            }

            return DispatchDirectiveParser(SourceLanguage.Laye, directiveToken);
        }

        if (ppToken.Kind == TokenKind.CPPIdentifier && MaybeExpandMacro(ppToken))
            return Preprocess(ReadTokenRaw());

        // TODO(local): concat adjacent string literals in C?

        return ppToken;

        Token DispatchDirectiveParser(SourceLanguage language, Token directiveToken)
        {
            Context.Assert(IsLexingFile, "Can only handle C preprocessor directives when lexing a source file.");
            Context.Assert(Language == SourceLanguage.C, "Can only handle C preprocessor directives if the lexer was switched to the C language mode.");

            var lexer = ((LexerBackedTokenStream)_tokenStreams.Peek()).Lexer;
            Context.Assert(0 != (lexer.State & LexerState.CPPWithinDirective), "Can only handle C preprocessor directives if the lexer state has been switched to include CPPWithinDirective.");

            if (directiveToken.Kind != TokenKind.CPPIdentifier)
            {
                Context.ErrorExpectedPreprocessorDirective(directiveToken.Source, directiveToken.Location);
                SkipRemainingDirectiveTokens();
            }
            else HandleDirective(language, directiveToken);

            return Preprocess(ReadTokenRaw());
        }
    }

    private bool MaybeExpandMacro(Token ppToken)
    {
        if (ppToken.DisableExpansion)
            return false;

        return false;
    }

    private Token ConvertPPToken(Token ppToken)
    {
        if (ppToken.Kind == TokenKind.CPPNumber)
        {
            Context.Todo("Convet PPNumber tokens.");
        }
        else if (ppToken.Language == SourceLanguage.C && ppToken.Kind == TokenKind.CPPIdentifier)
        {
            if (LanguageOptions.TryGetCKeywordKind(ppToken.StringValue, out var keywordTokenKind))
                ppToken.Kind = keywordTokenKind;
        }

        if (ppToken.Kind == TokenKind.CPPIdentifier)
            ppToken.Kind = TokenKind.Identifier;

        return ppToken;
    }

    private bool SkipRemainingDirectiveTokens()
    {
        bool hasSkippedAnyTokens = false;
        while (true)
        {
            var t = ReadTokenRaw();
            if (t.Kind is TokenKind.CPPDirectiveEnd or TokenKind.EndOfFile)
                break;

            hasSkippedAnyTokens = true;
        }

        return hasSkippedAnyTokens;
    }

    private void HandleDirective(SourceLanguage language, Token directiveToken)
    {
        Context.Assert(directiveToken.Kind == TokenKind.CPPIdentifier, "Can only handle identifier directives.");

        var directiveName = directiveToken.StringValue;
        if (directiveName == "define")
            HandleDefineDirective(directiveToken);
        else
        {
            Context.ErrorUnknownPreprocessorDirective(directiveToken.Source, directiveToken.Location);
            SkipRemainingDirectiveTokens();
        }
    }

    private void HandleDefineDirective(Token directiveToken)
    {
        if (SkipRemainingDirectiveTokens())
            Context.WarningExtraTokensAtEndOfDirective(directiveToken.Source, directiveToken.Location, directiveToken.StringValue);
    }
}
