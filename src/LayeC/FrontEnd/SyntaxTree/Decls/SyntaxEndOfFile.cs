
namespace LayeC.FrontEnd.SyntaxTree.Decls;

public sealed class SyntaxEndOfFile(Token endOfFileToken)
    : SyntaxNode(endOfFileToken.Source, endOfFileToken.Range)
{
    public Token EndOfFileToken { get; } = endOfFileToken;

    protected override string DebugNodeName { get; } = nameof(SyntaxEndOfFile);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [endOfFileToken];
}
