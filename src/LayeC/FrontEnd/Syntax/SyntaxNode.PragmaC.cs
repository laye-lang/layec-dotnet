namespace LayeC.FrontEnd.Syntax;

public sealed class SyntaxTopLevelPragmaC(Token pragmaToken, Token stringToken, Token openParenToken, IReadOnlyList<SyntaxNode> cNodes, Token closeParenToken)
    : SyntaxNode(pragmaToken)
{
    public Token PragmaToken { get; } = pragmaToken;
    public Token StringToken { get; } = stringToken;
    public Token OpenParenToken { get; } = openParenToken;
    public IReadOnlyList<SyntaxNode> TopLevelNodes { get; } = [.. cNodes];
    public Token CloseParenToken { get; } = closeParenToken;

    protected override string DebugNodeName { get; } = nameof(SyntaxTopLevelPragmaC);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [pragmaToken, stringToken, openParenToken, .. cNodes, closeParenToken];
}

public sealed class SyntaxExprPragmaC(Token pragmaToken, Token stringToken, Token openParenToken, SyntaxNode cExpr, Token closeParenToken)
    : SyntaxNode(pragmaToken)
{
    public Token PragmaToken { get; } = pragmaToken;
    public Token StringToken { get; } = stringToken;
    public Token OpenParenToken { get; } = openParenToken;
    public SyntaxNode Expression { get; } = cExpr;
    public Token CloseParenToken { get; } = closeParenToken;

    protected override string DebugNodeName { get; } = nameof(SyntaxExprPragmaC);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [pragmaToken, stringToken, openParenToken, cExpr, closeParenToken];
}
