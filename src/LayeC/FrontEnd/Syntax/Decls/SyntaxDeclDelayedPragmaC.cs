namespace LayeC.FrontEnd.Syntax.Decls;

public sealed class SyntaxDeclDelayedPragmaC(Token pragmaKeywordToken, Token cStringToken, Token openCurlyToken, IEnumerable<Token> cSyntaxTokens, Token closeCurlyToken)
    : SyntaxNode(pragmaKeywordToken.Source, pragmaKeywordToken.Range)
{
    public Token PragmaKeywordToken { get; } = pragmaKeywordToken;
    public Token CStringToken { get; } = cStringToken;
    public Token OpenCurlyToken { get; } = openCurlyToken;
    public Token CloseCurlyToken { get; } = closeCurlyToken;

    public IReadOnlyList<Token> CSyntaxTokens { get; } = [.. cSyntaxTokens];

    protected override string DebugNodeName { get; } = nameof(SyntaxDeclDelayedPragmaC);
    protected override IEnumerable<ITreeDebugNode> Children
    {
        get
        {
            yield return PragmaKeywordToken;
            yield return CStringToken;
            yield return OpenCurlyToken;

            foreach (var token in CSyntaxTokens)
                yield return token;

            yield return CloseCurlyToken;
        }
    }
}
