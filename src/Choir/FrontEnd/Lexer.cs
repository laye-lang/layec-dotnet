using System.Numerics;
using System.Text;

using Choir.Source;

namespace Choir.FrontEnd;

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

    /// <summary>
    /// Flag set to indicate we're in a conditional branch that was not selected by the preprocessor.
    /// This will suppress lexer error messages, but otherwise continue on as normal.
    /// </summary>
    CPPWithinRejectedBranch = 1 << 2,
}

public sealed class PreprocessorIfState(Token directiveToken)
{
    public Token IfDirectiveToken { get; } = directiveToken;
    public bool HasBranchBeenTaken { get; set; } = false;
}

public sealed class Lexer(CompilerContext context, SourceText source, LanguageOptions languageOptions)
{
    public CompilerContext Context { get; } = context;
    public SourceText Source { get; } = source;

    public LanguageOptions LanguageOptions { get; } = languageOptions;

    public SourceLanguage Language { get; private set; } = source.Language;
    public LexerState State { get; set; } = LexerState.None;

    public SourceLocation EndOfFileLocation { get; } = new(source.Length);

    #region Public PP Condition Tracking

    public Stack<PreprocessorIfState> PreprocessorIfStates { get; } = [];
    public int PreprocessorIfDepth => PreprocessorIfStates.Count;

    #endregion

    #region Source Character Processing

    private int _readPosition;
    private bool _isAtStartOfLine = true;
    private bool _hasWhiteSpaceBefore = false;

    public bool IsAtEnd => _readPosition >= Source.Text.Length;
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

    private bool SuppressDiagnostics => State.HasFlag(LexerState.CPPWithinRejectedBranch);

    public IDisposable PushMode(SourceLanguage language) => PushMode(language, State);
    public IDisposable PushMode(LexerState state) => PushMode(Language, state);
    public IDisposable PushMode(SourceLanguage language, LexerState state, Action? onExit = null) => new ScopedLexerModeDisposable(this, language, state, onExit);

    private sealed class ScopedLexerModeDisposable
        : IDisposable
    {
        private readonly Lexer _lexer;
        private readonly SourceLanguage _language;
        private readonly LexerState _state;
        private readonly Action? _onExit;

        public ScopedLexerModeDisposable(Lexer lexer, SourceLanguage language, LexerState state, Action? onExit)
        {
            _lexer = lexer;

            _language = lexer.Language;
            _state = lexer.State;

            _onExit = onExit;

            lexer.Language = language;
            lexer.State = state;
        }

        public void Dispose()
        {
            _lexer.Language = _language;
            _lexer.State = _state;
            _onExit?.Invoke();
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
                    _hasWhiteSpaceBefore = true;
                    Advance();
                    _trivia.Add(new TriviumNewLine(Source, GetRange(beginLocation)));
                    // Trailing trivia always ends with a newline if encountered.
                    if (!isLeading) goto return_trivia;
                } break;

                case ' ' or '\t' or '\v':
                {
                    _hasWhiteSpaceBefore = true;
                    Advance();
                    while (!IsAtEnd && CurrentCharacter is ' ' or '\t' or '\v')
                        Advance();
                    _trivia.Add(new TriviumWhiteSpace(Source, GetRange(beginLocation)));
                } break;

                // line comments are not available in C89 without extensions enabled. Laye is fine.
                case '/' when PeekCharacter(1) == '/' && (Language == SourceLanguage.Laye || LanguageOptions.CHasLineComments):
                {
                    _hasWhiteSpaceBefore = true;
                    Advance(2);
                    while (!IsAtEnd && CurrentCharacter is not '\n')
                        Advance();
                    _trivia.Add(new TriviumLineComment(Source, GetRange(beginLocation)));
                } break;

                case '/' when PeekCharacter(1) == '*':
                {
                    _hasWhiteSpaceBefore = true;

                    Advance(2);

                    int depth = 1;
                    while (depth > 0 && !IsAtEnd)
                    {
                        if (CurrentCharacter == '*' && PeekCharacter(1) == '/')
                        {
                            Advance(2);
                            depth--;
                        }
                        // don't handle nested delimited comments in C, only in Laye.
                        else if (Language == SourceLanguage.Laye && CurrentCharacter == '/' && PeekCharacter(1) == '*')
                        {
                            Advance(2);
                            depth++;
                        }
                        else Advance();
                    }

                    if (depth > 0 && !SuppressDiagnostics)
                        Context.ErrorUnclosedComment(Source, beginLocation);

                    _trivia.Add(new TriviumDelimitedComment(Source, GetRange(beginLocation)));
                } break;

                // A shebang, `#!`, at the very start of the file is treated as a line comment.
                // This allows running soiurce files as scripts on Unix systems without also making `#` or `#!` line comment sequences anywhere else.
                case '#' when _readPosition == 0 && PeekCharacter(1) == '!':
                {
                    _hasWhiteSpaceBefore = true;
                    Advance(2);
                    while (!IsAtEnd && CurrentCharacter is not '\n')
                        Advance();
                    _trivia.Add(new TriviumShebangComment(Source, GetRange(beginLocation)));
                } break;

                // when nothing matches, there is no trivia to read; simply return what we currently have.
                default: goto return_trivia;
            }

            Context.Assert(_readPosition > beginLocation.Offset, Source, beginLocation, $"{nameof(Lexer)}::{nameof(ReadTrivia)} failed to consume any non-trivia characters from the source text and did not return the current list of trivia if required.");
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

    public Token ReadNextPPToken()
    {
        var leadingTrivia = ReadTrivia(isLeading: true);
        var beginLocation = CurrentLocation;

        if (IsAtEnd)
        {
            return new(State.HasFlag(LexerState.CPPWithinDirective) ? TokenKind.CPPDirectiveEnd : TokenKind.EndOfFile, Language, Source, GetRange(beginLocation))
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
        bool isAtStartOfLine = _isAtStartOfLine;
        bool hasWhiteSpaceBefore = _hasWhiteSpaceBefore;

        _isAtStartOfLine = false;
        _hasWhiteSpaceBefore = false;

        char c = CurrentCharacter;
        Advance();

        switch (c)
        {
            case '\n' when State.HasFlag(LexerState.CPPWithinDirective):
            {
                Context.Assert(Language == SourceLanguage.C, Source, CurrentLocation, "Should only be within a preprocessing directive when lexing for C.");
                tokenKind = TokenKind.CPPDirectiveEnd;
            } break;

            case '<' when State.HasFlag(LexerState.CPPHasHeaderNames):
            {
                Context.Assert(Language == SourceLanguage.C, Source, CurrentLocation, "Should only be accepting a header name when lexing for C.");

                var tokenTextBuilder = new StringBuilder();
                tokenKind = TokenKind.CPPHeaderName;

                while (!IsAtEnd && CurrentCharacter is not '>')
                {
                    tokenTextBuilder.Append(CurrentCharacter);
                    Advance();
                }

                if (!TryAdvance('>') && !SuppressDiagnostics)
                    Context.ErrorExpectedMatchingCloseDelimiter(Source, '<', '>', CurrentLocation, beginLocation);

                stringValue = tokenTextBuilder.ToString();
            } break;

            case >= '0' and <= '9' when Language == SourceLanguage.C:
            case '.' when CurrentCharacter is >= '0' and <= '9' && Language == SourceLanguage.C:
            {
                tokenKind = TokenKind.CPPNumber;
                while (!IsAtEnd)
                {
                    if (CurrentCharacter is '.')
                        Advance();
                    else if (CurrentCharacter is '\'' && PeekCharacter(1) is (>= '0' and <= '9') or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or '$')
                        Advance(2);
                    else if (CurrentCharacter is 'e' or 'E' or 'p' or 'P' && PeekCharacter(1) is '+' or '-')
                        Advance(2);
                    else if (SyntaxFacts.IsCIdentifierContinue(CurrentCharacter))
                        Advance();
                    else break;
                }
            } break;

            case '"' or '\'':
            {
                char delimiter = c;

                var tokenTextBuilder = new StringBuilder();
                tokenKind = delimiter == '"' ? TokenKind.LiteralString : TokenKind.LiteralCharacter;

                while (!IsAtEnd && CurrentCharacter != delimiter)
                {
                    if (CurrentCharacter == '\n')
                    {
                        if (!SuppressDiagnostics)
                            Context.ErrorUnclosedStringOrCharacterLiteral(Source, CurrentLocation, delimiter == '"' ? "string" : "character");
                        goto done_lexing_string_or_character;
                    }
                    else if (CurrentCharacter == '\\')
                    {
                        var escapeLocation = CurrentLocation;
                        Advance();

                        switch (CurrentCharacter)
                        {
                            case 'n': Advance(); tokenTextBuilder.Append('\n'); break;
                            case 'r': Advance(); tokenTextBuilder.Append('\r'); break;
                            case 't': Advance(); tokenTextBuilder.Append('\t'); break;
                            case 'b': Advance(); tokenTextBuilder.Append('\b'); break;
                            case 'f': Advance(); tokenTextBuilder.Append('\f'); break;
                            case 'a': Advance(); tokenTextBuilder.Append('\a'); break;
                            case 'v': Advance(); tokenTextBuilder.Append('\v'); break;
                            case '0': Advance(); tokenTextBuilder.Append('\0'); break;
                            case '\\': Advance(); tokenTextBuilder.Append('\\'); break;
                            case '\"': Advance(); tokenTextBuilder.Append('\"'); break;
                            case '\'': Advance(); tokenTextBuilder.Append('\''); break;
                            default: if (!SuppressDiagnostics) Context.ErrorUnrecognizedEscapeSequence(Source, escapeLocation); break;
                        }
                    }
                    else
                    {
                        tokenTextBuilder.Append(CurrentCharacter);
                        Advance();
                    }
                }

                if (!TryAdvance(delimiter) && !SuppressDiagnostics)
                    Context.ErrorExpectedMatchingCloseDelimiter(Source, delimiter, delimiter, CurrentLocation, beginLocation);

            done_lexing_string_or_character:;
                if (delimiter == '\'' && tokenTextBuilder.Length != 1 && !SuppressDiagnostics)
                    Context.ErrorTooManyCharactersInCharacterLiteral(Source, beginLocation);

                stringValue = tokenTextBuilder.ToString();
            } break;

            case (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or '$':
            case char when SyntaxFacts.IsIdentifierStart(Language, c):
            {
                Context.Assert(SyntaxFacts.IsIdentifierStart(Language, c), $"Inline pattern failed for expected identifier start character '{c}'.");

                var tokenTextBuilder = new StringBuilder();
                tokenTextBuilder.Append(c);

                tokenKind = Language == SourceLanguage.C ? TokenKind.CPPIdentifier : TokenKind.Identifier;
                //tokenKind = TokenKind.CPPIdentifier;

                while (SyntaxFacts.IsIdentifierContinue(Language, CurrentCharacter))
                {
                    tokenTextBuilder.Append(CurrentCharacter);
                    Advance();
                }

                stringValue = tokenTextBuilder.ToString();
            } break;

            case '#' when Language == SourceLanguage.Laye && SyntaxFacts.IsIdentifierStart(Language, CurrentCharacter):
            {
                Context.Assert(SyntaxFacts.IsIdentifierStart(Language, CurrentCharacter), $"Inline pattern failed for expected identifier start character '{c}'.");

                var tokenTextBuilder = new StringBuilder();
                tokenKind = TokenKind.CPPIdentifier;

                while (SyntaxFacts.IsIdentifierContinue(Language, CurrentCharacter))
                {
                    tokenTextBuilder.Append(CurrentCharacter);
                    Advance();
                }

                stringValue = tokenTextBuilder.ToString();
            } break;

            case '@' when Language == SourceLanguage.Laye && SyntaxFacts.IsIdentifierStart(Language, CurrentCharacter):
            {
                Context.Assert(SyntaxFacts.IsIdentifierStart(Language, CurrentCharacter), $"Inline pattern failed for expected identifier start character '{c}'.");

                var tokenTextBuilder = new StringBuilder();
                tokenKind = TokenKind.CPPLayeMacroWrapperIdentifier;

                while (SyntaxFacts.IsIdentifierContinue(Language, CurrentCharacter))
                {
                    tokenTextBuilder.Append(CurrentCharacter);
                    Advance();
                }

                stringValue = tokenTextBuilder.ToString();
            } break;

            case >= '0' and <= '9':
            {
                tokenKind = TokenKind.LiteralInteger;
                while (CurrentCharacter is >= '0' and <= '9')
                    Advance();
            } break;

            case '#':
            {
                if (Language == SourceLanguage.C && TryAdvance('#'))
                    tokenKind = TokenKind.HashHash;
                else if (Language == SourceLanguage.Laye && TryAdvance('['))
                    tokenKind = TokenKind.HashSquare;
                else tokenKind = TokenKind.Hash;
            } break;

            case '(': tokenKind = TokenKind.OpenParen; break;
            case ')': tokenKind = TokenKind.CloseParen; break;
            case '[': tokenKind = TokenKind.OpenSquare; break;
            case ']': tokenKind = TokenKind.CloseSquare; break;
            case '{': tokenKind = TokenKind.OpenCurly; break;
            case '}': tokenKind = TokenKind.CloseCurly; break;

            case ',': tokenKind = TokenKind.Comma; break;
            case ';': tokenKind = TokenKind.SemiColon; break;

            case '.':
            {
                if (Language != SourceLanguage.Laye && CurrentCharacter is '.' && PeekCharacter(1) is '.')
                {
                    Advance(2);
                    tokenKind = TokenKind.DotDotDot;
                }
                else if (Language == SourceLanguage.Laye && TryAdvance('.'))
                {
                    if (TryAdvance('='))
                        tokenKind = TokenKind.DotDotEqual;
                    else tokenKind = TokenKind.DotDot;
                }
                else tokenKind = TokenKind.Dot;
            } break;

            case ':':
            {
                if (TryAdvance(':'))
                    tokenKind = TokenKind.ColonColon;
                else if (Language == SourceLanguage.C && LanguageOptions.CHasDigraphs && TryAdvance('>'))
                    tokenKind = TokenKind.CloseSquare;
                else tokenKind = TokenKind.Colon;
            } break;

            case '=':
            {
                if (TryAdvance('='))
                    tokenKind = TokenKind.EqualEqual;
                else if (Language == SourceLanguage.Laye && TryAdvance('>'))
                    tokenKind = TokenKind.EqualGreater;
                else tokenKind = TokenKind.Equal;
            } break;

            case '!':
            {
                if (TryAdvance('='))
                    tokenKind = TokenKind.BangEqual;
                else tokenKind = TokenKind.Bang;
            } break;

            case '<':
            {
                if (TryAdvance('='))
                {
                    if (Language == SourceLanguage.Laye && TryAdvance('>'))
                        tokenKind = TokenKind.LessEqualGreater;
                    else tokenKind = TokenKind.LessEqual;
                }
                else if (TryAdvance('<'))
                {
                    if (TryAdvance('='))
                        tokenKind = TokenKind.LessLessEqual;
                    else tokenKind = TokenKind.LessLess;
                }
                else if (Language == SourceLanguage.C && LanguageOptions.CHasDigraphs && TryAdvance(':'))
                    tokenKind = TokenKind.OpenSquare;
                else if (Language == SourceLanguage.C && LanguageOptions.CHasDigraphs && TryAdvance('%'))
                    tokenKind = TokenKind.OpenCurly;
                else tokenKind = TokenKind.Less;
            } break;

            case '>':
            {
                if (TryAdvance('='))
                    tokenKind = TokenKind.GreaterEqual;
                else if (TryAdvance('>'))
                {
                    if (Language == SourceLanguage.Laye && TryAdvance('>'))
                    {
                        if (TryAdvance('='))
                            tokenKind = TokenKind.GreaterGreaterGreaterEqual;
                        else tokenKind = TokenKind.GreaterGreaterGreater;
                    }
                    else if (TryAdvance('='))
                        tokenKind = TokenKind.GreaterGreaterEqual;
                    else tokenKind = TokenKind.GreaterGreater;
                }
                else tokenKind = TokenKind.Greater;
            } break;

            case '+':
            {
                if (TryAdvance('+'))
                    tokenKind = TokenKind.PlusPlus;
                else if (TryAdvance('='))
                    tokenKind = TokenKind.PlusEqual;
                else tokenKind = TokenKind.Plus;
            } break;

            case '-':
            {
                if (TryAdvance('-'))
                    tokenKind = TokenKind.MinusMinus;
                else if (TryAdvance('='))
                    tokenKind = TokenKind.MinusEqual;
                else if (TryAdvance('>'))
                    tokenKind = TokenKind.MinusGreater;
                else tokenKind = TokenKind.Minus;
            } break;

            case '*': tokenKind = TryAdvance('=') ? TokenKind.StarEqual : TokenKind.Star; break;
            case '/': tokenKind = TryAdvance('=') ? TokenKind.SlashEqual : TokenKind.Slash; break;

            case '%':
            {
                if (TryAdvance('='))
                    tokenKind = TokenKind.PercentEqual;
                else if (Language == SourceLanguage.C && LanguageOptions.CHasDigraphs && TryAdvance('>'))
                    tokenKind = TokenKind.CloseCurly;
                else if (Language == SourceLanguage.C && LanguageOptions.CHasDigraphs && TryAdvance(':'))
                {
                    if (CurrentCharacter == '%' && PeekCharacter(1) == ':')
                    {
                        Advance(2);
                        tokenKind = TokenKind.HashHash;
                    }
                    else tokenKind = TokenKind.Hash;
                }
                else tokenKind = TokenKind.Percent;
            } break;

            case '^': tokenKind = TryAdvance('=') ? TokenKind.CaretEqual : TokenKind.Caret; break;
            case '~': tokenKind = Language == SourceLanguage.Laye && TryAdvance('=') ? TokenKind.TildeEqual : TokenKind.Tilde; break;

            case '&':
            {
                if (TryAdvance('&'))
                    tokenKind = TokenKind.AmpersandAmpersand;
                else if (TryAdvance('='))
                    tokenKind = TokenKind.AmpersandEqual;
                else tokenKind = TokenKind.Ampersand;
            } break;

            case '|':
            {
                if (TryAdvance('|'))
                    tokenKind = TokenKind.PipePipe;
                else if (TryAdvance('='))
                    tokenKind = TokenKind.PipeEqual;
                else tokenKind = TokenKind.Pipe;
            } break;

            case '?':
            {
                if (Language == SourceLanguage.Laye && TryAdvance('?'))
                {
                    if (TryAdvance('='))
                        tokenKind = TokenKind.QuestionQuestionEqual;
                    else tokenKind = TokenKind.QuestionQuestion;
                }
                else tokenKind = TokenKind.Question;
            } break;

            default:
            {
                if (!SuppressDiagnostics)
                    Context.ErrorUnexpectedCharacter(Source, beginLocation);
                tokenKind = TokenKind.UnexpectedCharacter;
                Advance();
            } break;
        }

        var tokenRange = GetRange(beginLocation);
        Context.Assert(_readPosition > beginLocation.Offset, Source, beginLocation, $"{nameof(Lexer)}::{nameof(ReadNextPPToken)} failed to consume any non-trivia characters from the source text and did not return an EOF token.");
        Context.Assert(tokenKind != TokenKind.Invalid, Source, beginLocation, $"{nameof(Lexer)}::{nameof(ReadNextPPToken)} failed to assign a non-invalid kind to the read token.");

        var trailingTrivia = tokenKind == TokenKind.CPPDirectiveEnd ? TriviaList.EmptyTrailing : ReadTrivia(isLeading: false);

        if (tokenLanguage == SourceLanguage.Laye && tokenKind == TokenKind.Identifier)
        {
            if (LanguageOptions.TryGetLayeKeywordKind(stringValue, out var keywordTokenKind))
                tokenKind = keywordTokenKind;
            else if (stringValue.StartsWith("bool") && stringValue[4..].All(char.IsAsciiDigit))
            {
                tokenKind = TokenKind.KWBoolSized;
                integerValue = int.Parse(stringValue[4..]);

                if ((integerValue < 1 || integerValue >= 65535) && !SuppressDiagnostics)
                    Context.ErrorBitWidthOutOfRange(Source, beginLocation);
            }
            else if (stringValue.StartsWith("int") && stringValue[3..].All(char.IsAsciiDigit))
            {
                tokenKind = TokenKind.KWIntSized;
                integerValue = int.Parse(stringValue[3..]);

                if ((integerValue < 1 || integerValue >= 65535) && !SuppressDiagnostics)
                    Context.ErrorBitWidthOutOfRange(Source, beginLocation);
            }
        }

        return new(tokenKind, tokenLanguage, Source, tokenRange)
        {
            IsAtStartOfLine = isAtStartOfLine,
            HasWhiteSpaceBefore = hasWhiteSpaceBefore,

            LeadingTrivia = leadingTrivia,
            TrailingTrivia = trailingTrivia,

            StringValue = stringValue,
            IntegerValue = integerValue,
            FloatValue = floatValue,
        };
    }

    #endregion
}
