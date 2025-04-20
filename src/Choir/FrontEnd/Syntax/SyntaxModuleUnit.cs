using Choir.FrontEnd.Syntax.Decls;
using Choir.Source;

namespace Choir.FrontEnd.Syntax;

public sealed class SyntaxModuleUnit(SourceText source, SyntaxModuleUnitHeader header, IEnumerable<SyntaxNode> topLevelNodes)
    : SyntaxNode(source, SourceRange.Zero)
{
    public SyntaxModuleUnitHeader ModuleUnitHeader { get; } = header;
    public IReadOnlyList<SyntaxNode> TopLevelNodes { get; } = [.. topLevelNodes];

    protected override string DebugNodeName { get; } = nameof(SyntaxModuleUnit);
    protected override IEnumerable<ITreeDebugNode> Children
    {
        get
        {
            yield return ModuleUnitHeader;
            foreach (var node in TopLevelNodes)
                yield return node;
        }
    }
}

public sealed class SyntaxModuleUnitHeader(SyntaxDeclModule moduleDecl, IEnumerable<SyntaxDeclImport> importDecls)
    : SyntaxNode(moduleDecl.Source, moduleDecl.Range)
{
    public SyntaxDeclModule ModuleDeclaration { get; } = moduleDecl;
    public IReadOnlyList<SyntaxDeclImport> ImportDeclarations { get; } = [.. importDecls];

    protected override string DebugNodeName { get; } = nameof(SyntaxModuleUnitHeader);
    protected override IEnumerable<ITreeDebugNode> Children
    {
        get
        {
            yield return ModuleDeclaration;
            foreach (var importDecl in ImportDeclarations)
                yield return importDecl;
        }
    }
}
