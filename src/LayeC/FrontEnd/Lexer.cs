using System.Numerics;
using System.Text;

using LayeC.FrontEnd.C.Preprocess;
using LayeC.Source;

namespace LayeC.FrontEnd;

[Flags]
public enum LexerState
{
    None = 0,

    /// <summary>
    /// Flag set to indicate that, when reading C tokens, the lexer is within a preprocessor directive.
    /// Used, for example, to transform newline characters into directive-end tokens to aid in preprocessor parsing later.
    /// </summary>
    CPPWithinDirective = 1 << 0,

    /// <summary>
    /// Flag set to indicate that the lexer can, when reading C tokens, accept header names with angle-bracket delimiters.
    /// </summary>
    CPPHasHeaderNames = 1 << 1,
}

public sealed class Lexer(CompilerContext context, SourceText source,
    SourceLanguage language = SourceLanguage.Laye)
{
    public CompilerContext Context { get; } = context;
    public SourceText Source { get; } = source;

    public SourceLanguage Language { get; private set; } = language;
    public LexerState State { get; private set; } = LexerState.None;

    #region Source Character Processing

    private int _readPosition;
    private bool _isAtStartOfLine = true;

    private bool IsAtEnd => _readPosition >= Source.Text.Length;
    private char CurrentCharacter => PeekCharacter(0, out int _);

    public SourceLocation CurrentLocation => new(_readPosition);
    public SourceRange GetRange(SourceLocation beginLocation) => new(beginLocation, CurrentLocation);

    private void Advance(int amount = 1)
    {
        Context.Assert(amount >= 1, $"Parameter {nameof(amount)} to function {nameof(Lexer)}::{nameof(Advance)} must be positive; advancing the lexer must always move forward at least one character if possible.");

        for (int i = 0; i < amount && !IsAtEnd; i++)
        {
            char c = PeekCharacter(0, out int stride);
            if (c is '\n') _isAtStartOfLine = true;
            _readPosition += stride;
        }

        _readPosition = Math.Min(_readPosition, Source.Text.Length);
    }

    private bool TryAdvance(char c)
    {
        if (CurrentCharacter != c)
            return false;

        Advance();
        return true;
    }

    private char PeekCharacter(int ahead) => PeekCharacter(ahead, out int _);
    private char PeekCharacter(int ahead, out int stride)
    {
        Context.Assert(ahead >= 0, $"Parameter {nameof(ahead)} to function {nameof(Lexer)}::{nameof(PeekCharacter)} must be non-negative; the lexer should never rely on character look-back.");

        int internalOffset = 0;
        for (int i = 0; i < ahead; i++)
        {
            char _ = PeekCharacterInternal(internalOffset, out int advance);
            internalOffset += advance;
        }

        return PeekCharacterInternal(internalOffset, out stride);

        char PeekCharacterInternal(int at, out int stride)
        {
            stride = 1;
            char c = SafePeekSingleChar(at);

            switch (c)
            {
                case '\\' when Language is SourceLanguage.C && SafePeekSingleChar(at + 1) is '\n' && SafePeekSingleChar(at + 2) == '\r':
                case '\\' when Language is SourceLanguage.C && SafePeekSingleChar(at + 1) is '\r' && SafePeekSingleChar(at + 2) == '\n':
                {
                    stride = 4;
                    return SafePeekSingleChar(at + 3);
                }

                case '\\' when Language is SourceLanguage.C && SafePeekSingleChar(at + 1) is '\n' or '\r':
                {
                    stride = 3;
                    return SafePeekSingleChar(at + 2);
                }

                case '\n' when SafePeekSingleChar(at + 1) is '\r':
                case '\r' when SafePeekSingleChar(at + 1) is '\n':
                {
                    stride = 2;
                    return '\n';
                }

                default: return c;
            }
        }

        char SafePeekSingleChar(int at)
        {
            int peekIndex = _readPosition + at;
            if (peekIndex >= Source.Text.Length)
                return '\0';

            return Source.Text[peekIndex];
        }
    }

    #endregion

    #region Lexer State Management

    public IDisposable PushMode(SourceLanguage language) => PushMode(language, State);
    public IDisposable PushMode(LexerState state) => PushMode(Language, state);
    public IDisposable PushMode(SourceLanguage language, LexerState state) => new ScopedLexerModeDisposable(this, language, state);

    private sealed class ScopedLexerModeDisposable
        : IDisposable
    {
        private readonly Lexer _lexer;
        private readonly SourceLanguage _language;
        private readonly LexerState _state;

        public ScopedLexerModeDisposable(Lexer lexer, SourceLanguage language, LexerState state)
        {
            _lexer = lexer;

            _language = lexer.Language;
            _state = lexer.State;

            lexer.Language = language;
            lexer.State = state;
        }

        public void Dispose()
        {
            _lexer.Language = _language;
            _lexer.State = _state;
        }
    }

    #endregion

    #region Trivia

    private readonly List<Trivium> _trivia = new(16);

    private TriviaList ReadTrivia(bool isLeading)
    {
        if (IsAtEnd)
            return isLeading ? TriviaList.EmptyLeading : TriviaList.EmptyTrailing;

        Context.Assert(_trivia.Count == 0, "Somehow the trivia list already has items in it; someone used it on accident, or it wasn't cleared properly.");

        while (!IsAtEnd)
        {
            var beginLocation = CurrentLocation;
            switch (CurrentCharacter)
            {
                // the character peeker automatically transforms all forms of newline to just \n, so this one case should grab all valid newlines the lexer recognizes.
                case '\n' when !State.HasFlag(LexerState.CPPWithinDirective):
                {
                    Advance();
                    _trivia.Add(new TriviumNewLine(GetRange(beginLocation)));
                    // Trailing trivia always ends with a newline if encountered.
                    if (!isLeading) goto return_trivia;
                } break;

                case ' ' or '\t' or '\v':
                {
                    Advance();
                    while (!IsAtEnd && CurrentCharacter is ' ' or '\t' or '\v')
                        Advance();
                    _trivia.Add(new TriviumWhiteSpace(GetRange(beginLocation)));
                } break;

                case '/' when PeekCharacter(1) == '/':
                {
                    Advance(2);
                    while (!IsAtEnd && CurrentCharacter is not '\n')
                        Advance();
                    _trivia.Add(new TriviumLineComment(GetRange(beginLocation)));
                } break;

                case '/' when PeekCharacter(1) == '*':
                {
                    Advance(2);

                    int depth = 1;
                    while (depth > 0 && !IsAtEnd)
                    {
                        if (CurrentCharacter == '*' && PeekCharacter(1) == '/')
                        {
                            Advance(2);
                            depth--;
                        }
                        else if (Language == SourceLanguage.Laye && CurrentCharacter == '/' && PeekCharacter(1) == '*')
                        {
                            Advance(2);
                            depth++;
                        }
                        else Advance();
                    }

                    if (depth > 0)
                        Context.ErrorUnclosedComment(Source, beginLocation);

                    _trivia.Add(new TriviumDelimitedComment(GetRange(beginLocation)));
                } break;

                // A shebang, `#!`, at the very start of the file is treated as a line comment.
                // This allows running soiurce files as scripts on Unix systems without also making `#` or `#!` line comment sequences anywhere else.
                case '#' when _readPosition == 0 && PeekCharacter(1) == '!':
                {
                    Advance(2);
                    while (!IsAtEnd && CurrentCharacter is not '\n')
                        Advance();
                    _trivia.Add(new TriviumShebangComment(GetRange(beginLocation)));
                } break;

                // when nothing matches, there is no trivia to read; simply return what we currently have.
                default: goto return_trivia;
            }

            Context.Assert(_readPosition > beginLocation.Offset, Source, beginLocation, $"{nameof(CLexer)}::{nameof(ReadTrivia)} failed to consume any non-trivia characters from the source text and did not return the current list of trivia if required.");
        }

        if (_trivia.Count == 0)
            return isLeading ? TriviaList.EmptyLeading : TriviaList.EmptyTrailing;

    return_trivia:;
        var triviaList = new TriviaList(_trivia, isLeading);
        _trivia.Clear();

        return triviaList;
    }

    #endregion

    #region Tokens

    private Token ReadNextTokenRaw()
    {
        var leadingTrivia = ReadTrivia(isLeading: true);
        var beginLocation = CurrentLocation;

        if (IsAtEnd)
        {
            return new(TokenKind.EndOfFile, Language, Source, GetRange(beginLocation))
            {
                LeadingTrivia = leadingTrivia,
                TrailingTrivia = TriviaList.EmptyTrailing,
            };
        }

        StringView stringValue = default;
        BigInteger integerValue = default;
        double floatValue = 0;

        var tokenKind = TokenKind.Invalid;
        var tokenLanguage = Language;

        switch (CurrentCharacter)
        {
            case '\n' when State.HasFlag(LexerState.CPPWithinDirective):
            {
                Context.Assert(Language == SourceLanguage.C, Source, CurrentLocation, "Should only be within a preprocessing directive when lexing for C.");
                Advance();
                tokenKind = TokenKind.DirectiveEnd;
            } break;

            case '<' when State.HasFlag(LexerState.CPPHasHeaderNames):
            {
                Context.Assert(Language == SourceLanguage.C, Source, CurrentLocation, "Should only be accepting a header name when lexing for C.");

                var tokenTextBuilder = new StringBuilder();
                tokenKind = TokenKind.HeaderName;

                Advance();
                while (!IsAtEnd && CurrentCharacter is not '>')
                {
                    tokenTextBuilder.Append(CurrentCharacter);
                    Advance();
                }

                if (!TryAdvance('>'))
                    Context.ErrorExpectedMatchingCloseDelimiter(Source, '<', '>', CurrentLocation, beginLocation);

                stringValue = tokenTextBuilder.ToString();
            } break;

            case '(': Advance(); tokenKind = TokenKind.OpenParen; break;
            case ')': Advance(); tokenKind = TokenKind.CloseParen; break;
            case '[': Advance(); tokenKind = TokenKind.OpenSquare; break;
            case ']': Advance(); tokenKind = TokenKind.CloseSquare; break;
            case '{': Advance(); tokenKind = TokenKind.OpenCurly; break;
            case '}': Advance(); tokenKind = TokenKind.CloseCurly; break;
            case ';': Advance(); tokenKind = TokenKind.SemiColon; break;
            case '?': Advance(); tokenKind = TokenKind.Question; break;
            case '~': Advance(); tokenKind = TokenKind.Tilde; break;

            case '!':
            {
                Advance();
                if (TryAdvance('='))
                    tokenKind = TokenKind.BangEqual;
                else tokenKind = TokenKind.Bang;
            } break;

            case '#':
            {
                Advance();
                if (TryAdvance('#'))
                    tokenKind = TokenKind.PoundPound;
                else tokenKind = TokenKind.Pound;
            } break;

            case '%':
            {
                Advance();
                if (TryAdvance('='))
                    tokenKind = TokenKind.PercentEqual;
                else tokenKind = TokenKind.Percent;
            } break;

            case '&':
            {
                Advance();
                if (TryAdvance('&'))
                    tokenKind = TokenKind.AmpersandAmpersand;
                else if (TryAdvance('='))
                    tokenKind = TokenKind.AmpersandEqual;
                else tokenKind = TokenKind.Ampersand;
            } break;

            case '*':
            {
                Advance();
                if (TryAdvance('='))
                    tokenKind = TokenKind.StarEqual;
                else tokenKind = TokenKind.Star;
            } break;

            case '+':
            {
                Advance();
                if (TryAdvance('+'))
                    tokenKind = TokenKind.PlusPlus;
                else if (TryAdvance('='))
                    tokenKind = TokenKind.PlusEqual;
                else tokenKind = TokenKind.Plus;
            } break;

            case '-':
            {
                Advance();
                if (TryAdvance('-'))
                    tokenKind = TokenKind.MinusMinus;
                else if (TryAdvance('='))
                    tokenKind = TokenKind.MinusEqual;
                else if (TryAdvance('>'))
                    tokenKind = TokenKind.MinusGreater;
                else tokenKind = TokenKind.Minus;
            } break;

            case '.':
            {
                Advance();
                if (CurrentCharacter is '.' && PeekCharacter(1) is '.')
                {
                    Advance(2);
                    tokenKind = TokenKind.DotDotDot;
                }
                else tokenKind = TokenKind.Dot;
            } break;

            case '/':
            {
                Advance();
                if (TryAdvance('='))
                    tokenKind = TokenKind.SlashEqual;
                else tokenKind = TokenKind.Slash;
            } break;

            case ':':
            {
                Advance();
                if (TryAdvance(':'))
                    tokenKind = TokenKind.ColonColon;
                else tokenKind = TokenKind.Colon;
            } break;

            case '<':
            {
                Advance();
                if (TryAdvance('='))
                    tokenKind = TokenKind.LessEqual;
                else if (TryAdvance('<'))
                {
                    if (TryAdvance('='))
                        tokenKind = TokenKind.LessLessEqual;
                    else tokenKind = TokenKind.LessLess;
                }
                else tokenKind = TokenKind.Less;
            } break;

            case '=':
            {
                Advance();
                if (TryAdvance('='))
                    tokenKind = TokenKind.EqualEqual;
                else tokenKind = TokenKind.Equal;
            } break;

            case '>':
            {
                Advance();
                if (TryAdvance('='))
                    tokenKind = TokenKind.GreaterEqual;
                else if (TryAdvance('>'))
                {
                    if (TryAdvance('='))
                        tokenKind = TokenKind.GreaterGreaterEqual;
                    else tokenKind = TokenKind.GreaterGreater;
                }
                else tokenKind = TokenKind.Greater;
            } break;

            case '^':
            {
                Advance();
                if (TryAdvance('='))
                    tokenKind = TokenKind.CaretEqual;
                else tokenKind = TokenKind.Caret;
            } break;

            case '|':
            {
                Advance();
                if (TryAdvance('|'))
                    tokenKind = TokenKind.PipePipe;
                else if (TryAdvance('='))
                    tokenKind = TokenKind.PipeEqual;
                else tokenKind = TokenKind.Pipe;
            } break;

            case '"':
            {
                var tokenTextBuilder = new StringBuilder();
                tokenKind = TokenKind.LiteralString;

                Advance();
                while (!IsAtEnd && CurrentCharacter != '"')
                {
                    if (CurrentCharacter == '\\')
                    {
                        var escapeLocation = CurrentLocation;
                        Advance();

                        switch (CurrentCharacter)
                        {
                            case '\\': Advance(); tokenTextBuilder.Append('\\'); break;
                            case 'n': Advance(); tokenTextBuilder.Append('\n'); break;
                            default: Context.ErrorUnrecognizedEscapeSequence(Source, escapeLocation); break;
                        }
                    }
                    else
                    {
                        tokenTextBuilder.Append(CurrentCharacter);
                        Advance();
                    }
                }

                if (!TryAdvance('"'))
                    Context.ErrorExpectedMatchingCloseDelimiter(Source, '"', '"', CurrentLocation, beginLocation);

                stringValue = tokenTextBuilder.ToString();
            } break;

            case '_' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'):
            {
                var tokenTextBuilder = new StringBuilder();
                tokenTextBuilder.Append(CurrentCharacter);

                tokenKind = TokenKind.Identifier;

                Advance();
                while (CurrentCharacter is '_' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
                {
                    tokenTextBuilder.Append(CurrentCharacter);
                    Advance();
                }

                stringValue = tokenTextBuilder.ToString();
            } break;

            default:
            {
                Context.ErrorUnexpectedCharacter(Source, beginLocation);
                tokenKind = TokenKind.UnexpectedCharacter;
                Advance();
            } break;
        }

        var tokenRange = GetRange(beginLocation);
        Context.Assert(_readPosition > beginLocation.Offset, Source, beginLocation, $"{nameof(Lexer)}::{nameof(ReadNextTokenRaw)} failed to consume any non-trivia characters from the source text and did not return an EOF token.");
        Context.Assert(tokenKind != TokenKind.Invalid, Source, beginLocation, $"{nameof(Lexer)}::{nameof(ReadNextTokenRaw)} failed to assign a non-invalid kind to the read token.");

        var trailingTrivia = ReadTrivia(isLeading: false);
        return new(tokenKind, tokenLanguage, Source, tokenRange)
        {
            StringValue = stringValue,
            IntegerValue = integerValue,
            FloatValue = floatValue,

            LeadingTrivia = leadingTrivia,
            TrailingTrivia = trailingTrivia,
        };
    }

    #endregion

    #region Preprocessing

    public Token ReadNextToken()
    {
        var rawToken = ReadNextTokenRaw();

        if (rawToken.Kind == TokenKind.Identifier)
        {
            var tokenKind = rawToken.Kind;
            if (rawToken.StringValue == "int")
                tokenKind = TokenKind.Int;
            else if (rawToken.StringValue == "pragma")
                tokenKind = TokenKind.Pragma;

            if (tokenKind != rawToken.Kind)
            {
                rawToken = new Token(tokenKind, rawToken.Language, rawToken.Source, rawToken.Range)
                {
                    IsAtStartOfLine = rawToken.IsAtStartOfLine,
                    LeadingTrivia = rawToken.LeadingTrivia,
                    TrailingTrivia = rawToken.TrailingTrivia,
                    StringValue = rawToken.StringValue,
                    IntegerValue = rawToken.IntegerValue,
                    FloatValue = rawToken.FloatValue,
                };
            }
        }

        return rawToken;
    }

    internal void ParseDefineDirective(Token directiveStartToken, Token defineToken)
    {
        using var defineMode = PushMode(SourceLanguage.C, LexerState.CPPWithinDirective);
    }

    internal void ParseIncludeDirective(Token directiveStartToken, Token includeToken)
    {
        using var defineMode = PushMode(SourceLanguage.C, LexerState.CPPHasHeaderNames | LexerState.CPPWithinDirective);
    }

    #endregion
}
