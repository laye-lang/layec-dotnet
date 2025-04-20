using LayeC.FrontEnd.Syntax.Types;

namespace LayeC.FrontEnd.Syntax.Decls;

public sealed class SyntaxCDeclarationSpecifiers(SyntaxCTypeSpecifier typeSpec)
{
    public SyntaxCTypeSpecifier TypeSpecifier { get; } = typeSpec;
}
