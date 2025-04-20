using LayeC.Source;

namespace LayeC.FrontEnd.Syntax.Types;

public sealed class SyntaxCTypeSpecifier(SourceText source, SourceRange range, CTypeSpecifierKind typeSpecKind, List<SyntaxCTypeSpecifierPart> typeSpecParts)
{
    public SourceText Source { get; } = source;
    public SourceRange Range { get; } = range;
    public SourceLocation Location => Range.Begin;

    public CTypeSpecifierKind Kind { get; } = typeSpecKind;
    public IReadOnlyList<SyntaxCTypeSpecifierPart> Parts { get; } = [.. typeSpecParts];
}

public abstract class SyntaxCTypeSpecifierPart(SourceText source, SourceRange range)
{
    public SourceText Source { get; } = source;
    public SourceRange Range { get; } = range;
    public SourceLocation Location => Range.Begin;
}

public sealed class SyntaxCTypeSpecifierPartImplicitInt(Token missingToken)
    : SyntaxCTypeSpecifierPart(missingToken.Source, missingToken.Range)
{
    public Token MissingToken { get; } = missingToken;
}

public sealed class SyntaxCTypeSpecifierPartSimple(Token token)
    : SyntaxCTypeSpecifierPart(token.Source, token.Range)
{
    public Token Token { get; } = token;
}
