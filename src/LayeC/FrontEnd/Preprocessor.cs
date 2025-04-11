using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;

using LayeC.Diagnostics;
using LayeC.Source;

namespace LayeC.FrontEnd;

public sealed class PreprocessorMacroDefinition(StringView name, IEnumerable<Token> tokens, IEnumerable<StringView> parameterNames)
{
    public StringView Name { get; } = name;
    public IReadOnlyList<Token> Tokens { get; } = [.. tokens];
    public IReadOnlyList<StringView> ParameterNames { get; } = [.. parameterNames];

    public bool IsVariadic { get; init; }
    public bool IsFunctionLike { get; init; }
    public bool RequiresPasting { get; init; }

    public bool IsExpanding { get; set; } = false;
}

public sealed class Preprocessor(CompilerContext context, LanguageOptions languageOptions)
{
    public static string StringizeToken(Token token, bool escape)
    {
        switch (token.Kind)
        {
            default: return (string)token.Spelling;
            case TokenKind.Invalid: return "<INVALID>";
            case TokenKind.UnexpectedCharacter: return "<?>";
            case TokenKind.EndOfFile: return "<EOF>";
        }
    }

    private CompilerContext Context { get; } = context;
    private LanguageOptions LanguageOptions { get; } = languageOptions;

    public Token[] PreprocessSource(SourceText source, SourceLanguage language)
    {
        Context.Assert(_tokenStreams.Count == 0, "There should be no token streams left when preprocessing a new source file.");
        PushTokenStream(new LexerTokenStream(new(Context, source, LanguageOptions, language)));

        var tokens = new List<Token>();
        while (_tokenStreams.Count > 0)
        {
            var token = ReadToken();
            tokens.Add(token);

            if (token.Kind == TokenKind.EndOfFile)
            {
                Context.Assert(_tokenStreams.Count == 0, "Should have only returned the final EOF token if all token streams are empty.");
                break;
            }
        }

        Context.Diag.Flush();
        return [.. tokens];
    }

    #region Implementation

    private readonly Stack<ITokenStream> _tokenStreams = [];
    private Token? _peekedRawToken;

    private readonly Dictionary<StringView, PreprocessorMacroDefinition> _macroDefs = [];

    private bool _withinExpressionPragmaC = false;
    private bool IsInLexer => _peekedRawToken is null && _tokenStreams.TryPeek(out var ts) && ts is LexerTokenStream;
    private bool ArePreprocessorDirectivesAllowed => !_withinExpressionPragmaC && IsInLexer;

    private SourceText Source
    {
        get
        {
            if (_peekedRawToken is { } p)
                return p.Source;
            return _tokenStreams.TryPeek(out var ts) ? ts.Source : SourceText.Unknown;
        }
    }

    private SourceLanguage Language
    {
        get
        {
            if (_peekedRawToken is { } p)
                return p.Language;
            return _tokenStreams.TryPeek(out var ts) ? ts.Language : SourceLanguage.None;
        }
    }

    private Token NextRawPPToken
    {
        get
        {
            if (_peekedRawToken is { } peeked)
                return peeked;

            return _peekedRawToken = ReadTokenRawImpl();
        }
    }

    private void PushTokenStream(ITokenStream tokenStream)
    {
        MaterializePeekedToken();
        _tokenStreams.Push(tokenStream);
    }

    private void AddCIncludeLexerFromC(SourceText source)
    {
        PushTokenStream(new LexerTokenStream(new(Context, source, LanguageOptions, SourceLanguage.C)));
    }

    private void AddCIncludeLexerFromLaye(SourceText source, Token layeSourceToken)
    {
        Token[] leadingTokens = [
            new Token(TokenKind.KWPragma, SourceLanguage.Laye, layeSourceToken.Source, layeSourceToken.Range)
            {
                Spelling = "pragma",
                LeadingTrivia = layeSourceToken.LeadingTrivia,
                TrailingTrivia = new([new TriviumLiteral(" ")], false),
                IsAtStartOfLine = true,
            },
            new Token(TokenKind.LiteralString, SourceLanguage.Laye, layeSourceToken.Source, layeSourceToken.Range)
            {
                Spelling = "\"C\"",
                StringValue = "C",
                TrailingTrivia = new([new TriviumLiteral(" ")], false),
                HasWhiteSpaceBefore = true,
            },
            new Token(TokenKind.OpenCurly, SourceLanguage.Laye, layeSourceToken.Source, layeSourceToken.Range)
            {
                Spelling = "{",
                TrailingTrivia = new([new TriviumLiteral("\n")], false),
                HasWhiteSpaceBefore = true,
            }
        ];

        Token[] trailingTokens = [
            new Token(TokenKind.CloseCurly, SourceLanguage.Laye, layeSourceToken.Source, layeSourceToken.Range)
            {
                Spelling = "}",
                IsAtStartOfLine = true,
                TrailingTrivia = new([new TriviumLiteral("\n")], false),
                HasWhiteSpaceBefore = true,
            }
        ];

        var leadingTokenStream = new BufferTokenStream(leadingTokens);
        var lexerStream = new LexerTokenStream(new(Context, source, LanguageOptions, SourceLanguage.C));
        var trailingTokenStream = new BufferTokenStream(trailingTokens);

        PushTokenStream(trailingTokenStream);
        PushTokenStream(lexerStream);
        PushTokenStream(leadingTokenStream);
    }

    private void PopTokenStream()
    {
        Context.Assert(_tokenStreams.Count > 0, "Can't pop a token stream because there aren't any left.");
        var tokenStream = _tokenStreams.Pop();

        switch (tokenStream)
        {
            case LexerTokenStream lexerStream:
            {
                if (lexerStream.Lexer.PreprocessorIfDepth > 0)
                    Context.ErrorMissingEndif(lexerStream.Lexer.Source, lexerStream.Lexer.EndOfFileLocation);
            } break;

            case BufferTokenStream bufferStream:
            {
                if (bufferStream.SourceMacro is { } macro)
                    macro.IsExpanding = false;
            } break;
        }
    }

    private void MaterializePeekedToken()
    {
        var peeked = _peekedRawToken;
        _peekedRawToken = null;

        if (peeked is null or { Kind: TokenKind.EndOfFile })
            return;

        _tokenStreams.Push(new BufferTokenStream([peeked]));
    }

    private Token ReadToken()
    {
        var ppToken = ReadAndExpandToken();
        return ConvertPPToken(ppToken);
    }

    private Token ReadAndExpandToken()
    {
        var ppToken = ReadTokenRaw();
        if (Preprocess(ppToken) is { } preprocessedToken)
            return preprocessedToken;
        else return ReadAndExpandToken();
    }

    private Token ReadTokenRaw()
    {
        var ppToken = ReadTokenRawImpl();
        if (ppToken.Kind == TokenKind.CPPIdentifier)
        {
            // TODO(local): see if we should explicitly block macro expansion for this token
        }

        return ppToken;
    }

    private Token ReadTokenRawImpl()
    {
        var ppToken = ImplButForReal();
        return ppToken;

        Token ImplButForReal()
        {
            if (_peekedRawToken is { } peeked)
            {
                _peekedRawToken = null;
                return peeked;
            }

            while (_tokenStreams.Count > 0 && _tokenStreams.Peek().IsAtEnd)
            {
                var ts = _tokenStreams.Peek();
                if (ts is BufferTokenStream arrayStream && arrayStream.KeepWhenEmpty)
                    return Token.AnyEndOfFile;

                PopTokenStream();
            }

            if (_tokenStreams.Count == 0)
                return Token.AnyEndOfFile;

            var ppToken = _tokenStreams.Peek().Read();
            if (ppToken.Kind == TokenKind.EndOfFile)
            {
                if (_tokenStreams.Count == 1)
                {
                    PopTokenStream();
                    return ppToken;
                }

                return ReadTokenRawImpl();
            }

            return ppToken;
        }
    }

    /// <summary>
    /// Returns null when this token resulted in some form of preprocessor expansion, else the token.
    /// </summary>
    private Token? Preprocess(Token ppToken)
    {
        if (ppToken.Language == SourceLanguage.C && ppToken is { Kind: TokenKind.Hash, IsAtStartOfLine: true } && ArePreprocessorDirectivesAllowed)
        {
            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

            var directiveToken = ReadTokenRaw();
            using (var lexerPreprocessorState = lexer.PushMode(lexer.State | LexerState.CPPWithinDirective))
            {
                DispatchDirectiveParser(SourceLanguage.C, directiveToken);
            }

            return null;
        }

        if (ppToken.Language == SourceLanguage.C && ppToken is { Kind: TokenKind.Hash, IsAtStartOfLine: true } && _withinExpressionPragmaC)
        {
            Context.WarningPotentialPreprocessorDirectiveInPragmaCExpression(ppToken);
        }

        if (ppToken.Language == SourceLanguage.Laye && ppToken is { Kind: TokenKind.KWPragma or TokenKind.Hash, DisableExpansion: false, IsAtStartOfLine: true } && ArePreprocessorDirectivesAllowed)
        {
            if (ppToken.Kind == TokenKind.Hash)
                Context.ErrorCStylePreprocessingDirective(ppToken.Source, ppToken.Location);

            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

            var directiveToken = ReadTokenRaw();
            if (directiveToken.Kind == TokenKind.LiteralString && directiveToken.StringValue == "C")
            {
                return HandlePragmaC(false, lexer, ppToken, directiveToken);
            }
            else
            {
                using (var lexerPreprocessorState = lexer.PushMode(SourceLanguage.C, lexer.State | LexerState.CPPWithinDirective))
                {
                    DispatchDirectiveParser(SourceLanguage.Laye, directiveToken);
                }
            }

            return null;
        }

        if (ppToken.Language == SourceLanguage.Laye && ppToken is { Kind: TokenKind.KWPragma, DisableExpansion: false } && IsInLexer && NextRawPPToken.Kind == TokenKind.LiteralString && NextRawPPToken.StringValue == "C")
        {
            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;
            var directiveToken = ReadTokenRaw();
            return HandlePragmaC(true, lexer, ppToken, directiveToken);
        }

        if (ppToken.Kind == TokenKind.CPPIdentifier && MaybeExpandMacro(ppToken))
            return null;

        // TODO(local): concat adjacent string literals in C?

        return ppToken;

        void DispatchDirectiveParser(SourceLanguage language, Token directiveToken)
        {
            Context.Assert(ArePreprocessorDirectivesAllowed, directiveToken.Source, directiveToken.Location, "Can only handle C preprocessor directives when lexing a source file.");
            Context.Assert(Language == SourceLanguage.C, "Can only handle C preprocessor directives if the lexer was switched to the C language mode.");

            directiveToken.Language = SourceLanguage.C;

            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;
            Context.Assert(0 != (lexer.State & LexerState.CPPWithinDirective), "Can only handle C preprocessor directives if the lexer state has been switched to include CPPWithinDirective.");

            if (directiveToken.Kind is not (TokenKind.CPPIdentifier or TokenKind.Identifier))
            {
                Context.ErrorExpectedPreprocessorDirective(directiveToken.Source, directiveToken.Location);
                SkipRemainingDirectiveTokens();
            }
            else HandleDirective(language, ppToken, directiveToken);
        }

        Token? HandlePragmaC(bool expressionOnly, Lexer lexer, Token pragmaToken, Token cStringToken)
        {
            pragmaToken.DisableExpansion = true;

            var openDelimiterToken = ReadTokenRaw();
            if (expressionOnly)
            {
                if (openDelimiterToken.Kind is not TokenKind.OpenParen)
                {
                    Context.ErrorExpectedToken(openDelimiterToken.Source, openDelimiterToken.Location, "'('");
                    return Preprocess(openDelimiterToken);
                }
            }
            else
            {
                if (openDelimiterToken.Kind is not (TokenKind.OpenParen or TokenKind.OpenCurly))
                {
                    Context.ErrorExpectedToken(openDelimiterToken.Source, openDelimiterToken.Location, "'(' or '{'");
                    return Preprocess(openDelimiterToken);
                }
            }

            TokenKind closeDelimiterKind = TokenKind.CloseCurly;
            if (openDelimiterToken.Kind == TokenKind.OpenParen)
            {
                _withinExpressionPragmaC = true;
                closeDelimiterKind = TokenKind.CloseParen;
            }

            var pragmaTokenBody = new List<Token>();
            int delimiterNesting = 1;

            Token token;
            using (var _ = lexer.PushMode(SourceLanguage.C, lexer.State, () => _withinExpressionPragmaC = false))
            {
                while (true)
                {
                    token = ReadTokenRaw();
                    Debug.Assert(token.Language == SourceLanguage.C);

                    if (token.Kind == closeDelimiterKind)
                    {
                        delimiterNesting--;
                        if (delimiterNesting == 0)
                        {
                            token.Language = SourceLanguage.Laye;
                            break;
                        }
                    }
                    else if (token.Kind == TokenKind.EndOfFile)
                        break;

                    if (Preprocess(token) is { } preprocessedToken)
                        pragmaTokenBody.Add(preprocessedToken);
                }
            }

            Context.Assert(_withinExpressionPragmaC == false, "This should have been reset at the end of the previous using scope.");

            Token? closeDelimiterToken = token;
            if (delimiterNesting > 0)
            {
                closeDelimiterToken = null;
                Context.ErrorExpectedMatchingCloseDelimiter(
                    openDelimiterToken.Source,
                    openDelimiterToken.Kind == TokenKind.OpenCurly ? '{' : '(',
                    openDelimiterToken.Kind == TokenKind.OpenCurly ? '}' : ')',
                    token.Range.End,
                    openDelimiterToken.Location
                );
            }

            if (closeDelimiterToken is not null)
                PushTokenStream(new BufferTokenStream([closeDelimiterToken]));
            PushTokenStream(new BufferTokenStream(pragmaTokenBody));
            PushTokenStream(new BufferTokenStream([pragmaToken, cStringToken, openDelimiterToken]));
            return null;
        }
    }

    private sealed class VAOptState
    {
        public int StartOfExpansion { get; set; }
        public int Index { get; set; } = -1;
        public int CloseParenIndex { get; set; }
        public SourceLocation StringizeLocation { get; set; }
        public bool StringiZe { get; set; }
        public bool StringizeWhitespaceBefore { get; set; }
        public bool PasteTokens { get; set; }
        public bool EndsWithPlacemarker { get; set; }
        public bool DidPaste { get; set; }
    }

    private sealed class MacroExpansion
    {
        public required PreprocessorMacroDefinition MacroDefinition { get; init; }
        public required Token SourceToken { get; init; }

        public List<List<Token>> Arguments { get; } = [];
        public List<List<Token>> ExpandedArguments { get; } = [];

        public List<Token> Expansion { get; } = [];
        public int Cursor { get; set; }
        public Token MacroPPTokenAtCursor => MacroDefinition.Tokens[Cursor];

        public VAOptState VAOptState { get; } = new();

        public bool InsertWhiteSpace { get; set; }
        public bool PasteBefore { get; set; }
    }

    private bool MaybeExpandMacro(Token ppToken)
    {
        Context.Assert(ppToken.Kind == TokenKind.CPPIdentifier, "Can only attempt to expand C preprocessor identifiers.");

        if (ppToken.DisableExpansion)
            return false;

        var macroDef = GetExpandableMacroAndArguments(ppToken.StringValue, out var arguments);
        if (macroDef is null || macroDef.IsExpanding)
            return false;

        if (macroDef.IsFunctionLike)
        {
            Context.Todo("Function-like macro expansion.");
        }
        else
        {
            if (macroDef.Tokens.Count == 0)
                return true;

            var expansion = new MacroExpansion()
            {
                MacroDefinition = macroDef,
                SourceToken = ppToken,
            };

            if (macroDef.RequiresPasting)
            {
                for (; expansion.Cursor < macroDef.Tokens.Count; expansion.Cursor++)
                {
                    var token = expansion.MacroPPTokenAtCursor;
                    // Only attempt to paste if there's actually a valid token.
                    // We error on invalid '##' placement, but allow the preprocessor to continue.
                    if (token.Kind == TokenKind.HashHash && expansion.Cursor + 1 < expansion.MacroDefinition.Tokens.Count)
                    {
                        expansion.Cursor++;
                        Paste(expansion, token, expansion.MacroPPTokenAtCursor);
                    }
                    else expansion.Expansion.Add(token);
                }
            }
            else expansion.Expansion.AddRange(macroDef.Tokens);

            PrepareExpansion(expansion, ppToken.IsAtStartOfLine, ppToken.HasWhiteSpaceBefore);
        }

        return true;

        PreprocessorMacroDefinition? GetExpandableMacro(StringView name)
        {
            if (_macroDefs.TryGetValue(name, out var def) && !def.IsExpanding)
                return def;
            else return null;
        }

        PreprocessorMacroDefinition? GetExpandableMacroAndArguments(StringView name, out Token[][] args)
        {
#pragma warning disable IDE0301 // Simplify collection initialization
            args = Array.Empty<Token[]>();
#pragma warning restore IDE0301 // Simplify collection initialization

            var macroDef = GetExpandableMacro(name);
            if (macroDef is null or { IsFunctionLike: false })
                return macroDef;

            if (NextRawPPToken.Kind != TokenKind.OpenParen)
                return null;

            var openParen = ReadTokenRaw();
            Context.Assert(openParen.Kind == TokenKind.OpenParen, openParen.Source, openParen.Location, "How is this not an open paren?");

            return macroDef;
        }

        void PrepareExpansion(MacroExpansion expansion, bool isAtStartOfLine, bool hasWhitespaceBefore)
        {
            if (expansion.Expansion.Count == 0) return;

            expansion.MacroDefinition.IsExpanding = true;

            ITokenStream tokenStream = new BufferTokenStream(expansion.Expansion, expansion.MacroDefinition);
            if (expansion.SourceToken.Language == SourceLanguage.Laye)
            {
                Token[] leadingTokens = [
                    new Token(TokenKind.KWPragma, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                    {
                        Spelling = "pragma",
                        LeadingTrivia = expansion.SourceToken.LeadingTrivia,
                        TrailingTrivia = new([new TriviumLiteral(" ")], false),
                        IsAtStartOfLine = isAtStartOfLine,
                        HasWhiteSpaceBefore = hasWhitespaceBefore,
                    },
                    new Token(TokenKind.LiteralString, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                    {
                        Spelling = "\"C\"",
                        StringValue = "C",
                        TrailingTrivia = new([new TriviumLiteral(" ")], false),
                        HasWhiteSpaceBefore = true,
                    },
                    new Token(TokenKind.OpenParen, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                    {
                        Spelling = "(",
                        TrailingTrivia = new([new TriviumLiteral(" ")], false),
                        HasWhiteSpaceBefore = true,
                    }
                ];

                Token[] trailingTokens = [
                    new Token(TokenKind.CloseParen, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                    {
                        Spelling = ")",
                        TrailingTrivia = expansion.SourceToken.TrailingTrivia,
                        HasWhiteSpaceBefore = true,
                    }
                ];

                expansion.Expansion[^1].TrailingTrivia = new(expansion.Expansion[^1].TrailingTrivia.Trivia.Concat([new TriviumLiteral(" ")]), false);

                var leadingTokenStream = new BufferTokenStream(leadingTokens, expansion.MacroDefinition);
                var trailingTokenStream = new BufferTokenStream(trailingTokens, expansion.MacroDefinition);

                PushTokenStream(trailingTokenStream);
                PushTokenStream(tokenStream);
                PushTokenStream(leadingTokenStream);
            }
            else
            {
                var firstToken = expansion.Expansion[0];
                firstToken.IsAtStartOfLine = isAtStartOfLine;
                firstToken.HasWhiteSpaceBefore = hasWhitespaceBefore;
                PushTokenStream(tokenStream);
            }
        }
    }

    private void Paste(MacroExpansion expansion, Token hashHashToken, Token rightToken)
    {
        if (expansion.Expansion.Count == 0)
            return; // this should already have generated an error for an invalid concatenation

        var leftToken = expansion.Expansion[^1];
        string concatText = StringizeToken(leftToken, false) + StringizeToken(rightToken, false);
        if (concatText.StartsWith("/*") || concatText.StartsWith("//"))
        {
            Context.ErrorConcatentationCannotResultInComment(hashHashToken);
            return;
        }

        var source = new SourceText("<paste>", concatText);
        var proxyContext = new CompilerContext(VoidDiagnosticConsumer.Instance, Context.Target) { IncludePaths = Context.IncludePaths };
        var lexer = new Lexer(proxyContext, source, LanguageOptions, SourceLanguage.C);
        var pastedToken = lexer.ReadNextPPToken();
        pastedToken.LeadingTrivia = leftToken.LeadingTrivia;
        pastedToken.TrailingTrivia = rightToken.TrailingTrivia;
        pastedToken.IsAtStartOfLine = leftToken.IsAtStartOfLine;
        pastedToken.HasWhiteSpaceBefore = leftToken.HasWhiteSpaceBefore;

        if (!lexer.IsAtEnd)
        {
            Context.ErrorConcatenationShouldOnlyResultInOneToken(hashHashToken.Source, hashHashToken.Location);
            return;
        }
        else if (pastedToken.Kind == TokenKind.EndOfFile || proxyContext.Diag.HasEmittedErrors)
        {
            Context.ErrorConcatenationFormedInvalidToken(hashHashToken.Source, hashHashToken.Location, source.Text);
            return;
        }

        expansion.Expansion[^1] = pastedToken;

        if (expansion.VAOptState.Index >= 0)
            expansion.VAOptState.DidPaste = true;

        expansion.InsertWhiteSpace = false;
    }

    private Token ConvertPPToken(Token ppToken)
    {
        if (ppToken.Kind == TokenKind.CPPNumber)
        {
            //Context.Todo("Convet PPNumber tokens.");
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

    private void HandleDirective(SourceLanguage language, Token preprocessorToken, Token directiveToken)
    {
        Context.Assert(directiveToken.Kind is (TokenKind.CPPIdentifier or TokenKind.Identifier), "Can only handle identifier directives.");

        var directiveName = directiveToken.StringValue;
        if (directiveName == "define")
            HandleDefineDirective(preprocessorToken, directiveToken);
        else if (directiveName == "include")
            HandleIncludeDirective(language, preprocessorToken, directiveToken);
        else
        {
            Context.ErrorUnknownPreprocessorDirective(directiveToken.Source, directiveToken.Location);
            SkipRemainingDirectiveTokens();
        }
    }

    private sealed class ParsedMacroData
    {
        public StringView Name { get; set; }
        public List<Token> Tokens { get; } = [];
        public List<StringView> ParameterNames { get; set; } = [];

        public bool IsVariadic { get; set; }
        public bool IsFunctionLike { get; set; }
        public bool RequiresPasting { get; set; }
    }

    private void HandleDefineDirective(Token preprocessorToken, Token directiveToken)
    {
        var macroNameToken = ReadTokenRaw();
        if (macroNameToken.Kind != TokenKind.CPPIdentifier)
        {
            Context.ErrorExpectedMacroName(macroNameToken.Source, macroNameToken.Location);
            SkipRemainingDirectiveTokens();
            return;
        }

        var macroData = new ParsedMacroData()
        {
            Name = macroNameToken.StringValue,
        };

        var token = ReadTokenRaw();
        macroData.IsFunctionLike = token is { Kind: TokenKind.OpenParen, HasWhiteSpaceBefore: false };

        if (macroData.IsFunctionLike)
        {
            var openParenToken = token;

            token = ReadTokenRaw();
            while (token.Kind is not (TokenKind.EndOfFile or TokenKind.CloseParen or TokenKind.CPPDirectiveEnd))
            {
                if (token.Kind is TokenKind.DotDotDot)
                {
                    macroData.IsVariadic = true;
                    token = ReadTokenRaw();
                    break;
                }
                else if (token.Kind is TokenKind.CPPIdentifier)
                {
                    var parameterName = token.StringValue;
                    if (macroData.ParameterNames.Contains(parameterName))
                        Context.ErrorDuplicateMacroParameter(token.Source, token.Location);

                    macroData.ParameterNames.Add(parameterName);
                    token = ReadTokenRaw();

                    if (token.Kind == TokenKind.Comma)
                        token = ReadTokenRaw();
                    else break;
                }
                else Context.ErrorExpectedToken(token.Source, token.Location, "an identifier");
            }

            if (token.Kind is not TokenKind.CloseParen)
                Context.ErrorExpectedMatchingCloseDelimiter(token.Source, '(', ')', token.Location, openParenToken.Location);

            if (token.Kind is not (TokenKind.EndOfFile or TokenKind.CPPDirectiveEnd))
                token = ReadTokenRaw();
        }

        while (token.Kind is not (TokenKind.EndOfFile or TokenKind.CPPDirectiveEnd))
        {
            if (token.Kind is TokenKind.CPPIdentifier)
            {
                if (token.StringValue == "__VA_ARGS__")
                    token.Kind = TokenKind.CPPVAArgs;
                else if (token.StringValue == "__VA_OPT__")
                {
                    token.Kind = TokenKind.CPPVAOpt;
                    if (!LanguageOptions.CIsC23)
                        Context.ExtVAOpt(token.Source, token.Location);
                }
                else if (macroData.ParameterNames.Contains(token.StringValue))
                    token.Kind = TokenKind.CPPMacroParam;
            }

            if (!macroData.IsVariadic && token.Kind is TokenKind.CPPVAArgs or TokenKind.CPPVAOpt)
                Context.ErrorVariadicTokenInNonVariadicMacro(token);

            if (token.Kind is TokenKind.HashHash && macroData.Tokens.Count != 0 && macroData.Tokens[^1].Kind == TokenKind.HashHash)
                Context.ErrorAdjacentConcatenationTokens(token);

            macroData.Tokens.Add(token);
            token = ReadTokenRaw();
        }

        for (int i = 0; i < macroData.Tokens.Count; )
        {
            switch (macroData.Tokens[i++].Kind)
            {
                case TokenKind.Hash: DefineDirectiveCheckHash(macroData, i); break;
                case TokenKind.HashHash: DefineDirectiveCheckHashHash(macroData, i); break;
                case TokenKind.CPPVAOpt: token.CPPIntegerData = FindVAOptCloseParen(macroData, ref i); break;
            }
        }

        _macroDefs[macroData.Name] = new PreprocessorMacroDefinition(macroData.Name, macroData.Tokens, macroData.ParameterNames)
        {
            IsFunctionLike = macroData.IsFunctionLike,
            IsVariadic = macroData.IsVariadic,
            RequiresPasting = macroData.RequiresPasting,
        };
    }

    private void HandleIncludeDirective(SourceLanguage language, Token preprocessorToken, Token directiveToken)
    {
        var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

        Token includePathToken;
        using (var _ = lexer.PushMode(SourceLanguage.C, lexer.State | LexerState.CPPHasHeaderNames))
            includePathToken = ReadTokenRaw();

        if (SkipRemainingDirectiveTokens())
            Context.WarningExtraTokensAtEndOfDirective(directiveToken);

        if (includePathToken.Kind is not (TokenKind.LiteralString or TokenKind.CPPHeaderName))
        {
            Context.ErrorExpectedToken(includePathToken.Source, includePathToken.Location, "a header name");
            return;
        }

        var includeSource = Context.GetSourceTextForFilePath(
            (string)includePathToken.StringValue,
            includePathToken.Kind == TokenKind.CPPHeaderName ? IncludeKind.System : IncludeKind.Local,
            includePathToken.Source.Name
        );

        if (includeSource is null)
        {
            Context.ErrorCannotOpenSourceFile(includePathToken.Source, includePathToken.Location, includePathToken.StringValue);
            return;
        }

        if (language == SourceLanguage.C)
            AddCIncludeLexerFromC(includeSource);
        else AddCIncludeLexerFromLaye(includeSource, preprocessorToken);
    }

    private void DefineDirectiveCheckHash(ParsedMacroData macroData, int tokenIndex)
    {
        if (macroData.IsFunctionLike)
        {
            SourceLocation diagnosticLocation;
            if (tokenIndex < macroData.Tokens.Count)
            {
                var token = macroData.Tokens[tokenIndex];
                if (token.Kind is TokenKind.CPPVAOpt or TokenKind.CPPVAArgs or TokenKind.CPPMacroParam)
                    return;

                diagnosticLocation = token.Range.End + 1;
            }
            else diagnosticLocation = macroData.Tokens[^1].Range.End + 1;

            Context.ErrorExpectedMacroParamOrVAOptAfterHash(macroData.Tokens[0].Source, diagnosticLocation);
        }
    }

    private void DefineDirectiveCheckHashHash(ParsedMacroData macroData, int tokenIndex)
    {
        macroData.RequiresPasting = true;
        // Note that the index is one-past the current token.
        if (tokenIndex == 1)
            Context.ErrorConcatenationTokenCannotStartMacro(macroData.Tokens[tokenIndex - 1]);
        else if (tokenIndex >= macroData.Tokens.Count)
            Context.ErrorConcatenationTokenCannotEndMacro(macroData.Tokens[tokenIndex - 1]);
    }

    private int FindVAOptCloseParen(ParsedMacroData macroData, ref int tokenIndex)
    {
        var token = SafeGetToken(tokenIndex);
        if (token.Kind != TokenKind.OpenParen)
        {
            Context.ErrorExpectedToken(token.Source, token.Location, "'('");
            return -1;
        }

        var openParenToken = token;
        tokenIndex++;
        
        token = SafeGetToken(tokenIndex);
        if (token.Kind == TokenKind.HashHash)
            Context.ErrorConcatenationTokenCannotStartVAOpt(token);

        int parenNesting = 1;
        while (tokenIndex < macroData.Tokens.Count)
        {
            token = macroData.Tokens[tokenIndex];
            tokenIndex++;

            switch (token.Kind)
            {
                default: break;

                case TokenKind.OpenParen: parenNesting++; break;
                case TokenKind.CloseParen:
                {
                    parenNesting--;
                    if (parenNesting == 0)
                        return tokenIndex - 1;
                } break;

                case TokenKind.CPPVAOpt: Context.ErrorVAOptCannotBeNested(token); break;
                case TokenKind.Hash: DefineDirectiveCheckHash(macroData, tokenIndex); break;

                case TokenKind.HashHash:
                {
                    DefineDirectiveCheckHashHash(macroData, tokenIndex);
                    if (parenNesting == 1 && SafeGetToken(tokenIndex).Kind == TokenKind.CloseParen)
                        Context.ErrorConcatenationTokenCannotEndVAOpt(token);
                } break;
            }
        }

        Context.ErrorExpectedMatchingCloseDelimiter(token.Source, '(', ')', token.Range.End + 1, openParenToken.Location);
        return -1;

        Token SafeGetToken(int index)
        {
            return index < macroData.Tokens.Count ? macroData.Tokens[index] : Token.AnyEndOfFile;
        }
    }

    #endregion
}
