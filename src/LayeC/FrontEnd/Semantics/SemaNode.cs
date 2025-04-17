using LayeC.Source;

namespace LayeC.FrontEnd.Semantics;

public abstract class SemaNode(SourceText source, SourceRange range)
    : ITreeDebugNode
{
    private static int _counter = 0;

    public readonly int Id = Interlocked.Increment(ref _counter);

    public SourceText Source { get; } = source;
    public SourceRange Range { get; } = range;
    public SourceLocation Location => Range.Begin;

    string ITreeDebugNode.DebugNodeName => DebugNodeName;
    protected abstract string DebugNodeName { get; }

    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children => Children;
    protected abstract IEnumerable<ITreeDebugNode> Children { get; }

    public SemaNode(Token token)
        : this(token.Source, token.Range)
    {
    }

    public SemaNode(SemaNode child)
        : this(child.Source, child.Range)
    {
    }
}
