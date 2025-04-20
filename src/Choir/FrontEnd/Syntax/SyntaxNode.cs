using Choir.Source;

namespace Choir.FrontEnd.Syntax;

public abstract class SyntaxNode(SourceText source, SourceRange range)
    : ITreeDebugNode
{
    private static int _counter = 0;

    public readonly int Id = Interlocked.Increment(ref _counter);

    public SourceText Source { get; } = source;
    public SourceRange Range { get; } = range;
    public SourceLocation Location => Range.Begin;

    public virtual bool CanBeType { get; } = false;

    string ITreeDebugNode.DebugNodeName => DebugNodeName;
    protected abstract string DebugNodeName { get; }

    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children => Children;
    protected abstract IEnumerable<ITreeDebugNode> Children { get; }

    public SyntaxNode(Token token)
        : this(token.Source, token.Range)
    {
    }
}
