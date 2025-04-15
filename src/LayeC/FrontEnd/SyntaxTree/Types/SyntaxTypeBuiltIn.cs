namespace LayeC.FrontEnd.SyntaxTree.Types;

public sealed class SyntaxTypeBuiltIn(BuiltInTypeKind kind, Token token)
    : SyntaxNode(token)
{
    public BuiltInTypeKind Kind { get; } = kind;
    public Token Token { get; } = token;

    protected override string DebugNodeName { get; } = nameof(SyntaxTypeBuiltIn);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [token];
}
