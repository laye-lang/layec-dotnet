using System.Diagnostics;

using LayeC.FrontEnd.Semantics.Decls;

namespace LayeC.FrontEnd;

public sealed partial class CParser
{
    public SemaDeclTranslationUnit ParseTranslationUnit()
    {
        Context.Todo(nameof(ParseTranslationUnit));
        throw new UnreachableException();
    }
}
