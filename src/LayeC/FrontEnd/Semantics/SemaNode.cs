using LayeC.FrontEnd.Syntax;
using LayeC.Source;

namespace LayeC.FrontEnd.Semantics;

public abstract class SemaNode(SourceText source, SourceRange range)
    : ITreeDebugNode
{
    private static long _counter = 0;

    public long Id { get; } = Interlocked.Increment(ref _counter);

    public SourceText Source { get; } = source;
    public SourceRange Range { get; } = range;
    public SourceLocation Location => Range.Begin;

    /// <summary>
    /// If this is node was generated from C source, this is the node which it was generated from.
    /// </summary>
    public SemaNode? UnderlyingCNode { get; set; }

    string ITreeDebugNode.DebugNodeName => DebugNodeName;
    protected abstract string DebugNodeName { get; }

    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children => Children;
    protected abstract IEnumerable<ITreeDebugNode> Children { get; }

    protected SemaNode(Token token)
        : this(token.Source, token.Range)
    {
    }

    protected SemaNode(SyntaxNode child)
        : this(child.Source, child.Range)
    {
    }
}
