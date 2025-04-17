using LayeC.Source;

namespace LayeC.FrontEnd.Semantics.Decls;

public sealed class SemaDeclTranslationUnit(SourceText source, IEnumerable<SemaDecl> topLevelDecls)
    : SemaDecl(source, SourceRange.Zero)
{
    public IReadOnlyList<SemaDecl> TopLevelDecls { get; } = [.. topLevelDecls];

    protected override string DebugNodeName { get; } = nameof(SemaDeclTranslationUnit);
    protected override IEnumerable<ITreeDebugNode> Children
    {
        get
        {
            foreach (var topLevelDecl in TopLevelDecls)
                yield return topLevelDecl;
        }
    }
}
