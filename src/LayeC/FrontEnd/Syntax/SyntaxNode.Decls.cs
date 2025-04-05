
namespace LayeC.FrontEnd.Syntax;

public sealed class SyntaxDeclFunction(SyntaxNode returnType, Token nameToken, Token openParenToken, Token closeParenToken, Token semiColonToken)
    : SyntaxNode(nameToken)
{
    public SyntaxNode ReturnType { get; } = returnType;
    public Token NameToken { get; } = nameToken;
    public Token OpenParenToken { get; } = openParenToken;
    public Token CloseParenToken { get; } = closeParenToken;
    public Token SemiColonToken { get; } = semiColonToken;

    protected override string DebugNodeName { get; } = nameof(SyntaxDeclFunction);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [returnType, nameToken, openParenToken, closeParenToken, semiColonToken];
}
