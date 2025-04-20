using Choir.Source;

namespace Choir.FrontEnd;

public abstract class Trivium(SourceText source, SourceRange range, string debugName)
    : ITreeDebugNode
{
    public SourceText Source { get; } = source;
    public SourceRange Range { get; } = range;
    public SourceLocation Location => Range.Begin;

    string ITreeDebugNode.DebugNodeName { get; } = debugName;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "I know Array.Empty is quick and constant, there's no need to use a collection expression when the semantics in this case are unclear and may change.")]
    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children { get; } = Array.Empty<ITreeDebugNode>();
}

public sealed class TriviaList(IEnumerable<Trivium> trivia, bool isLeading)
    : ITreeDebugNode
{
    public static readonly TriviaList EmptyLeading = new([], true);
    public static readonly TriviaList EmptyTrailing = new([], false);

    public IReadOnlyList<Trivium> Trivia { get; } = [.. trivia];
    public bool IsLeading { get; } = isLeading;

    public SourceLocation Location => Trivia.Count == 0 ? default : Trivia[0].Location;

    string ITreeDebugNode.DebugNodeName { get; } = nameof(TriviaList);
    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children { get; } = [.. trivia];

    public TriviaList Clone()
    {
        if (Trivia.Count == 0)
            return IsLeading ? EmptyLeading : EmptyTrailing;

        return new TriviaList(Trivia, IsLeading);
    }
}

public sealed class TriviumLiteral(StringView literal)
    : Trivium(SourceText.Unknown, default, nameof(TriviumLiteral))
{
    public StringView Literal { get; } = literal;
}

public sealed class TriviumShebangComment(SourceText source, SourceRange range) : Trivium(source, range, nameof(TriviumShebangComment));
public sealed class TriviumWhiteSpace(SourceText source, SourceRange range) : Trivium(source, range, nameof(TriviumWhiteSpace));
public sealed class TriviumNewLine(SourceText source, SourceRange range) : Trivium(source, range, nameof(TriviumNewLine));
public sealed class TriviumLineComment(SourceText source, SourceRange range) : Trivium(source, range, nameof(TriviumLineComment));
public sealed class TriviumDelimitedComment(SourceText source, SourceRange range) : Trivium(source, range, nameof(TriviumDelimitedComment));
