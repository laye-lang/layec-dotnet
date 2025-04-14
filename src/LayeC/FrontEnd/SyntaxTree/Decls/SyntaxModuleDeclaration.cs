using System.Diagnostics;

using LayeC.Source;

namespace LayeC.FrontEnd.SyntaxTree.Decls;

public abstract class SyntaxModuleDeclaration(SourceText source, SourceRange range, StringView moduleName)
    : SyntaxNode(source, range)
{
    public StringView ModuleName { get; } = moduleName;
}

public sealed class SyntaxImplicitProgramModuleDeclaration(SourceText source)
    : SyntaxModuleDeclaration(source, SourceRange.Zero, LayeModule.ProgramModuleName)
{
    protected override string DebugNodeName { get; } = nameof(SyntaxImplicitProgramModuleDeclaration);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = Array.Empty<ITreeDebugNode>();
}

public sealed class SyntaxNamedModuleDeclaration(Token moduleKeywordToken, SyntaxModuleName moduleNameSyntax, Token semiColonToken)
    : SyntaxModuleDeclaration(moduleKeywordToken.Source, moduleKeywordToken.Range, moduleNameSyntax.ModuleName)
{
    public Token ModuleKeywordToken { get; } = moduleKeywordToken;
    public SyntaxModuleName ModuleNameSyntax { get; } = moduleNameSyntax;
    public Token SemiColonToken { get; } = semiColonToken;

    protected override string DebugNodeName { get; } = nameof(SyntaxModuleDeclaration);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [moduleKeywordToken, moduleNameSyntax, semiColonToken];
}

public sealed class SyntaxModuleName
    : SyntaxNode
{
    public static SyntaxModuleName Create(Token nameToken)
    {
        return new(nameToken.Source, nameToken.Range, [nameToken], [], nameToken.StringValue);
    }

    public static SyntaxModuleName Create(IEnumerable<Token> nameTokens, IEnumerable<Token> delimiterTokens)
    {
        IReadOnlyList<Token> nt = [.. nameTokens];
        Debug.Assert(nt.Count > 0);
        return new(
            nt[0].Source,
            new SourceRange(nt[0].Range.Begin, nt[^1].Range.End),
            nt,
            [.. delimiterTokens],
            string.Join(',', nameTokens.Select(t => t.StringValue))
        );
    }

    public IReadOnlyList<Token> NameTokens { get; }
    public IReadOnlyList<Token> DelimiterTokens { get; }
    public StringView ModuleName { get; }

    protected override string DebugNodeName { get; } = nameof(SyntaxModuleName);
    protected override IEnumerable<ITreeDebugNode> Children
    {
        get
        {
            yield return NameTokens[0];
            for (int i = 0; i < DelimiterTokens.Count; i++)
            {
                yield return DelimiterTokens[i];
                yield return NameTokens[i + 1];
            }
        }
    }

    private SyntaxModuleName(SourceText source, SourceRange range, IReadOnlyList<Token> nameTokens, IReadOnlyList<Token> delimiterTokens, StringView moduleName)
        : base(source, range)
    {
        Debug.Assert(nameTokens.Count > 0);
        Debug.Assert(nameTokens.Count == 1 + delimiterTokens.Count);

        NameTokens = nameTokens;
        DelimiterTokens = delimiterTokens;
        ModuleName = moduleName;
    }
}
