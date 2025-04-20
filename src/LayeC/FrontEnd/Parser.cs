using LayeC.Source;

namespace LayeC.FrontEnd;

/// <summary>
/// The base, shared implementation of a language parser.
/// </summary>
public abstract class Parser(CompilerContext context, LanguageOptions languageOptions, SourceText source, ITokenStream tokens)
{
    public CompilerContext Context { get; } = context;
    public LanguageOptions LanguageOptions { get; } = languageOptions;
    public SourceText Source { get; } = source;

    #region Token Processing

    private readonly ITokenStream _tokens = tokens;
    private readonly List<Token> _peekedTokens = new(5);

    public bool IsAtEnd => _peekedTokens.Count == 0 && _tokens.IsAtEnd;
    public Token CurrentToken => PeekToken(0);

    private SourceLocation _lastValidLocation;

    public SourceText CurrentSource => !IsAtEnd ? CurrentToken.Source : Source;
    public SourceRange CurrentRange => !IsAtEnd ? CurrentToken.Range : new(_lastValidLocation, _lastValidLocation);
    public SourceLocation CurrentLocation => CurrentRange.Begin;

    private void InternalAdvance()
    {
        if (IsAtEnd) return;
        _lastValidLocation = CurrentToken.Range.End + 1;
        _peekedTokens.RemoveAt(0);
    }

    public Token PeekToken(int ahead)
    {
        Context.Assert(ahead >= 0, "Cannot peek backwards into the parser's token stream.");
        Context.Assert(ahead < _peekedTokens.Capacity, $"Cannot peek more than {_peekedTokens.Count} tokens ahead.");

        for (int i = _peekedTokens.Count; i <= ahead; i++)
        {
            if (_tokens.IsAtEnd)
                return Token.AnyEndOfFile;

            var nextToken = _tokens.Read();
            _peekedTokens.Add(nextToken);
        }

        return _peekedTokens[ahead];
    }

    public bool At(params TokenKind[] kinds)
    {
        if (IsAtEnd) return false;
        return Array.IndexOf(kinds, CurrentToken.Kind) >= 0;
    }

    public bool PeekAt(int ahead, params TokenKind[] kinds)
    {
        if (IsAtEnd) return false;
        return Array.IndexOf(kinds, PeekToken(ahead).Kind) >= 0;
    }

    public Token CreateMissingToken(SourceLocation location)
    {
        return new Token(TokenKind.Missing, SourceLanguage.None, Source, new SourceRange(location, location));
    }

    public Token Consume()
    {
        Context.Assert(!IsAtEnd, "Can't consume past the end of the parser's token stream.");
        var result = CurrentToken;
        InternalAdvance();
        return result;
    }

    public Token? TryConsume(TokenKind kind)
    {
        if (!At(kind)) return null;
        var result = CurrentToken;
        InternalAdvance();
        return result;
    }

    public Token Expect(string what, TokenKind kind)
    {
        if (!At(kind))
        {
            var ct = CurrentToken;
            Context.ErrorExpectedToken(ct.Source, ct.Location, what);
            return CreateMissingToken(ct.Location);
        }

        var result = CurrentToken;
        InternalAdvance();
        return result;
    }

    public Token SkipUntil(string what, params TokenKind[] kinds)
    {
        while (!IsAtEnd)
        {
            var ct = CurrentToken;
            if (At(kinds))
                return ct;
            else if (ct.Kind == TokenKind.EndOfFile)
            {
                Context.ErrorExpectedToken(Source, ct.Location, what);
                return ct;
            }

            InternalAdvance();
        }

        Context.ErrorExpectedToken(Source, CurrentLocation, what);
        return Token.AnyEndOfFile;
    }

    #endregion
}
