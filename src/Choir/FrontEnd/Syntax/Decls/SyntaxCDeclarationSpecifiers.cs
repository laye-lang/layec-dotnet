using Choir.FrontEnd.Syntax.Types;

namespace Choir.FrontEnd.Syntax.Decls;

public sealed class SyntaxCDeclarationSpecifiers(SyntaxCTypeSpecifier typeSpec)
{
    public SyntaxCTypeSpecifier TypeSpecifier { get; } = typeSpec;
}
