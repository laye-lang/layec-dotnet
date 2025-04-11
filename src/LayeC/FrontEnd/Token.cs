using System.Numerics;

using LayeC.Source;

namespace LayeC.FrontEnd;

public sealed class Token(TokenKind kind, SourceLanguage language, SourceText source, SourceRange range)
    : IHasSourceInfo
    , ITreeDebugNode
{
    public static readonly Token AnyEndOfFile = new(TokenKind.EndOfFile, SourceLanguage.None, SourceText.Unknown, default);

    public TokenKind Kind { get; set; } = kind;
    public SourceLanguage Language { get; set; } = language;
    public SourceText Source { get; } = source;
    public SourceRange Range { get; } = range;
    public SourceLocation Location { get; } = range.Begin;

    private StringView? _overrideSpelling;
    public StringView Spelling
    {
        get => _overrideSpelling ?? Source.Slice(Range);
        set => _overrideSpelling = value;
    }

    public bool IsAtStartOfLine { get; set; } = false;
    public bool IsAtEndOfLine => TrailingTrivia.Trivia.Any(t => t is TriviumNewLine or TriviumLineComment);
    public bool HasWhiteSpaceBefore { get; set; }

    public TriviaList LeadingTrivia { get; set; } = TriviaList.EmptyLeading;
    public TriviaList TrailingTrivia { get; set; } = TriviaList.EmptyTrailing;

    public StringView StringValue { get; set; }
    public BigInteger IntegerValue { get; set; }
    public double FloatValue { get; set; }

    public bool DisableExpansion { get; set; }

    public int CPPIntegerData { get; set; }
    public int MacroParameterIndex => CPPIntegerData;
    public int VAOptCloseParenIndex => CPPIntegerData;

    string ITreeDebugNode.DebugNodeName { get; } = nameof(Token);
    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children
    {
        get
        {
            if (LeadingTrivia.Trivia.Count != 0)
                yield return LeadingTrivia;

            if (TrailingTrivia.Trivia.Count != 0)
                yield return TrailingTrivia;
        }
    }

    public Token Clone()
    {
        return new Token(Kind, Language, Source, Range)
        {
            _overrideSpelling = _overrideSpelling,
            IsAtStartOfLine = IsAtStartOfLine,
            HasWhiteSpaceBefore = HasWhiteSpaceBefore,
            LeadingTrivia = LeadingTrivia.Clone(),
            TrailingTrivia = TrailingTrivia.Clone(),
            StringValue = StringValue,
            IntegerValue = IntegerValue,
            FloatValue = FloatValue,
            DisableExpansion = DisableExpansion,
            CPPIntegerData = CPPIntegerData,
        };
    }
}
