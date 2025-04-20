using Choir.FrontEnd.Semantics.Decls;
using Choir.FrontEnd.Syntax.Meta;

namespace Choir.FrontEnd;

public sealed partial class CParser
{
    /// <summary>
    /// Returns false at EOF.
    /// </summary>
    public bool ParseTopLevel(out SemaDecl decl)
    {
        List<SyntaxCAttribute> declAttrs = [];
        List<SyntaxCAttribute> declSpecAttrs = [];

        while (MaybeParseC23Attributes(declAttrs) || MaybeParseGNUAttributes(declSpecAttrs))
        {
        }

        decl = ParseExternalDeclaration(declAttrs, declSpecAttrs);
        return true;
    }
}
