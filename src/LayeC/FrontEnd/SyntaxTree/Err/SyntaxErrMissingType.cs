
namespace LayeC.FrontEnd.SyntaxTree.Err;

public sealed class SyntaxErrMissingType(Token missingToken)
    : SyntaxNode(missingToken)
{
    public Token Token { get; } = missingToken;

    public override bool CanBeType { get; } = true;

    protected override string DebugNodeName { get; } = nameof(SyntaxErrMissingType);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [missingToken];
}
