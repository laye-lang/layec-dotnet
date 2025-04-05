namespace LayeC.FrontEnd.Syntax;

public sealed class SyntaxQualMut(SyntaxNode inner, Token mutToken)
    : SyntaxNode(inner)
{
    public SyntaxNode Inner { get; } = inner;
    public Token MutToken { get; } = mutToken;
    protected override string DebugNodeName { get; } = nameof(SyntaxTypeBuiltIn);
    protected override IEnumerable<ITreeDebugNode> Children { get; } =
        [.. ((ITreeDebugNode[])[inner, mutToken]).OrderBy(n => ((IHasSourceInfo)n).Range.Begin.Offset)];
}

public sealed class SyntaxTypeBuiltIn(Token keywordToken)
    : SyntaxNode(keywordToken)
{
    public Token KeywordToken { get; } = keywordToken;
    protected override string DebugNodeName { get; } = nameof(SyntaxTypeBuiltIn);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [keywordToken];
}
