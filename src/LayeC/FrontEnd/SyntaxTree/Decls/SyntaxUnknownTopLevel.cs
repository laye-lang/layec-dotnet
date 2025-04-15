namespace LayeC.FrontEnd.SyntaxTree.Decls;

public sealed class SyntaxUnknownTopLevel(IEnumerable<SyntaxNode> associatedNodes, Token token)
    : SyntaxNode(token)
{
    public IReadOnlyList<SyntaxNode> AssociatedNodes { get; } = [.. associatedNodes];
    public Token Token { get; } = token;

    protected override string DebugNodeName { get; } = nameof(SyntaxUnknownTopLevel);
    protected override IEnumerable<ITreeDebugNode> Children
    {
        get
        {
            foreach (var node in AssociatedNodes)
                yield return node;

            yield return Token;
        }
    }
}
