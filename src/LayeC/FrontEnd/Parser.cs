using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using LayeC.FrontEnd.Syntax;
using LayeC.Source;

namespace LayeC.FrontEnd;

public sealed partial class Parser
{
    public static SyntaxModuleUnit ParseLayeModuleUnitSource(CompilerContext context, Sema sema, SourceText unitSource)
    {
        var parser = new Parser(context, sema, unitSource, SourceLanguage.Laye);
        var topLevelNodes = new List<SyntaxNode>();

        while (true)
        {
            var node = parser.ParseTopLevelSyntax();
            topLevelNodes.Add(node);

            if (node is SyntaxEndOfFile)
                break;
        }

        return new SyntaxModuleUnit(unitSource, topLevelNodes);
    }

    public CompilerContext Context { get; }
    public Sema Sema { get; }
    public SourceText Source { get; }
    public LanguageOptions LanguageOptions { get; }

    private readonly Lexer _lexer;

    private Parser(CompilerContext context, Sema sema, SourceText source,
        SourceLanguage language = SourceLanguage.Laye)
    {
        Context = context;
        Sema = sema;
        Source = source;
        LanguageOptions = sema.LanguageOptions;
        _lexer = new(context, source, sema.LanguageOptions, language);
    }

    #region Token Processing

    private readonly List<Token> _peekBuffer = new(6);

    private SourceLanguage Language => _peekBuffer.Count == 0 ? _lexer.Language : _peekBuffer[0].Language;
    private Token CurrentToken => PeekToken(0);
    private SourceLocation CurrentLocation => CurrentToken.Location;

    private bool At(TokenKind kind) => PeekAt(0, kind);
    private bool At(params TokenKind[] kinds) => PeekAt(0, kinds);

    private bool PeekAt(int ahead, TokenKind kind) => PeekToken(ahead).Kind == kind;
    private bool PeekAt(int ahead, params TokenKind[] kinds)
    {
        var peekKind = PeekToken(ahead).Kind;
        foreach (var kind in kinds)
        {
            if (peekKind == kind)
                return true;
        }

        return false;
    }

    private Token Consume()
    {
        var token = CurrentToken;
        _peekBuffer.RemoveAt(0);
        return token;
    }

    private bool TryConsume(TokenKind kind, [NotNullWhen(true)] out Token? token)
    {
        token = CurrentToken;
        if (token.Kind == kind)
        {
            _peekBuffer.RemoveAt(0);
            return true;
        }

        token = null;
        return false;
    }

    private Token Expect(TokenKind kind, string expected)
    {
        if (!TryConsume(kind, out var expectedToken))
        {
            var currentLocation = CurrentLocation;
            Context.ErrorExpectedToken(Source, currentLocation, expected);
            expectedToken = new Token(TokenKind.Missing, Language, Source, new(currentLocation, currentLocation));
        }

        return expectedToken;
    }

    private Token ExpectIdentifier() => Expect(TokenKind.Identifier, "an identifier");

    private Token PeekToken(int ahead = 0)
    {
        Context.Assert(ahead >= 0, $"Parameter {nameof(ahead)} to function {nameof(Parser)}::{nameof(PeekToken)} must be non-negative; the parser should never rely on token look-back.");
        Context.Assert(ahead < _peekBuffer.Capacity, $"Parameter {nameof(ahead)} to function {nameof(Parser)}::{nameof(PeekToken)} must be less than the lookahead limit of {_peekBuffer.Capacity}.");

        while (_peekBuffer.Count <= ahead)
        {
            var token = _lexer.ReadNextToken();
            _peekBuffer.Add(token);
        }

        return _peekBuffer[ahead];
    }

    private IDisposable PushLexerMode(SourceLanguage language) => PushLexerMode(language, _lexer.State);
    private IDisposable PushLexerMode(LexerState state) => PushLexerMode(Language, state);
    private IDisposable PushLexerMode(SourceLanguage language, LexerState state)
    {
        Context.Assert(_peekBuffer.Count == 0, Source, _lexer.CurrentLocation, "Can only alter the parser's lexer state if there are no tokens in the peek buffer.");
        return _lexer.PushMode(language, state);
    }

    #endregion

    #region Parser State Management

    private void AssertC([CallerMemberName] string callerMemberName = "<unknown>") => AssertLanguage(SourceLanguage.C, callerMemberName);
    private void AssertLaye([CallerMemberName] string callerMemberName = "<unknown>") => AssertLanguage(SourceLanguage.Laye, callerMemberName);
    private void AssertLanguage(SourceLanguage language, [CallerMemberName] string callerMemberName = "<unknown>")
    {
        Context.Assert(Language == language, $"Cannot call {nameof(Parser)}::{callerMemberName} if the current source language is not C.");
    }

    private void AssertPeekBufferEmpty()
    {
        Context.Assert(_peekBuffer.Count == 0, "Expected the token peek buffer to be empty.");
    }

    #endregion

    public SyntaxNode ParseTopLevelSyntax()
    {
        if (TryConsume(TokenKind.EndOfFile, out var eofToken))
            return new SyntaxEndOfFile(eofToken);

        if (Language == SourceLanguage.C)
            return ParseCTopLevel();

        if (TryConsume(TokenKind.Pragma, out var pragmaToken))
        {
            var pragmaKindToken = Consume();
            return ParseLayeTopLevelPragma(pragmaToken, pragmaKindToken);
        }

        var declType = ParseLayeQualifiedType();
        return ParseLayeBindingOrFunctionDeclStartingAtName(declType);
    }

    public SyntaxNode ParseExpression()
    {
        if (Language == SourceLanguage.C)
            return ParseCExpression();
        else return ParseLayeExpression();
    }
}
