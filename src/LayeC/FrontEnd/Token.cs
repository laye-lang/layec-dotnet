using System.Numerics;

using LayeC.Source;

namespace LayeC.FrontEnd;

public sealed class Token(TokenKind kind, SourceLanguage language, SourceText source, SourceRange range)
    : IHasSourceInfo
    , ITreeDebugNode
{
    public static readonly Token AnyEndOfFile = new(TokenKind.EndOfFile, SourceLanguage.None, SourceText.Unknown, default);

    public TokenKind Kind { get; set; } = kind;
    public SourceLanguage Language { get; } = language;
    public SourceText Source { get; } = source;
    public SourceRange Range { get; } = range;
    public SourceLocation Location { get; } = range.Begin;
    public StringView Spelling => Source.Slice(Range);

    public bool IsAtStartOfLine { get; init; } = false;
    public bool IsAtEndOfLine => TrailingTrivia.Trivia.Any(t => t is TriviumNewLine or TriviumLineComment);

    public TriviaList LeadingTrivia { get; init; } = TriviaList.EmptyLeading;
    public TriviaList TrailingTrivia { get; init; } = TriviaList.EmptyTrailing;

    public bool HasWhiteSpaceBefore => LeadingTrivia.Trivia.Count != 0;

    public StringView StringValue { get; init; }
    public BigInteger IntegerValue { get; init; }
    public double FloatValue { get; init; }

    public bool DisableExpansion { get; set; }

    public bool IsCPPMacroParam { get; set; }
    public int CPPMacroParamIndex { get; set; }

    string ITreeDebugNode.DebugNodeName { get; } = nameof(Token);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "I know Array.Empty is quick and constant, there's no need to use a collection expression when the semantics in this case are unclear and may change.")]
    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children { get; } = Array.Empty<ITreeDebugNode>();
}
