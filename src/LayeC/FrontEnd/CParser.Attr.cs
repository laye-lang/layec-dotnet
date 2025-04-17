using LayeC.FrontEnd.Syntax.Meta;

namespace LayeC.FrontEnd;

public sealed partial class CParser
{
    public bool MaybeParseC23Attributes(SyntaxCAttributesBuilder attrs)
    {
        return false;
    }

    public bool MaybeParseGNUAttributes(SyntaxCAttributesBuilder attrs)
    {
        return false;
    }
}
