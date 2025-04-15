namespace LayeC.FrontEnd.SyntaxTree.Err;

public sealed class SyntaxErrLonePragmaC(Token pragmaKeywordToken, Token cStringToken)
    : SyntaxNode(pragmaKeywordToken)
{
    public Token PragmaKeywordToken { get; } = pragmaKeywordToken;
    public Token CStringToken { get; } = cStringToken;

    protected override string DebugNodeName { get; } = nameof(SyntaxErrLonePragmaC);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [pragmaKeywordToken, cStringToken];
}
