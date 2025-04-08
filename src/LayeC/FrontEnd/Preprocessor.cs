using System.Diagnostics;

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

        return [.. tokens];
    }

    #region Implementation

    private readonly Stack<ITokenStream> _tokenStreams = [];
    private Token? _peekedRawToken;

    private readonly Dictionary<StringView, PreprocessorMacroDefinition> _macroDefs = [];

    private bool IsLexingFile => _peekedRawToken is null && _tokenStreams.TryPeek(out var ts) && ts is LexerTokenStream;

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
        _tokenStreams.Push(tokenStream);
        MaterializedPeekedToken();
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
                IsAtStartOfLine = true,
                HasWhiteSpaceBefore = false,
            },
            new Token(TokenKind.LiteralString, SourceLanguage.Laye, layeSourceToken.Source, layeSourceToken.Range)
            {
                Spelling = "\"C\"",
                StringValue = "C",
                HasWhiteSpaceBefore = true,
            },
            new Token(TokenKind.OpenCurly, SourceLanguage.Laye, layeSourceToken.Source, layeSourceToken.Range)
            {
                Spelling = "{",
                HasWhiteSpaceBefore = true,
            }
        ];

        Token[] trailingTokens = [
            new Token(TokenKind.CloseCurly, SourceLanguage.Laye, layeSourceToken.Source, layeSourceToken.Range)
            {
                Spelling = "}",
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

    private void MaterializedPeekedToken()
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
        return Preprocess(ppToken);
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

    private Token Preprocess(Token ppToken)
    {
        if (ppToken.Language == SourceLanguage.C && ppToken is { Kind: TokenKind.Hash, IsAtStartOfLine: true } && IsLexingFile)
        {
            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

            var directiveToken = ReadTokenRaw();
            using (var lexerPreprocessorState = lexer.PushMode(lexer.State | LexerState.CPPWithinDirective))
            {
                DispatchDirectiveParser(SourceLanguage.C, directiveToken);
            }

            return Preprocess(ReadTokenRaw());
        }

        if (ppToken.Language == SourceLanguage.Laye && ppToken is { Kind: TokenKind.KWPragma or TokenKind.Hash, IsAtStartOfLine: true } && IsLexingFile)
        {
            if (ppToken.Kind == TokenKind.Hash)
                Context.ErrorCStylePreprocessingDirective(ppToken.Source, ppToken.Location);

            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

            var directiveToken = ReadTokenRaw();
            if (directiveToken.Kind == TokenKind.LiteralString && directiveToken.StringValue == "C")
            {
                Context.Todo("`pragma \"C\"` in Laye source files.");
                throw new UnreachableException();
            }

            using (var lexerPreprocessorState = lexer.PushMode(SourceLanguage.C, lexer.State | LexerState.CPPWithinDirective))
            {
                DispatchDirectiveParser(SourceLanguage.Laye, directiveToken);
            }

            return Preprocess(ReadTokenRaw());
        }

        if (ppToken.Kind == TokenKind.CPPIdentifier && MaybeExpandMacro(ppToken))
            return Preprocess(ReadTokenRaw());

        // TODO(local): concat adjacent string literals in C?

        return ppToken;

        void DispatchDirectiveParser(SourceLanguage language, Token directiveToken)
        {
            Context.Assert(IsLexingFile, "Can only handle C preprocessor directives when lexing a source file.");
            Context.Assert(Language == SourceLanguage.C, "Can only handle C preprocessor directives if the lexer was switched to the C language mode.");

            directiveToken.Language = SourceLanguage.C;

            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;
            Context.Assert(0 != (lexer.State & LexerState.CPPWithinDirective), "Can only handle C preprocessor directives if the lexer state has been switched to include CPPWithinDirective.");

            if (directiveToken.Kind != TokenKind.CPPIdentifier)
            {
                Context.ErrorExpectedPreprocessorDirective(directiveToken.Source, directiveToken.Location);
                SkipRemainingDirectiveTokens();
            }
            else HandleDirective(language, directiveToken);
        }
    }

    private sealed class MacroExpansion
    {
        public required PreprocessorMacroDefinition MacroDefinition { get; init; }
        public required Token SourceToken { get; init; }

        public List<Token> Expansion { get; } = [];
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
                Context.Todo("Pasting...");
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

            ITokenStream tokenStream = new BufferTokenStream(expansion.Expansion, expansion.MacroDefinition);
            if (expansion.SourceToken.Language == SourceLanguage.Laye)
            {
                Token[] leadingTokens = [
                    new Token(TokenKind.KWPragma, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                    {
                        Spelling = "pragma",
                        LeadingTrivia = expansion.SourceToken.LeadingTrivia,
                        IsAtStartOfLine = isAtStartOfLine,
                        HasWhiteSpaceBefore = hasWhitespaceBefore,
                    },
                    new Token(TokenKind.LiteralString, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                    {
                        Spelling = "\"C\"",
                        StringValue = "C",
                        HasWhiteSpaceBefore = true,
                    },
                    new Token(TokenKind.OpenParen, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                    {
                        Spelling = "(",
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
            }

            _tokenStreams.Push(tokenStream);
        }
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

    private void HandleDirective(SourceLanguage language, Token directiveToken)
    {
        Context.Assert(directiveToken.Kind == TokenKind.CPPIdentifier, "Can only handle identifier directives.");

        var directiveName = directiveToken.StringValue;
        if (directiveName == "define")
            HandleDefineDirective(directiveToken);
        else if (directiveName == "include")
            HandleIncludeDirective(language, directiveToken);
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

    private void HandleDefineDirective(Token directiveToken)
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

    private void HandleIncludeDirective(SourceLanguage language, Token directiveToken)
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
        else AddCIncludeLexerFromLaye(includeSource, directiveToken);
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
