using LayeC.FrontEnd.Semantics;
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

    public SemaNode? SourceCNode { get; set; }

    string ITreeDebugNode.DebugNodeName => DebugNodeName;
    protected abstract string DebugNodeName { get; }

    IEnumerable<ITreeDebugNode> ITreeDebugNode.Children => SourceCNode is { } sourceCNode ? [.. Children, sourceCNode] : Children;
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

public sealed class SyntaxModuleUnit(SourceText source, IReadOnlyList<SyntaxNode> topLevelNodes)
    : SyntaxNode(source, default)
{
    public IReadOnlyList<SyntaxNode> TopLevelNodes { get; } = [.. topLevelNodes];
    protected override string DebugNodeName { get; } = nameof(SyntaxModuleUnit);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [.. topLevelNodes];
}

public sealed class SyntaxEndOfFile(Token eofToken)
    : SyntaxNode(eofToken)
{
    public Token EndOfFileToken { get; } = eofToken;
    protected override string DebugNodeName { get; } = nameof(SyntaxEndOfFile);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [eofToken];
}
