namespace LayeC.FrontEnd.SyntaxTree.Exprs;

public sealed class SyntaxExprDelayedPragmaC(Token pragmaKeywordToken, Token cStringToken, Token openParenToken, IEnumerable<Token> cSyntaxTokens, Token closeParenToken)
    : SyntaxNode(pragmaKeywordToken.Source, pragmaKeywordToken.Range)
{
    public Token PragmaKeywordToken { get; } = pragmaKeywordToken;
    public Token CStringToken { get; } = cStringToken;
    public Token OpenParenToken { get; } = openParenToken;
    public Token CloseParenToken { get; } = closeParenToken;

    public IReadOnlyList<Token> CSyntaxTokens { get; } = [.. cSyntaxTokens];

    protected override string DebugNodeName { get; } = nameof(SyntaxExprDelayedPragmaC);
    protected override IEnumerable<ITreeDebugNode> Children
    {
        get
        {
            yield return PragmaKeywordToken;
            yield return CStringToken;
            yield return OpenParenToken;

            foreach (var token in CSyntaxTokens)
                yield return token;

            yield return CloseParenToken;
        }
    }
}
