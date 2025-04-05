using LayeC.Source;

namespace LayeC.FrontEnd;

public abstract class Trivium(SourceRange range, string debugName)
    : ITreeDebugNode
{
    public SourceRange Range { get; } = range;

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

    string ITreeDebugNode.DebugNodeName { get; } = nameof(TriviaList);
    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children { get; } = trivia;
}

public sealed class TriviumShebangComment(SourceRange range) : Trivium(range, nameof(TriviumShebangComment));
public sealed class TriviumWhiteSpace(SourceRange range) : Trivium(range, nameof(TriviumWhiteSpace));
public sealed class TriviumNewLine(SourceRange range) : Trivium(range, nameof(TriviumNewLine));
public sealed class TriviumLineComment(SourceRange range) : Trivium(range, nameof(TriviumLineComment));
public sealed class TriviumDelimitedComment(SourceRange range) : Trivium(range, nameof(TriviumDelimitedComment));
