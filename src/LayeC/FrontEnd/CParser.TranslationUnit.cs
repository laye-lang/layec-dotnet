using LayeC.FrontEnd.Semantics.Decls;

namespace LayeC.FrontEnd;

public sealed partial class CParser
{
    public SemaDeclTranslationUnit ParseTranslationUnit()
    {
        var topLevelDecls = new List<SemaDecl>();

        while (ParseTopLevel(out var decl))
            topLevelDecls.Add(decl);

        return new SemaDeclTranslationUnit(Source, topLevelDecls);
    }
}
