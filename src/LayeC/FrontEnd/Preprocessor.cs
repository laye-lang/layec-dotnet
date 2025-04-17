using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

using LayeC.Diagnostics;
using LayeC.Source;

namespace LayeC.FrontEnd;

public enum PreprocessorMode
{
    /// <summary>
    /// The default mode of the preprocessor, supports all features for all languages.
    /// </summary>
    Full,

    /// <summary>
    /// A minimal mode, for use when reading module header information from Laye source files.
    /// Alternatively, this is useful to explicitly *not* preprocess a source file due to user input, e.g. when the source is already the result of preprocessing.
    ///
    /// Disables macro expansions and directive parsing entirely, but maintains the language switching of 'pragma "C"` in Laye.
    /// Again, remember that any embedded C code will not be preprocessed, only lexed and converted to valid tokens without preprocessing.
    /// </summary>
    Minimal,
}

public sealed class PreprocessorMacroDefinition(Token nameToken, IEnumerable<Token> tokens, IEnumerable<StringView> parameterNames)
{
    public Token NameToken { get; } = nameToken;
    public StringView Name => NameToken.StringValue;
    public IReadOnlyList<Token> Tokens { get; } = [.. tokens];
    public IReadOnlyList<StringView> ParameterNames { get; } = [.. parameterNames];

    public bool IsVariadic { get; init; }
    public bool IsFunctionLike { get; init; }
    public bool RequiresPasting { get; init; }

    public bool IsExpanding { get; set; } = false;
}

public sealed class Preprocessor
{
    public static string StringizeToken(Token token, bool escape)
    {
        var builder = new StringBuilder();
        StringizeToken(builder, token, escape);
        return builder.ToString();
    }

    public static void StringizeToken(StringBuilder builder, Token token, bool escape)
    {
        switch (token.Kind)
        {
            //default: throw new ArgumentException($"Unexpected token kind: {token.Kind}");
            default: builder.Append((string)token.Spelling); break;

            case TokenKind.Invalid: builder.Append("<INVALID>"); break;
            case TokenKind.UnexpectedCharacter: builder.Append("<?>"); break;

            case TokenKind.EndOfFile: builder.Append("<EOF>"); break;

            case TokenKind.CPPIdentifier: builder.Append((string)token.Spelling); break;
            case TokenKind.CPPNumber: builder.Append((string)token.Spelling); break;
            case TokenKind.CPPVAOpt: builder.Append("__VA_OPT__"); break;
            case TokenKind.CPPVAArgs: builder.Append("__VA_ARGS__"); break;
            case TokenKind.CPPMacroParam: throw new ArgumentException("Macro parameter names should have already been replaced.");
            case TokenKind.CPPHeaderName: throw new ArgumentException("Cannot stringize a header name token.");
            case TokenKind.CPPDirectiveEnd: throw new ArgumentException("Cannot stringize a directive end token.");
            case TokenKind.CPPLayeMacroWrapperIdentifier: builder.Append((string)token.Spelling); break;

            case TokenKind.Identifier: builder.Append((string)token.Spelling); break;
            case TokenKind.LiteralInteger: builder.Append((string)token.Spelling); break;
            case TokenKind.LiteralFloat: builder.Append((string)token.Spelling); break;

            case TokenKind.LiteralCharacter:
            {
                if (escape)
                    StringizeString(builder, token.StringValue, '\'');
                else
                {
                    builder.Append('\'');
                    builder.Append((string)token.StringValue);
                    builder.Append('\'');
                }
            } break;

            case TokenKind.LiteralString:
            {
                if (escape)
                    StringizeString(builder, token.StringValue, '"');
                else
                {
                    builder.Append('"');
                    builder.Append((string)token.StringValue);
                    builder.Append('"');
                }
            } break;
        }
    }

    private static void StringizeString(StringBuilder builder, StringView str, char delim)
    {
        builder.Append('"');

        if (delim == '"')
            builder.Append("\\\"");
        else builder.Append('\'');

        foreach (char c in str)
        {
            if (c is '"' or '\\')
                builder.Append('\\');
            builder.Append(c);
        }

        if (delim == '"')
            builder.Append("\\\"");
        else builder.Append('\'');

        builder.Append('"');
    }

    private static Token StringizeTokens(IReadOnlyList<Token> tokens, Token hashToken, Token sourceToken)
    {
        var builder = new StringBuilder();

        bool first = true;
        foreach (var token in tokens)
        {
            if (first)
                first = false;
            else if (token.HasWhiteSpaceBefore)
                builder.Append(' ');
            StringizeToken(builder, token, true);
        }

        string stringValue = builder.ToString();
        return new Token(TokenKind.LiteralString, SourceLanguage.C, hashToken.Source, hashToken.Range)
        {
            Spelling = stringValue,
            StringValue = stringValue,
            LeadingTrivia = hashToken.LeadingTrivia,
            TrailingTrivia = sourceToken.TrailingTrivia,
            HasWhiteSpaceBefore = hashToken.HasWhiteSpaceBefore,
        };
    }

    public CompilerContext Context { get; }
    public LanguageOptions LanguageOptions { get; }
    // TODO(local): I don't actually like how the current implementation of "Minimal" works.
    // instead of *disabling* directives, it should instead read all their tokens (according to their same rules) and return them through buffer token streams.
    // (I think).
    // This way, all tokens are preserved in the output in case it's necessary.
    // For now, this is irrelevant.
    public PreprocessorMode Mode { get; }

    public SourceLanguage Language
    {
        get
        {
            if (_peekedRawToken is { } p)
                return p.Language;
            return _tokenStreams.TryPeek(out var ts) ? ts.Language : SourceLanguage.None;
        }
    }

    public bool IsAtEnd => _tokenStreams.Count == 0;

    private delegate List<Token> BuiltInMacroFunction(Token sourceToken, Token builtInToken);
    private readonly Dictionary<StringView, BuiltInMacroFunction> _builtInMacros;
    private readonly List<StringView> _builtInPPExpressionMacros;
    private readonly List<StringView> _reservedPPNames;

    public Preprocessor(CompilerContext context, LanguageOptions languageOptions, PreprocessorMode mode = PreprocessorMode.Full)
    {
        Context = context;
        LanguageOptions = languageOptions;
        Mode = mode;

        _builtInMacros = new()
        {
            { "__FILE__", HandleFileBuiltInMacro },
            { "__LINE__", HandleLineBuiltInMacro },
        };

        _builtInPPExpressionMacros = ["__has_feature", "__has_include", "__has_include_next"];

        // NOTE(nic): consider adding `__DATE__` and `__TIME__` (and others like that) later
        _reservedPPNames = new()
        {
            "__FILE__",
            "__LINE__",
            "__VA_ARGS__",
            "__VA_OPT__",
            "__has_feature",
            "__has_include",
            "__has_include_next",
            "defined",
        };
    }

    private List<Token> HandleFileBuiltInMacro(Token sourceToken, Token builtInToken)
    {
        var spellingBuilder = new StringBuilder();
        StringizeString(spellingBuilder, sourceToken.Source.Name, '\"');

        List<Token> tokens = [
            new Token(TokenKind.LiteralString, SourceLanguage.C, builtInToken.Source, builtInToken.Range)
            {
                Spelling = spellingBuilder.ToString(),
                StringValue = sourceToken.Source.Name,
                LeadingTrivia = builtInToken.LeadingTrivia,
                TrailingTrivia = builtInToken.TrailingTrivia,
            },
        ];

        return tokens;
    }

    private List<Token> HandleLineBuiltInMacro(Token sourceToken, Token builtInToken)
    {
        var locInfoShort = sourceToken.Source.SeekLineColumn(sourceToken.Location);

        List<Token> tokens = [
            new Token(TokenKind.CPPNumber, SourceLanguage.C, builtInToken.Source, builtInToken.Range)
            {
                Spelling = locInfoShort.Line.ToString(),
                LeadingTrivia = builtInToken.LeadingTrivia,
                TrailingTrivia = builtInToken.TrailingTrivia,
            },
        ];

        return tokens;
    }

    public Token[] Preprocess()
    {
        var tokens = new List<Token>();
        while (!IsAtEnd)
        {
            var token = ReadToken();
            // TODO(local): concat adjacent string literals in C
            tokens.Add(token);

            if (token.Kind == TokenKind.EndOfFile)
            {
                Context.Assert(IsAtEnd, "Should have only returned the final EOF token if all token streams are empty.");
                break;
            }
        }

        Context.Diag.Flush();
        return [.. tokens];
    }

    public void PushSourceTokenStream(SourceText source)
    {
        //Context.Assert(_tokenStreams.Count == 0, "There should be no token streams left when preprocessing a new source file.");
        PushTokenStream(new LexerTokenStream(new(Context, source, LanguageOptions)));
    }

    public Token ReadToken()
    {
        var ppToken = ReadAndExpandToken();
        return ConvertPPToken(ppToken);
    }

    #region Implementation

    private readonly Stack<ITokenStream> _tokenStreams = [];
    private Token? _peekedRawToken;

    private readonly Dictionary<StringView, PreprocessorMacroDefinition> _macroDefs = [];

    private bool _withinExpressionPragmaC = false;
    private bool IsInLexer => _peekedRawToken is null && _tokenStreams.TryPeek(out var ts) && ts is LexerTokenStream;
    private bool ArePreprocessorDirectivesAllowed => !_withinExpressionPragmaC && Mode == PreprocessorMode.Full && IsInLexer;

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
        Context.Assert(source.Language == SourceLanguage.C, "Only C files accepted here.");
        PushTokenStream(new LexerTokenStream(new(Context, source, LanguageOptions)));
    }

    private void AddCIncludeLexerFromLaye(SourceText source, Token layeSourceToken)
    {
        Context.Assert(source.Language == SourceLanguage.C, "Only C files accepted here.");

        Token[] leadingTokens = [
            new Token(TokenKind.KWPragma, SourceLanguage.Laye, layeSourceToken.Source, layeSourceToken.Range)
            {
                Spelling = "pragma",
                LeadingTrivia = layeSourceToken.LeadingTrivia,
                TrailingTrivia = new([new TriviumLiteral(" ")], false),
                IsAtStartOfLine = true,
                DisableExpansion = true,
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
        var lexerStream = new LexerTokenStream(new(Context, source, LanguageOptions));
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
                while (lexerStream.Lexer.PreprocessorIfDepth > 0)
                {
                    var state = lexerStream.Lexer.PreprocessorIfStates.Pop();
                    Context.ErrorUnclosedConditionalDirective(state.IfDirectiveToken);
                }
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

    private Token ReadAndExpandToken()
    {
        // eliminate recursion at all costs
        while (true)
        {
            var ppToken = ReadTokenRaw();
            if (Preprocess(ppToken) is { } preprocessedToken)
                return preprocessedToken;
        }
    }

    private Token ReadTokenRaw()
    {
        var ppToken = ReadTokenRawImpl();
        if (ppToken.Kind == TokenKind.CPPIdentifier)
        {
            if (_macroDefs.TryGetValue(ppToken.StringValue, out var macroDef) && macroDef.IsExpanding)
                ppToken.DisableExpansion = true;
        }

        return ppToken;
    }

    private Token ReadTokenRawImpl()
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

    /// <summary>
    /// Returns null when this token resulted in some form of preprocessor expansion, else the token.
    /// </summary>
    private Token? Preprocess(Token ppToken)
    {
        if (ppToken.Language == SourceLanguage.C && ppToken is { Kind: TokenKind.Hash, IsAtStartOfLine: true } && ArePreprocessorDirectivesAllowed)
        {
            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

            using (lexer.PushMode(SourceLanguage.C, lexer.State | LexerState.CPPWithinDirective))
            {
                var directiveToken = ReadTokenRaw();
                DispatchDirectiveParser(SourceLanguage.C, directiveToken);
            }

            return null;
        }

        if (ppToken.Language == SourceLanguage.C && ppToken is { Kind: TokenKind.Hash, IsAtStartOfLine: true } && _withinExpressionPragmaC)
        {
            Context.WarningPotentialPreprocessorDirectiveInPragmaCExpression(ppToken);
        }

        if (ppToken.Language == SourceLanguage.Laye && ppToken is { Kind: TokenKind.KWPragma or TokenKind.Hash, DisableExpansion: false, IsAtStartOfLine: true })
        {
            if (ArePreprocessorDirectivesAllowed)
            {
                if (ppToken.Kind == TokenKind.Hash)
                    Context.ErrorCStylePreprocessingDirective(ppToken.Source, ppToken.Location);

                var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

                Token directiveToken;
                using (lexer.PushMode(SourceLanguage.C, lexer.State | LexerState.CPPWithinDirective))
                    directiveToken = ReadTokenRaw();

                if (directiveToken.Kind == TokenKind.LiteralString && directiveToken.StringValue == "C")
                {
                    return HandlePragmaC(false, lexer, ppToken, directiveToken);
                }
                else
                {
                    using (lexer.PushMode(SourceLanguage.C, lexer.State | LexerState.CPPWithinDirective))
                        DispatchDirectiveParser(SourceLanguage.Laye, directiveToken);
                    return null;
                }
            }
            else
            {
                var directiveToken = NextRawPPToken;
                if (directiveToken.Kind == TokenKind.LiteralString && directiveToken.StringValue == "C")
                {
                    var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;
                    directiveToken = ReadTokenRaw();
                    return HandlePragmaC(false, lexer, ppToken, directiveToken);
                }
            }
        }

        if (ppToken.Language == SourceLanguage.Laye && ppToken is { Kind: TokenKind.KWPragma, DisableExpansion: false } && IsInLexer && NextRawPPToken.Kind == TokenKind.LiteralString && NextRawPPToken.StringValue == "C")
        {
            var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;
            var directiveToken = ReadTokenRaw();
            return HandlePragmaC(true, lexer, ppToken, directiveToken);
        }

        if (ppToken.Kind == TokenKind.CPPIdentifier && Mode == PreprocessorMode.Full && MaybeExpandMacro(ppToken))
            return null;

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
                SkipRemainingDirectiveTokens(directiveToken);
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
        public bool Stringize { get; set; }
        public Token StringizeToken { get; set; } = Token.AnyEndOfFile;
        public bool PasteTokens { get; set; }
        public bool EndsWithPlacemarker { get; set; }
        public bool DidPaste { get; set; }

        public void Reset()
        {
            StartOfExpansion = 0;
            Index = -1;
            CloseParenIndex = 0;
            StringizeToken = Token.AnyEndOfFile;
            Stringize = false;
            PasteTokens = false;
            EndsWithPlacemarker = false;
            DidPaste = false;
        }

        public void DeferStringize(Token hashToken)
        {
            Debug.Assert(!Stringize);
            Stringize = true;
            StringizeToken = hashToken;
        }
    }

    private sealed class MacroExpansion
    {
        public required PreprocessorMacroDefinition MacroDefinition { get; init; }
        public required Token SourceToken { get; init; }

        public Token[][] Arguments { get; init; } = [];
        public List<Token>?[]? ExpandedArguments { get; set; }

        public List<Token> Expansion { get; } = [];
        public int Cursor { get; set; }
        public Token MacroPPTokenAtCursor => MacroDefinition.Tokens[Cursor];

        public VAOptState VAOptState { get; } = new();
        public bool InVAOpt => VAOptState.Index >= 0;

        public bool InsertWhiteSpace { get; set; }
        public bool PasteBefore { get; set; }

        public int GetParamIndex(Token token)
        {
            Debug.Assert(token.Kind is TokenKind.CPPMacroParam or TokenKind.CPPVAArgs);
            if (token.Kind == TokenKind.CPPVAArgs)
                return MacroDefinition.ParameterNames.Count;
            Debug.Assert(MacroDefinition.ParameterNames.Any(n => n == token.StringValue));
            return MacroDefinition.ParameterNames.Index().First(pair => pair.Item == token.StringValue).Index;
        }

        public bool HasVariadicArguments(Preprocessor pp)
        {
            IReadOnlyList<Token> param = pp.Substitute(this, MacroDefinition.ParameterNames.Count);
            return param.Count != 0;
        }

        public void Append(Preprocessor pp, Token token)
        {
            if (PasteBefore)
            {
                PasteBefore = false;
                pp.Paste(this, token, token);
                return;
            }

            if (InsertWhiteSpace)
            {
                InsertWhiteSpace = false;
                token.HasWhiteSpaceBefore = true;
            }

            Expansion.Add(token.Clone());
        }

        public void Append(Preprocessor pp, IEnumerable<Token> tokens, Token expandedFromToken)
        {
            if (expandedFromToken.HasWhiteSpaceBefore)
                InsertWhiteSpace = true;

            if (!tokens.Any())
            {
                if (Expansion.Count > 0)
                    Expansion[^1].TrailingTrivia = new([.. Expansion[^1].TrailingTrivia.Trivia, .. expandedFromToken.TrailingTrivia.Trivia], false);
                Placemarker(expandedFromToken);
            }
            else
            {
                foreach (var token in tokens)
                    Append(pp, token);
            }
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public void Placemarker(Token trailingToken)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            // TODO(local): see if we can use this trailing token anywhere in the placemarker for trailing trivia
            if (PasteBefore)
                PasteBefore = false;
            else if (Cursor < MacroDefinition.Tokens.Count && MacroPPTokenAtCursor.Kind == TokenKind.HashHash)
                Cursor++;
            else if (InVAOpt && Cursor == VAOptState.CloseParenIndex)
                VAOptState.EndsWithPlacemarker = true;
        }
    }

    private IReadOnlyList<Token> Substitute(MacroExpansion expansion, int parameterIndex)
    {
        // p4: 'the replacement preprocessing tokens are the preprocessing tokens
        // of the corresponding argument after all macros contained therein have been
        // expanded.'

        var argTokens = expansion.Arguments[parameterIndex];
        if (!argTokens.Any(t => t.Kind == TokenKind.CPPIdentifier && GetExpandableMacro(t.StringValue) is not null))
            return argTokens;

        expansion.ExpandedArguments ??= new List<Token>?[expansion.MacroDefinition.ParameterNames.Count + (expansion.MacroDefinition.IsVariadic ? 1 : 0)];
        var expandedArgs = expansion.ExpandedArguments[parameterIndex];
        if (expandedArgs is not null)
            return expandedArgs;

        expansion.ExpandedArguments[parameterIndex] = expandedArgs = [];

        // p4: 'The argument’s preprocessing tokens are completely macro replaced before
        // being substituted as if they formed the rest of the preprocessing file with no
        // other preprocessing tokens being available.'
        PushTokenStream(new BufferTokenStream([.. argTokens]) { KeepWhenEmpty = true });
        while (true)
        {
            var token = ReadAndExpandToken();
            if (token.Kind == TokenKind.EndOfFile)
                break;
            expandedArgs.Add(token);
        }

        PopTokenStream();
        return expandedArgs;
    }

    private PreprocessorMacroDefinition? GetExpandableMacro(StringView name)
    {
        if (_macroDefs.TryGetValue(name, out var def) && !def.IsExpanding)
            return def;
        else return null;
    }

    private bool IsBuiltInMacroName(StringView macroName)
    {
        return _builtInMacros.ContainsKey(macroName) || _builtInPPExpressionMacros.Contains(macroName);
    }

    private bool IsMacroDefined(StringView macroName)
    {
        if (IsBuiltInMacroName(macroName))
            return true;

        return _macroDefs.ContainsKey(macroName);
    }

    private bool MaybeExpandMacro(Token ppToken, bool parenMustBeDirectlyAdjacent = true)
    {
        Context.Assert(ppToken.Kind == TokenKind.CPPIdentifier, "Can only attempt to expand C preprocessor identifiers.");

        if (ppToken.DisableExpansion)
            return false;

        if (_builtInMacros.TryGetValue(ppToken.StringValue, out var macroFunction))
        {
            List<Token> tokens = macroFunction(ppToken, ppToken);
            PushTokenStream(new BufferTokenStream(tokens));
            return true;
        }

        var leadingTrivia = ppToken.LeadingTrivia;
        var macroDef = GetExpandableMacroAndArguments(ppToken.StringValue, out var arguments, out var trailingTrivia);
        if (macroDef is null || macroDef.IsExpanding)
            return false;

        if (macroDef.IsFunctionLike)
        {
            if (macroDef.Tokens.Count == 0)
                return true;

            Context.Assert(arguments.Length >= 0, "Should always be at least one argument list.");

            bool tryToExpand = true;
            if (!macroDef.IsVariadic)
            {
                bool isValidIfZeroArguments = arguments.Length == 1 && macroDef.ParameterNames.Count == 0;
                if (arguments.Length != macroDef.ParameterNames.Count && !isValidIfZeroArguments)
                {
                    Context.ErrorIncorrectArgumentCountForFunctionLikeMacro(ppToken, macroDef.NameToken, isTooFew: arguments.Length < macroDef.ParameterNames.Count);
                    tryToExpand = false;
                }
            }
            else
            {
                if (arguments.Length < macroDef.ParameterNames.Count)
                {
                    Context.ErrorIncorrectArgumentCountForFunctionLikeMacro(ppToken, macroDef.NameToken, isTooFew: true);
                    tryToExpand = false;
                }
                else if (arguments.Length == macroDef.ParameterNames.Count && !LanguageOptions.CIsC23)
                {
                    Context.ExtZeroVAArgs(ppToken.Source, ppToken.Location);
                    // tryToExpand = false;
                }
            }

            // should have generated an error; we parsed all the necessary tokens, but won't be able to reliably expand this.
            if (!tryToExpand)
                return true;

            var expansion = new MacroExpansion()
            {
                MacroDefinition = macroDef,
                SourceToken = ppToken,
                Arguments = arguments,
            };

            while (expansion.Cursor < macroDef.Tokens.Count)
            {
                var token = expansion.MacroPPTokenAtCursor;

                // Skip '__VA_OPT__(' and mark that we're inside of __VA_OPT__.
                if (token.Kind == TokenKind.CPPVAOpt)
                {
                    expansion.VAOptState.Index = expansion.Cursor;
                    expansion.VAOptState.CloseParenIndex = token.VAOptCloseParenIndex;
                    expansion.VAOptState.StartOfExpansion = expansion.Expansion.Count;
                    expansion.Cursor++; // yeet '__VA_OPT__'
                    expansion.Cursor++; // yeet '('

                    // If __VA_ARGS__ expands to nothing, discard everything up
                    // to, but NOT including, the closing rparen.
                    if (!expansion.HasVariadicArguments(this))
                    {
                        while (expansion.Cursor < expansion.VAOptState.CloseParenIndex)
                            expansion.Cursor++;

                        // Per C2y 6.10.5.2p7, the __VA_OPT__ parameter expands to
                        // a single placemarker in this case.
                        expansion.VAOptState.EndsWithPlacemarker = true;

                        Context.Assert(expansion.MacroPPTokenAtCursor.Kind == TokenKind.CloseParen, token.Source, token.Location, "This should be the closing paren of the __VA_OPT__.");
                    }

                    token = expansion.MacroPPTokenAtCursor;
                }

                if (expansion.InVAOpt && expansion.Cursor == expansion.VAOptState.CloseParenIndex)
                {
                    var closeParenToken = expansion.MacroDefinition.Tokens[expansion.VAOptState.CloseParenIndex];

                    expansion.Cursor++;
                    Context.Assert(!expansion.VAOptState.PasteTokens || expansion.VAOptState.Stringize, "Only paste tokens here if we're also stringizing.");

                    if (expansion.VAOptState.Stringize)
                    {
                        var tokens = expansion.Expansion[expansion.VAOptState.StartOfExpansion..expansion.Expansion.Count];
                        var stringizedToken = StringizeTokens(tokens, expansion.VAOptState.StringizeToken, closeParenToken);
                        expansion.Expansion.RemoveRange(expansion.VAOptState.StartOfExpansion, expansion.Expansion.Count - expansion.VAOptState.StartOfExpansion);
                        expansion.PasteBefore = expansion.VAOptState.PasteTokens;
                        expansion.Append(this, stringizedToken);
                    }
                    else
                    {
                        bool expandsToNothing = expansion.Expansion.Count == expansion.VAOptState.StartOfExpansion && !expansion.VAOptState.DidPaste;
                        if (expansion.VAOptState.EndsWithPlacemarker || expandsToNothing)
                            expansion.Placemarker(closeParenToken);
                    }

                    expansion.VAOptState.Reset();
                    continue;
                }

                if (token.Kind == TokenKind.Hash)
                {
                    var hashToken = token;
                    expansion.Cursor++;
                    token = expansion.MacroPPTokenAtCursor;

                    if (token.Kind == TokenKind.CPPVAOpt)
                    {
                        expansion.VAOptState.PasteTokens = expansion.PasteBefore;
                        expansion.PasteBefore = false;
                        expansion.VAOptState.DeferStringize(hashToken);
                        continue;
                    }

                    expansion.Cursor++; // yeet (what should be a) parameter
                    expansion.Append(this, StringizeParameter(expansion, token, hashToken));

                    continue;
                }

                if (token.Kind == TokenKind.HashHash)
                {
                    // we will have already diagnosed '####' so we should not perform anything here
                    if (!expansion.PasteBefore)
                    {
                        expansion.Cursor++;
                        expansion.PasteBefore = true;
                    }

                    continue;
                }

                if (token.Kind is not (TokenKind.CPPMacroParam or TokenKind.CPPVAArgs))
                {
                    expansion.Cursor++;
                    AppendTokenToExpansion(expansion, token);
                    continue;
                }

                // non-__VA_OPT__ parameter
                expansion.Cursor++;

                // If the next token is '##', we must check for placemarkers here, and in any
                // case, we need to append the argument tokens as-is.
                //
                // Similarly, if the *previous* token was '##', we need to insert the tokens
                // without expansion here. This can happen if e.g. 'A' in 'A##B' was empty. Note
                // that we can’t get here if 'B' was pasted since we would have already skipped
                // over it in that case. We’ve already skipped the parameter so look back 2 tokens.
                //
                // Note that the 'paste_before' flag is irrelevant here since it is set for both
                // 'A##B' and 'A##__VA_OPT__(B)', but only the former case should be handled here.
                if (
                    (expansion.Cursor < expansion.MacroDefinition.Tokens.Count && expansion.MacroPPTokenAtCursor.Kind == TokenKind.HashHash) ||
                    (expansion.Cursor >= 2 && expansion.MacroDefinition.Tokens[expansion.Cursor - 2].Kind == TokenKind.HashHash)
                )
                {
                    var paramTokens = expansion.Arguments[expansion.GetParamIndex(token)].Select(t => t.Clone()).ToArray();
                    if (paramTokens.Length > 0)
                    {
                        paramTokens[0].LeadingTrivia = token.LeadingTrivia;
                        paramTokens[^1].TrailingTrivia = token.TrailingTrivia;
                    }

                    expansion.Append(this, paramTokens, token);
                }
                else
                {
                    // Finally, if we get here, we have a parameter and there is no paste in sight.
                    // in this case, the standard requires that we fully expand it before appending
                    // it to the expansion (this does not happen for operands of '#' or '##').
                    var paramTokens = Substitute(expansion, expansion.GetParamIndex(token)).Select(t => t.Clone()).ToArray();
                    if (paramTokens.Length > 0)
                    {
                        paramTokens[0].LeadingTrivia = token.LeadingTrivia;
                        paramTokens[^1].TrailingTrivia = token.TrailingTrivia;
                    }

                    expansion.Append(this, paramTokens, token);
                }
            }

            PrepareExpansion(expansion, leadingTrivia, trailingTrivia);
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
                    else AppendTokenToExpansion(expansion, token);
                }
            }
            else
            {
                foreach (var token in macroDef.Tokens)
                    AppendTokenToExpansion(expansion, token);
            }

            PrepareExpansion(expansion, leadingTrivia, trailingTrivia);
        }

        return true;

        void AppendTokenToExpansion(MacroExpansion expansion, Token token)
        {
            if (token.Kind == TokenKind.CPPFile)
            {
                foreach (var t in HandleFileBuiltInMacro(expansion.SourceToken, token))
                    expansion.Append(this, t);
            }
            else if (token.Kind == TokenKind.CPPLine)
            {
                foreach (var t in HandleLineBuiltInMacro(expansion.SourceToken, token))
                    expansion.Append(this, t);
            }
            else expansion.Append(this, token);
        }

        PreprocessorMacroDefinition? GetExpandableMacroAndArguments(StringView name, out Token[][] args, out TriviaList trailingTrivia)
        {
#pragma warning disable IDE0301 // Simplify collection initialization
            args = Array.Empty<Token[]>();
#pragma warning restore IDE0301 // Simplify collection initialization
            trailingTrivia = ppToken.TrailingTrivia;

            var macroDef = GetExpandableMacro(name);
            if (macroDef is null or { IsFunctionLike: false })
                return macroDef;

            if (NextRawPPToken.Kind != TokenKind.OpenParen)
                return null;

            if (parenMustBeDirectlyAdjacent && NextRawPPToken.LeadingTrivia.Trivia.Count != 0)
                return null;

            var openParenToken = ReadTokenRaw();
            Context.Assert(openParenToken.Kind == TokenKind.OpenParen, openParenToken.Source, openParenToken.Location, "How is this not an open paren?");

            var parsedArguments = new List<List<Token>>();

            Token token = ReadTokenRaw();
            if (token.Kind == TokenKind.CloseParen)
            {
                // always have at least one argument list
                parsedArguments.Add([]);
            }
            else
            {
                parsedArguments.Add([]);

                int parenNesting = 1;
                while (token.Kind != TokenKind.EndOfFile && parenNesting > 0)
                {
                    switch (token.Kind)
                    {
                        default: break;
                        case TokenKind.OpenParen: parenNesting++; break;
                        case TokenKind.CloseParen:
                        {
                            parenNesting--;
                            if (parenNesting == 0) continue;
                        } break;

                        // ... 'The individual arguments within the list are separated by comma preprocessing
                        // tokens, but comma preprocessing tokens between matching inner parentheses do not
                        // separate arguments.
                        //
                        // ... 'If there is a ... in the identifier-list in the macro definition, then the trailing
                        // arguments (if any), including any separating comma preprocessing tokens, are merged to
                        // form a single item:'
                        case TokenKind.Comma:
                        {
                            if (parenNesting == 1 && (!macroDef.IsVariadic || parsedArguments.Count <= macroDef.ParameterNames.Count))
                            {
                                parsedArguments.Add([]);
                                token = ReadTokenRaw();
                                continue;
                            }
                        } break;
                    }

                    parsedArguments[^1].Add(token);
                    token = ReadTokenRaw();
                }

                if (parenNesting != 0 || token.Kind != TokenKind.CloseParen)
                    Context.ErrorExpectedMatchingCloseDelimiter(openParenToken.Source, '(', ')', token.Location, openParenToken.Location);
            }

            if (macroDef.IsVariadic && parsedArguments.Count == macroDef.ParameterNames.Count)
            {
                // ... 'if there are as many arguments as named parameters, the macro invocation behaves as if
                // a comma token has been appended to the argument list such that variable arguments are formed
                // that contain no pp-tokens.'
                parsedArguments.Add([]);
            }

            trailingTrivia = token.TrailingTrivia;
            args = [.. parsedArguments.Select(list => list.ToArray())];
            return macroDef;
        }

        void PrepareExpansion(MacroExpansion expansion, TriviaList leadingTrivia, TriviaList trailingTrivia)
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
                        LeadingTrivia = leadingTrivia,
                        TrailingTrivia = new([new TriviumLiteral(" ")], false),
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
                        TrailingTrivia = trailingTrivia,
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
                expansion.Expansion[0].LeadingTrivia = leadingTrivia;
                expansion.Expansion[^1].TrailingTrivia = trailingTrivia;
                PushTokenStream(tokenStream);
            }
        }
    }

    private Token StringizeParameter(MacroExpansion expansion, Token paramToken, Token hashToken)
    {
        if (paramToken.Kind is not (TokenKind.CPPMacroParam or TokenKind.CPPVAArgs))
        {
            Context.ErrorCanOnlyStringizeParameters(hashToken, paramToken);
            string invalidText = StringizeToken(paramToken, true);
            return new Token(TokenKind.LiteralString, SourceLanguage.C, hashToken.Source, new(hashToken.Location, hashToken.Location))
            {
                Spelling = invalidText,
                StringValue = invalidText,
            };
        }

        var paramTokens = expansion.Arguments[expansion.GetParamIndex(paramToken)];
        return StringizeTokens(paramTokens, hashToken, paramToken);
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

        var source = new SourceText("<paste>", concatText, SourceLanguage.C);
        var proxyContext = new CompilerContext(VoidDiagnosticConsumer.Instance, Context.Target, Context.Triple) { IncludePaths = Context.IncludePaths };
        var lexer = new Lexer(proxyContext, source, LanguageOptions);
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

    private bool SkipRemainingDirectiveTokens(Token? currentToken)
    {
        if (currentToken is { Kind: TokenKind.CPPDirectiveEnd or TokenKind.EndOfFile })
            return false;

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
            HandleDefineDirective();
        else if (directiveName == "undef")
            HandleUndefDirective(directiveToken);
        else if (directiveName == "include")
            HandleIncludeDirective(language, preprocessorToken, directiveToken, false);
        else if (directiveName == "include_next")
            HandleIncludeDirective(language, preprocessorToken, directiveToken, true);
        else if (directiveName == "if" && language == SourceLanguage.C)
            HandleIfDirective(preprocessorToken, directiveToken);
        else if (directiveName == "ifdef" && language == SourceLanguage.C)
            HandleIfdefDirective(preprocessorToken, directiveToken, true);
        else if (directiveName == "ifndef" && language == SourceLanguage.C)
            HandleIfdefDirective(preprocessorToken, directiveToken, false);
        else if (directiveName == "elif" && language == SourceLanguage.C)
            HandleElifDirective(preprocessorToken, directiveToken);
        else if (directiveName == "elifdef" && language == SourceLanguage.C)
            HandleElifdefDirective(preprocessorToken, directiveToken, true);
        else if (directiveName == "elifndef" && language == SourceLanguage.C)
            HandleElifdefDirective(preprocessorToken, directiveToken, false);
        else if (directiveName == "else" && language == SourceLanguage.C)
            HandleElseDirective(preprocessorToken, directiveToken);
        else if (directiveName == "endif" && language == SourceLanguage.C)
            HandleEndifDirective(preprocessorToken, directiveToken);
        else if (directiveName == "pragma" && language == SourceLanguage.C)
            HandlePragmaDirective(language, preprocessorToken, directiveToken);
        else if (directiveName == "warning" && language == SourceLanguage.C)
            HandleDiagnosticDirective(language, preprocessorToken, directiveToken, DiagnosticLevel.Warning);
        else if (directiveName == "error" && language == SourceLanguage.C)
            HandleDiagnosticDirective(language, preprocessorToken, directiveToken, DiagnosticLevel.Error);
        else
        {
            Context.ErrorUnknownPreprocessorDirective(directiveToken.Source, directiveToken.Location);
            SkipRemainingDirectiveTokens(directiveToken);
        }
    }

    private sealed class ParsedMacroData
    {
        public required Token NameToken { get; set; }
        public StringView Name => NameToken.StringValue;
        public List<Token> Tokens { get; } = [];
        public List<StringView> ParameterNames { get; set; } = [];

        public bool IsVariadic { get; set; }
        public bool IsFunctionLike { get; set; }
        public bool RequiresPasting { get; set; }
    }

    private void HandleDefineDirective()
    {
        var macroNameToken = ReadTokenRaw();
        if (macroNameToken.Kind != TokenKind.CPPIdentifier)
        {
            Context.ErrorExpectedMacroName(macroNameToken.Source, macroNameToken.Location);
            SkipRemainingDirectiveTokens(macroNameToken);
            return;
        }

        var macroData = new ParsedMacroData()
        {
            NameToken = macroNameToken,
        };

        var token = ReadTokenRaw();
        macroData.IsFunctionLike = token.Kind == TokenKind.OpenParen && macroNameToken.TrailingTrivia.Trivia.Count == 0;

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
                else if (token.StringValue == "__FILE__")
                    token.Kind = TokenKind.CPPFile;
                else if (token.StringValue == "__LINE__")
                    token.Kind = TokenKind.CPPLine;
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
            token = macroData.Tokens[i++];
            switch (token.Kind)
            {
                case TokenKind.Hash: DefineDirectiveCheckHash(macroData, i); break;
                case TokenKind.HashHash: DefineDirectiveCheckHashHash(macroData, i); break;
                case TokenKind.CPPVAOpt: token.CPPIntegerData = FindVAOptCloseParen(macroData, ref i); break;
            }
        }

        if (_reservedPPNames.Contains(macroData.Name))
        {
            Context.ErrorCannotDefineReservedName(macroNameToken.Source, macroNameToken.Location);
            return;
        }

        _macroDefs[macroData.Name] = new PreprocessorMacroDefinition(macroData.NameToken, macroData.Tokens, macroData.ParameterNames)
        {
            IsFunctionLike = macroData.IsFunctionLike,
            IsVariadic = macroData.IsVariadic,
            RequiresPasting = macroData.RequiresPasting,
        };
    }

    private void HandleUndefDirective(Token directiveToken)
    {
        var macroNameToken = ReadTokenRaw();
        if (macroNameToken.Kind != TokenKind.CPPIdentifier)
        {
            Context.ErrorExpectedMacroName(macroNameToken.Source, macroNameToken.Location);
            SkipRemainingDirectiveTokens(macroNameToken);
            return;
        }

        if (SkipRemainingDirectiveTokens(null))
            Context.WarningExtraTokensAtEndOfDirective(directiveToken);

        if (_reservedPPNames.Contains(macroNameToken.StringValue))
        {
            Context.ErrorCannotUndefineReservedName(macroNameToken.Source, macroNameToken.Location);
            return;
        }

        _macroDefs.Remove(macroNameToken.StringValue);
    }

    private readonly HashSet<SourceText> _includeGuard = [];
    private readonly Dictionary<SourceText, string> _includeGuardMacroNames = [];

    private void HandleIncludeDirective(SourceLanguage language, Token preprocessorToken, Token directiveToken, bool includeNext)
    {
        var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

        if (includeNext)
            Context.ExtIncludeNext(directiveToken);

        Token includePathToken;
        using (var _ = lexer.PushMode(SourceLanguage.C, lexer.State | LexerState.CPPHasHeaderNames))
            includePathToken = ReadTokenRaw();

        if (SkipRemainingDirectiveTokens(includePathToken))
            Context.WarningExtraTokensAtEndOfDirective(directiveToken);

        if (includePathToken.Kind is not (TokenKind.LiteralString or TokenKind.CPPHeaderName))
        {
            Context.ErrorExpectedToken(includePathToken.Source, includePathToken.Location, "a header name");
            return;
        }

        IncludeKind includeKind = includePathToken.Kind == TokenKind.CPPHeaderName ? IncludeKind.System : IncludeKind.Local;
        if (includeNext) includeKind |= IncludeKind.IncludeNext;

        var includeSource = Context.GetSourceTextForIncludeFilePath((string)includePathToken.StringValue, includeKind, includePathToken.Source.Name);

        if (includeSource is null)
        {
            Context.ErrorCannotOpenSourceFile(includePathToken.Source, includePathToken.Location, includePathToken.StringValue);
            return;
        }

        // check for an unconditional header guard against re-including this file
        if (_includeGuard.Contains(includeSource))
        {
            // only exception to the unconditional guard: if it was put in place by detecting idiomatic header guards
            if (_includeGuardMacroNames.TryGetValue(includeSource, out string? includeGuardMacroName))
            {
                // only if the detected header guard macro is still defined is this okay to ignore.
                if (_macroDefs.TryGetValue(includeGuardMacroName, out _))
                    return;
            }
            else return;
        }

        if (language == SourceLanguage.C)
            AddCIncludeLexerFromC(includeSource);
        else AddCIncludeLexerFromLaye(includeSource, preprocessorToken);
    }

    private void SkipTokensUntilNextValidConditionalDirective()
    {
        var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

        Token? preprocessorToken = null;
        Token? directiveToken = null;

        int ifNesting = 0;
        using (lexer.PushMode(lexer.State | LexerState.CPPWithinRejectedBranch))
        {
            while (!lexer.IsAtEnd)
            {
                var token = lexer.ReadNextPPToken();
                if (token is { Kind: TokenKind.Hash, IsAtStartOfLine: true })
                {
                    using (lexer.PushMode(lexer.State | LexerState.CPPWithinDirective))
                    {
                        var nextToken = lexer.ReadNextPPToken();
                        if (nextToken is not { Kind: TokenKind.CPPIdentifier })
                            continue;

                        if (nextToken.StringValue == "if" || nextToken.StringValue == "ifdef" || nextToken.StringValue == "ifndef")
                            ifNesting++;
                        else if (nextToken.StringValue == "else" || nextToken.StringValue == "elif" || nextToken.StringValue == "elifdef" || nextToken.StringValue == "elifndef")
                        {
                            if (ifNesting == 0)
                            {
                                preprocessorToken = token;
                                directiveToken = nextToken;
                                break;
                            }
                        }
                        else if (nextToken.StringValue == "endif")
                        {
                            if (ifNesting == 0)
                            {
                                preprocessorToken = token;
                                directiveToken = nextToken;
                                break;
                            }
                            else ifNesting--;
                        }
                    }
                }
            }
        }

        if (preprocessorToken is not null && directiveToken is not null)
        {
            using (lexer.PushMode(lexer.State | LexerState.CPPWithinDirective))
                HandleDirective(SourceLanguage.C, preprocessorToken, directiveToken);
        }

        // if we get here without handling a directive, we'll report a missing #endif when we pop this buffer
    }

    private void HandleIfDirective(Token preprocessorToken, Token directiveToken)
    {
        var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

        var eval = new PPExprParser(this, lexer);
        bool isBranchChosen = 1 == eval.Evaluate();

        // TODO(local): report a different error if we didn't manage to consume all the necessary tokens
        SkipRemainingDirectiveTokens(eval.LastReadToken);

        var ifState = new PreprocessorIfState(directiveToken) { HasBranchBeenTaken = isBranchChosen };
        lexer.PreprocessorIfStates.Push(ifState);

        // if we passed the condition, continue to lex and preprocess tokens like normal.
        if (isBranchChosen)
            return;

        SkipTokensUntilNextValidConditionalDirective();
    }

    private void HandleIfdefDirective(Token preprocessorToken, Token directiveToken, bool isIfdef)
    {
        var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

        Token macroName = ReadTokenRaw();
        if (SkipRemainingDirectiveTokens(macroName))
            Context.WarningExtraTokensAtEndOfDirective(directiveToken);

        bool isDefined;
        if (macroName.Kind != TokenKind.CPPIdentifier)
        {
            Context.ErrorExpectedMacroName(macroName.Source, macroName.Location);
            isDefined = false;
        }
        else isDefined = IsMacroDefined(macroName.StringValue);

        bool isBranchChosen = isDefined == isIfdef;

        var ifState = new PreprocessorIfState(directiveToken) { HasBranchBeenTaken = isBranchChosen };
        lexer.PreprocessorIfStates.Push(ifState);

        // if we passed the condition (based on which directive was used), continue to lex and preprocess tokens like normal.
        if (isBranchChosen)
            return;

        SkipTokensUntilNextValidConditionalDirective();
    }

    private void HandleElifDirective(Token preprocessorToken, Token directiveToken)
    {
        var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

        var eval = new PPExprParser(this, lexer);
        bool isBranchChosen = 1 == eval.Evaluate();

        // TODO(local): report a different error if we didn't manage to consume all the necessary tokens
        SkipRemainingDirectiveTokens(eval.LastReadToken);

        if (lexer.PreprocessorIfDepth == 0)
        {
            Context.ErrorConditionalDirectiveWithoutIf(directiveToken);
            return;
        }

        var state = lexer.PreprocessorIfStates.Peek();
        if (!state.HasBranchBeenTaken)
        {
            // if we passed the condition, continue to lex and preprocess tokens like normal.
            if (isBranchChosen)
            {
                state.HasBranchBeenTaken = true;
                return;
            }
        }

        SkipTokensUntilNextValidConditionalDirective();
    }

    private void HandleElifdefDirective(Token preprocessorToken, Token directiveToken, bool isElifdef)
    {
        if (!LanguageOptions.CIsC23)
        {
            if (isElifdef)
                Context.ExtElifdef(directiveToken);
            else Context.ExtElifndef(directiveToken);
        }

        var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

        Token macroName = ReadTokenRaw();
        if (SkipRemainingDirectiveTokens(macroName))
            Context.WarningExtraTokensAtEndOfDirective(directiveToken);

        bool isDefined;
        if (macroName.Kind != TokenKind.CPPIdentifier)
        {
            Context.ErrorExpectedMacroName(macroName.Source, macroName.Location);
            isDefined = false;
        }
        else isDefined = IsMacroDefined(macroName.StringValue);

        if (lexer.PreprocessorIfDepth == 0)
        {
            Context.ErrorConditionalDirectiveWithoutIf(directiveToken);
            return;
        }

        var state = lexer.PreprocessorIfStates.Peek();
        if (!state.HasBranchBeenTaken)
        {
            bool isBranchChosen = isDefined == isElifdef;

            // if we passed the condition (based on which directive was used), continue to lex and preprocess tokens like normal.
            if (isBranchChosen)
            {
                state.HasBranchBeenTaken = true;
                return;
            }
        }

        SkipTokensUntilNextValidConditionalDirective();
    }

    private void HandleElseDirective(Token preprocessorToken, Token directiveToken)
    {
        var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

        if (SkipRemainingDirectiveTokens(null))
            Context.WarningExtraTokensAtEndOfDirective(directiveToken);

        if (lexer.PreprocessorIfDepth == 0)
        {
            Context.ErrorConditionalDirectiveWithoutIf(directiveToken);
            return;
        }

        var state = lexer.PreprocessorIfStates.Peek();
        // if all other branches failed, continue to lex and preprocess tokens like normal.
        // we hit a branch, finally.
        if (!state.HasBranchBeenTaken) return;

        SkipTokensUntilNextValidConditionalDirective();
    }

    private void HandleEndifDirective(Token preprocessorToken, Token directiveToken)
    {
        var lexer = ((LexerTokenStream)_tokenStreams.Peek()).Lexer;

        if (SkipRemainingDirectiveTokens(null))
            Context.WarningExtraTokensAtEndOfDirective(directiveToken);

        if (lexer.PreprocessorIfDepth == 0)
        {
            Context.ErrorConditionalDirectiveWithoutIf(directiveToken);
            return;
        }

        lexer.PreprocessorIfStates.Pop();
    }

    private void HandlePragmaDirective(SourceLanguage language, Token preprocessorToken, Token directiveToken)
    {
        Context.Assert(language == SourceLanguage.C, "Pragma directive is only available in C");

        var token = ReadTokenRaw();
        if (token.Kind == TokenKind.CPPIdentifier && token.StringValue == "once")
        {
            Context.ExtPragmaOnce(token.Source, token.Location);

            if (SkipRemainingDirectiveTokens(token))
                Context.WarningExtraTokensAtEndOfDirective(directiveToken);

            _includeGuard.Add(preprocessorToken.Source);
            return;
        }
        else if (token.Kind == TokenKind.CPPIdentifier && token.StringValue == "system_header")
        {
            preprocessorToken.Source.IsSystemHeader = true;

            if (SkipRemainingDirectiveTokens(token))
                Context.WarningExtraTokensAtEndOfDirective(directiveToken);

            return;
        }

        // let's not worry about warning for extra tokens on unrecognized pragmas
        // or even warning that we don't recognize it at all, for now.
        SkipRemainingDirectiveTokens(token);
    }

    private void HandleDiagnosticDirective(SourceLanguage language, Token preprocessorToken, Token directiveToken, DiagnosticLevel diagLevel)
    {
        var tokens = new List<Token>();

        var token = ReadTokenRaw();
        while (token.Kind is not (TokenKind.CPPDirectiveEnd or TokenKind.EndOfFile))
        {
            tokens.Add(token);
            token = ReadTokenRaw();
        }

        // just in fuckin case
        SkipRemainingDirectiveTokens(token);

        var diagnosticMessageToken = StringizeTokens(tokens, directiveToken, directiveToken);
        Context.Assert(diagnosticMessageToken.Kind == TokenKind.LiteralString, "Expected stringize operation to create a literal string.");

        Context.Diag.Emit(diagLevel, directiveToken.Source, directiveToken.Location, (string)diagnosticMessageToken.StringValue);
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

    #region Expression Evaluation

    private sealed class PPExprLexerTokenStream(Preprocessor pp, Lexer lexer)
        : ITokenStream
    {
        private readonly int _initialTokenStreamCount = pp._tokenStreams.Count;

        public Lexer Lexer { get; } = lexer;

        private bool _isAtEnd = false;
        public bool IsAtEnd => _isAtEnd || pp.NextRawPPToken.Kind is TokenKind.CPPDirectiveEnd or TokenKind.EndOfFile;
        public SourceLanguage Language => Lexer.Language;

        public Token LastToken { get; set; } = Token.AnyEndOfFile;

        public Token Read()
        {
            //pp.Context.Assert(pp.IsInLexer && pp._tokenStreams.Peek() is LexerTokenStream { } lts && lts.Lexer == Lexer, "Should still be at the correct/current lexer if trying to read from this expression token stream.");
            pp.Context.Assert(!IsAtEnd, "Don't attempt to read past the end of this token stream.");
            pp.Context.Assert(pp._tokenStreams.Count >= _initialTokenStreamCount, "Somehow popped the lexer's token stream, shit.");

            var token = LastToken = pp.ReadTokenRaw();
            if (token.Kind is TokenKind.CPPDirectiveEnd or TokenKind.EndOfFile)
                _isAtEnd = true;
            return token;
        }
    }

    private sealed class PPExprParser
        : Parser
    {
        private readonly Preprocessor _pp;
        private readonly PPExprLexerTokenStream _ts;

        public Token LastReadToken => _ts.LastToken;

        private PPExprParser(Preprocessor pp, PPExprLexerTokenStream tokenStream)
            : base(pp.Context, pp.LanguageOptions, tokenStream.Lexer.Source, tokenStream)
        {
            _pp = pp;
            _ts = tokenStream;
        }

        public PPExprParser(Preprocessor pp, Lexer lexer)
            : this(pp, new PPExprLexerTokenStream(pp, lexer))
        {
        }

        private static int B(bool b) => b ? 1 : 0;

        private void ConsumeUntilMatchingCloseParen()
        {
            int nesting = 1;
            while (!IsAtEnd)
            {
                switch (CurrentToken.Kind)
                {
                    case TokenKind.OpenParen: nesting++; break;
                    case TokenKind.CloseParen:
                    {
                        nesting--;
                        if (nesting == 0) return;
                    } break;
                }

                Consume();
            }

            Expect("')'", TokenKind.CloseParen);
        }

        public int Evaluate()
        {
            int primary = EvaluatePrimary();
            return EvaluateBinary(primary, 0);
        }

        private int EvaluatePrimary()
        {
            if (IsAtEnd)
            {
                Context.ErrorExpectedValueInPPExpr(Source, CurrentLocation);
                return 0;
            }

            var token = Consume();
            switch (token.Kind)
            {
                case TokenKind.CPPNumber: return B(token.StringValue != "0");
                case TokenKind.Bang: return B(EvaluatePrimary() == 0);

                case TokenKind.OpenParen:
                {
                    int subexpr = Evaluate();
                    Expect("')'", TokenKind.CloseParen);
                    return subexpr;
                }

                case TokenKind.CPPIdentifier when token.StringValue == "defined":
                {
                    if (IsAtEnd)
                    {
                        Context.ErrorExpectedMacroName(Source, CurrentLocation);
                        return 0;
                    }

                    if (TryConsume(TokenKind.OpenParen) is not null)
                    {
                        var macroToken = Consume();
                        Expect("')'", TokenKind.CloseParen);

                        if (macroToken.Kind != TokenKind.CPPIdentifier)
                        {
                            Context.ErrorExpectedMacroName(macroToken.Source, macroToken.Location);
                            return 0;
                        }

                        return B(_pp.IsMacroDefined(macroToken.StringValue));
                    }

                    var macroNameToken = Consume();
                    if (macroNameToken.Kind != TokenKind.CPPIdentifier)
                    {
                        Context.ErrorExpectedMacroName(macroNameToken.Source, macroNameToken.Location);
                        return 0;
                    }

                    return B(_pp.IsMacroDefined(macroNameToken.StringValue));
                }

                case TokenKind.CPPIdentifier when token.StringValue == "__has_feature" && At(TokenKind.OpenParen):
                {
                    Context.ExtHasFeature(token);
                    Consume();

                    var featureToken = Consume();
                    Expect("')'", TokenKind.CloseParen);

                    if (featureToken.Kind != TokenKind.CPPIdentifier)
                    {
                        Context.ErrorExpectedToken(featureToken.Source, featureToken.Location, "an identifier");
                        return 0;
                    }

                    return 0;
                }

                case TokenKind.CPPIdentifier when (token.StringValue == "__has_include" || token.StringValue == "__has_include_next") && At(TokenKind.OpenParen):
                {
                    bool isIncludeNext = token.StringValue == "__has_include_next";

                    if (isIncludeNext)
                        Context.ExtHasIncludeNext(token);
                    else Context.ExtHasInclude(token);

                    Consume();

                    Token includePathToken;
                    using (_ts.Lexer.PushMode(_ts.Lexer.State | LexerState.CPPHasHeaderNames))
                        includePathToken = Consume();
                    Expect("')'", TokenKind.CloseParen);

                    if (includePathToken.Kind is not (TokenKind.LiteralString or TokenKind.CPPHeaderName))
                    {
                        Context.ErrorExpectedToken(includePathToken.Source, includePathToken.Location, "a header name");
                        return 0;
                    }

                    string includeFilePath;
                    if (includePathToken.Kind == TokenKind.LiteralString)
                        includeFilePath = _pp.Context.IncludePaths.ResolveIncludePath((string)includePathToken.StringValue, includePathToken.Source.Name, out bool _, ref isIncludeNext);
                    else includeFilePath = _pp.Context.IncludePaths.ResolveIncludePath((string)includePathToken.StringValue, out bool _, ref isIncludeNext);

                    return B(File.Exists(includeFilePath));
                }

                case TokenKind.CPPIdentifier when token.StringValue == "__has_cpp_attribute" && At(TokenKind.OpenParen):
                {
                    Context.ExtHasFeature(token);
                    Consume();

                    var attributeToken = Consume();
                    Expect("')'", TokenKind.CloseParen);

                    if (attributeToken.Kind != TokenKind.CPPIdentifier)
                    {
                        Context.ErrorExpectedToken(attributeToken.Source, attributeToken.Location, "an identifier");
                        return 0;
                    }

                    return 0;
                }

                case TokenKind.CPPIdentifier:
                {
                    if (_pp.MaybeExpandMacro(token, parenMustBeDirectlyAdjacent: false))
                        return EvaluatePrimary();

                    if (TryConsume(TokenKind.OpenParen) is not null)
                    {
                        Context.ErrorFunctionLikeMacroIsNotDefined(token.Source, token.Location);
                        ConsumeUntilMatchingCloseParen();
                        return 0;
                    }

                    // it's an undefined identifier and also not a function-like macro invocation, just return 0
                    return 0;
                }

                default:
                {
                    Context.ErrorInvalidTokenInPPExpr(token);
                    return 0;
                }
            }
        }

        private static int GetBinaryOperatorPrecedence(Token token) => token.Kind switch
        {
            TokenKind.Star or TokenKind.Slash or TokenKind.Percent => 10,
            TokenKind.Plus or TokenKind.Minus => 9,
            TokenKind.LessLess or TokenKind.GreaterGreater => 8,
            TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual => 7,
            TokenKind.EqualEqual or TokenKind.BangEqual => 6,
            TokenKind.Ampersand => 5,
            TokenKind.Caret => 4,
            TokenKind.Pipe => 3,
            TokenKind.AmpersandAmpersand => 2,
            TokenKind.PipePipe => 1,
            _ => 0,
        };

        private bool IsBinaryOperatorRightAssociative(Token token) => false;

        private int EvaluateBinary(int lhs, int precedence)
        {
            while (!IsAtEnd && CurrentToken.Kind is not (TokenKind.CloseParen) && GetBinaryOperatorPrecedence(CurrentToken) >= precedence)
            {
                var operatorToken = Consume();

                int rhs = EvaluatePrimary();

                bool isRightAssoc = IsBinaryOperatorRightAssociative(operatorToken);
                while (!IsAtEnd && (
                        GetBinaryOperatorPrecedence(CurrentToken) > GetBinaryOperatorPrecedence(operatorToken) ||
                        (isRightAssoc && GetBinaryOperatorPrecedence(CurrentToken) == GetBinaryOperatorPrecedence(operatorToken))
                    ))
                {
                    rhs = EvaluateBinary(rhs, GetBinaryOperatorPrecedence(CurrentToken));
                }

                switch (operatorToken.Kind)
                {
                    case TokenKind.Star: lhs *= rhs; break;
                    case TokenKind.Slash: lhs /= rhs; break;
                    case TokenKind.Percent: lhs %= rhs; break;
                    case TokenKind.Plus: lhs += rhs; break;
                    case TokenKind.Minus: lhs -= rhs; break;
                    case TokenKind.LessLess: lhs <<= rhs; break;
                    case TokenKind.GreaterGreater: lhs >>= rhs; break;
                    case TokenKind.Less: lhs = B(lhs < rhs); break;
                    case TokenKind.LessEqual: lhs = B(lhs <= rhs); break;
                    case TokenKind.Greater: lhs = B(lhs > rhs); break;
                    case TokenKind.GreaterEqual: lhs = B(lhs >= rhs); break;
                    case TokenKind.EqualEqual: lhs = B(lhs == rhs); break;
                    case TokenKind.BangEqual: lhs = B(lhs != rhs); break;
                    case TokenKind.Ampersand: lhs &= rhs; break;
                    case TokenKind.Caret: lhs ^= rhs; break;
                    case TokenKind.Pipe: lhs |= rhs; break;
                    case TokenKind.AmpersandAmpersand: lhs = B(lhs != 0 && rhs != 0); break;
                    case TokenKind.PipePipe: lhs = B(lhs != 0 || rhs != 0); break;

                    default:
                    {
                        Context.ErrorTokenIsNotBinaryOperatorInPPExpr(operatorToken);
                    } break;
                }
            }

            return lhs;
        }
    }

    #endregion
}
