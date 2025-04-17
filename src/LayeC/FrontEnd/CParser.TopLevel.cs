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
        SyntaxCAttributesBuilder declAttrs = new();
        SyntaxCAttributesBuilder declSpecAttrs = new();

        while (MaybeParseC23Attributes(declAttrs) || MaybeParseGNUAttributes(declSpecAttrs))
        {
        }

        declGroup = ParseExternalDeclaration(declAttrs, declSpecAttrs);
        return true;
    }
}
