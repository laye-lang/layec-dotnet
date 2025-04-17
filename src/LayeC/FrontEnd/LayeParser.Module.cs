using LayeC.FrontEnd.Syntax;
using LayeC.FrontEnd.Syntax.Decls;

namespace LayeC.FrontEnd;

public sealed partial class LayeParser
{
    public SyntaxModuleUnitHeader ParseModuleUnitHeader()
    {
        var moduleDecl = TryParseModuleDeclaration()
            ?? new SyntaxDeclImplicitProgramModule(Source);

        var importDecls = new List<SyntaxDeclImport>();
        while (TryParseImportDeclaration() is { } importDecl)
            importDecls.Add(importDecl);

        return new SyntaxModuleUnitHeader(moduleDecl, importDecls);
    }

    public SyntaxDeclModule? TryParseModuleDeclaration()
    {
        if (!At(TokenKind.KWModule)) return null;
        return ParseNamedModuleDeclaration();
    }

    private SyntaxDeclNamedModule ParseNamedModuleDeclaration()
    {
        Context.Assert(At(TokenKind.KWModule), $"Can only call {nameof(ParseNamedModuleDeclaration)} when at a 'module' keyword token.");

        var moduleKeywordToken = Consume();
        var moduleName = ParseModuleName();
        var semiColonToken = Expect("';'", TokenKind.SemiColon);

        return new SyntaxDeclNamedModule(moduleKeywordToken, moduleName, semiColonToken);
    }

    public SyntaxModuleName ParseModuleName()
    {
        var firstToken = Expect("an identifier", TokenKind.Identifier);
        if (!At(TokenKind.ColonColon))
        {
            if (firstToken.Kind == TokenKind.Identifier && !SyntaxFacts.IsAsciiIdentifier(firstToken.StringValue))
                Context.ErrorIdentifierIsInvalidInModuleName(firstToken);
            return SyntaxModuleName.Create(firstToken);
        }

        var nameTokens = new List<Token>() { firstToken };
        var delimiterTokens = new List<Token>() { };

        while (TryConsume(TokenKind.ColonColon) is { } delimiterToken)
        {
            delimiterTokens.Add(delimiterToken);

            var nextNameToken = Expect("an identifier", TokenKind.Identifier);
            nameTokens.Add(nextNameToken);

            if (nextNameToken.Kind == TokenKind.Identifier && !SyntaxFacts.IsAsciiIdentifier(nextNameToken.StringValue))
                Context.ErrorIdentifierIsInvalidInModuleName(nextNameToken);
        }

        return SyntaxModuleName.Create(nameTokens, delimiterTokens);
    }

    public SyntaxDeclImport? TryParseImportDeclaration()
    {
        if (!At(TokenKind.KWImport)) return null;
        return ParseImportDeclaration();
    }

    private SyntaxDeclImport ParseImportDeclaration()
    {
        Context.Assert(At(TokenKind.KWImport), $"Can only call {nameof(ParseImportDeclaration)} when at an 'import' keyword token.");

        var importKeywordToken = Consume();
        var moduleName = ParseModuleName();
        var semiColonToken = Expect("';'", TokenKind.SemiColon);

        return new SyntaxDeclImport(importKeywordToken, moduleName, semiColonToken);
    }
}
