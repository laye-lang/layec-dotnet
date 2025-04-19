using LayeC.FrontEnd.Syntax.Meta;

namespace LayeC.FrontEnd;

public sealed partial class CParser
{
    public bool MaybeParseC23Attributes(List<SyntaxCAttribute> attrs)
    {
        return false;
    }

    public bool MaybeParseGNUAttributes(List<SyntaxCAttribute> attrs)
    {
        return false;
    }
}
