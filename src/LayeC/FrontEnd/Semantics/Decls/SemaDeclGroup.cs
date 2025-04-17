using LayeC.Source;

namespace LayeC.FrontEnd.SemaTree.Decls;

public sealed class SemaDeclGroup(SourceText source, SourceRange range, IEnumerable<SemaDecl> decls)
    : SemaDecl(source, range)
{
    public IReadOnlyList<SemaDecl> Declarations { get; } = [.. decls];

    protected override string DebugNodeName { get; } = nameof(SemaDeclGroup);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [.. decls];

    public SemaDeclGroup(SemaDecl decl)
        : this(decl.Source, decl.Range, [decl])
    {
    }
}
