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

    public bool RequiresPasting { get; } = tokens.Any(t => t.Kind == TokenKind.HashHash);

    public bool IsExpanding { get; set; } = false;
}

public sealed class Preprocessor(CompilerContext context, LanguageOptions languageOptions)
{
    private CompilerContext Context { get; } = context;
    private LanguageOptions LanguageOptions { get; } = languageOptions;

    public Token[] PreprocessSource(SourceText source, SourceLanguage language)
    {
        AddLexerForSourceText(source, language);

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

    private void AddLexerForSourceText(SourceText source, SourceLanguage language)
    {
        MaterializedPeekedToken();
        _tokenStreams.Push(new LexerTokenStream(new(Context, source, LanguageOptions, language)));
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
            using (var lexerPreprocessorState = lexer.PushMode(lexer.State | LexerState.CPPWithinDirective))
            {
                var directiveToken = ReadTokenRaw();
                DispatchDirectiveParser(SourceLanguage.C, directiveToken);
            }

            return Preprocess(ReadTokenRaw());
        }

        if (ppToken.Language == SourceLanguage.Laye && ppToken is { Kind: TokenKind.KWPragma or TokenKind.Hash, IsAtStartOfLine: true } && IsLexingFile)
        {
            if (ppToken.Kind == TokenKind.Hash)
                Context.ErrorCStylePreprocessingDirective(ppToken.Source, ppToken.Location);

            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;
            using (var lexerPreprocessorState = lexer.PushMode(SourceLanguage.C, lexer.State | LexerState.CPPWithinDirective))
            {
                var directiveToken = ReadTokenRaw();
                if (directiveToken.Kind == TokenKind.LiteralString && directiveToken.StringValue == "C")
                {
                    Context.Todo("`pragma \"C\"` in Laye source files.");
                    throw new UnreachableException();
                }

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

            if (expansion.SourceToken.Language == SourceLanguage.Laye)
            {
                expansion.Expansion.Insert(0, new Token(TokenKind.KWPragma, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                {
                    Spelling = "pragma",
                    LeadingTrivia = expansion.SourceToken.LeadingTrivia,
                });

                expansion.Expansion.Insert(1, new Token(TokenKind.LiteralString, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                {
                    Spelling = "\"C\"",
                    StringValue = "C",
                    HasWhiteSpaceBefore = true,
                });

                expansion.Expansion.Insert(2, new Token(TokenKind.OpenParen, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                {
                    Spelling = "(",
                    HasWhiteSpaceBefore = true,
                });

                expansion.Expansion.Add(new Token(TokenKind.CloseParen, SourceLanguage.Laye, expansion.SourceToken.Source, expansion.SourceToken.Range)
                {
                    Spelling = ")",
                    TrailingTrivia = expansion.SourceToken.TrailingTrivia,
                    HasWhiteSpaceBefore = true,
                });
            }

            var firstToken = expansion.Expansion[0];
            firstToken.IsAtStartOfLine = isAtStartOfLine;
            firstToken.HasWhiteSpaceBefore = hasWhitespaceBefore;
            _tokenStreams.Push(new BufferTokenStream(expansion.Expansion, expansion.MacroDefinition));
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
                    if (!LanguageOptions.CIsGNUMode && !LanguageOptions.CIsC23)
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

        // TODO(local): process the replacement list

        _macroDefs[macroData.Name] = new PreprocessorMacroDefinition(macroData.Name, macroData.Tokens, macroData.ParameterNames)
        {
            IsFunctionLike = macroData.IsFunctionLike,
            IsVariadic = macroData.IsVariadic,
        };

        //if (SkipRemainingDirectiveTokens())
        //    Context.WarningExtraTokensAtEndOfDirective(directiveToken.Source, directiveToken.Location, directiveToken.StringValue);
    }

    #endregion
}
