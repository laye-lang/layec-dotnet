using LayeC.Source;

namespace LayeC.FrontEnd.Syntax;

public abstract class SyntaxNode(SourceText source, SourceRange range)
    : IHasSourceInfo
    , ITreeDebugNode
{
    private static long _counter = 0;

    public long Id { get; } = Interlocked.Increment(ref _counter);

    public SourceText Source { get; } = source;
    public SourceRange Range { get; } = range;
    public SourceLocation Location => Range.Begin;

    public virtual bool CanBeType { get; } = false;

    string ITreeDebugNode.DebugNodeName => DebugNodeName;
    protected abstract string DebugNodeName { get; }

    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children => Children;
    protected abstract IEnumerable<ITreeDebugNode> Children { get; }

    protected SyntaxNode(Token token)
        : this(token.Source, token.Range)
    {
    }

    protected SyntaxNode(SyntaxNode child)
        : this(child.Source, child.Range)
    {
    }
}
