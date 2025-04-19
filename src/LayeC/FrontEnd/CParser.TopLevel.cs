using LayeC.FrontEnd.Semantics.Decls;
using LayeC.FrontEnd.Syntax.Meta;

namespace LayeC.FrontEnd;

public sealed partial class CParser
{
    /// <summary>
    /// Returns false at EOF.
    /// </summary>
    public bool ParseTopLevel(out SemaDeclGroup declGroup)
    {
        List<SyntaxCAttribute> declAttrs = [];
        List<SyntaxCAttribute> declSpecAttrs = [];

        while (MaybeParseC23Attributes(declAttrs) || MaybeParseGNUAttributes(declSpecAttrs))
        {
        }

        declGroup = ParseExternalDeclaration(declAttrs, declSpecAttrs);
        return true;
    }
}
