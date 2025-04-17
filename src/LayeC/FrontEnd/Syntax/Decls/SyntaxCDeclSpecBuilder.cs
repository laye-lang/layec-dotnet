using LayeC.FrontEnd.Syntax.Meta;

namespace LayeC.FrontEnd.Syntax.Decls;

public sealed class SyntaxCDeclSpecBuilder
{
    public SyntaxCAttributesBuilder Attributes { get; } = new();

    public void TakeAttributesFrom(SyntaxCAttributesBuilder attrs)
    {
    }
}
