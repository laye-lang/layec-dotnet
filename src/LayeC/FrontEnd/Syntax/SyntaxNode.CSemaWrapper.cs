using LayeC.FrontEnd.Semantics;

namespace LayeC.FrontEnd.Syntax;

public sealed class SyntaxCNode(SemaNode semaNode)
    : SyntaxNode(semaNode.Source, semaNode.Range)
{
    public SemaNode SemaNode { get; } = semaNode;

    protected override string DebugNodeName { get; } = nameof(SyntaxCNode);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [semaNode];
}
