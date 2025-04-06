using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using LayeC.FrontEnd.Semantics;
using LayeC.FrontEnd.Syntax;

namespace LayeC.FrontEnd;

public sealed partial class Parser
{
    private SyntaxNode ParseCTopLevel()
    {
        AssertC();

        var declSpecAttributesBuilder = new SemaCAttributesBuilder(Source);
        while (MaybeParseGNUAttributes(declSpecAttributesBuilder))
        {
            // xyzzy
        }

        return ParseCExternalDeclaration(declSpecAttributesBuilder);
    }

    #region Attributes

    private bool MaybeParseGNUAttributes(SemaCAttributesBuilder attributesBuilder)
    {
        AssertC();
        return false;
    }

    #endregion

    #region Declarations

    private enum DeclaratorContext
    {
        File,
    }

    private SyntaxNode ParseCExternalDeclaration(SemaCAttributesBuilder declSpecAttributesBuilder)
    {
        AssertC();
        switch (CurrentToken.Kind)
        {
            case TokenKind.Typedef:
            case TokenKind.StaticAssert:
            case TokenKind._StaticAssert:
                return ParseCDeclaration(DeclaratorContext.File, declSpecAttributesBuilder);

            default:
                return ParseCDeclarationOrFunctionDefinition(DeclaratorContext.File, declSpecAttributesBuilder);
        }
    }

    private SyntaxNode ParseCDeclaration(DeclaratorContext declContext, SemaCAttributesBuilder declSpecAttributesBuilder)
    {
        AssertC();

        // TODO(local): static asserts
        // TODO(local): empty attributes? or something?

        CStorageClass storageClass;
        //var baseType = ParseCDeclarationSpecifiers();

        Context.Todo($"{nameof(Parser)}::{nameof(ParseCDeclaration)}");
        throw new UnreachableException();
    }

    private SyntaxNode ParseCDeclarationOrFunctionDefinition(DeclaratorContext declContext, SemaCAttributesBuilder declSpecAttributesBuilder)
    {
        AssertC();
        Context.Todo($"{nameof(Parser)}::{nameof(ParseCDeclarationOrFunctionDefinition)}");
        throw new UnreachableException();
    }

    private bool TryConsumeFunctionSpecifier([NotNullWhen(true)] out Token? funcSpecToken)
    {
        var funcSpec = CFunctionSpecifier.None;
        if (TryConsumeFunctionSpecifier(ref funcSpec, out funcSpecToken))
        {
            Context.ErrorFunctionSpecifierNotAllowed(Source, funcSpecToken.Location, funcSpecToken.Spelling);
            return true;
        }

        return false;
    }

    private bool TryConsumeFunctionSpecifier(ref CFunctionSpecifier funcSpec, [NotNullWhen(true)] out Token? funcSpecToken)
    {
        AssertC();

        funcSpecToken = CurrentToken;
        switch (funcSpecToken.Kind)
        {
            default: funcSpecToken = null; return false;
            case TokenKind.Inline: funcSpec |= CFunctionSpecifier.Inline; break;
            case TokenKind._NoReturn: funcSpec |= CFunctionSpecifier.NoReturn; break;
        }

        Consume();
        return true;
    }

    private SyntaxNode ParseCDeclarator()
    {
        AssertC();
        var intToken = Expect(TokenKind.Int, "'int'");
        var nameToken = ExpectIdentifier();
        var openParenToken = Expect(TokenKind.OpenParen, "'('");
        var closeParenToken = Expect(TokenKind.CloseParen, "')'");
        var semiColonToken = Expect(TokenKind.SemiColon, "';'");
        return new SyntaxDeclFunction(new SyntaxTypeBuiltIn(intToken), nameToken, openParenToken, closeParenToken, semiColonToken);
    }

    #endregion

    private SyntaxNode ParseCExpression()
    {
        AssertC();

        Context.Todo($"{nameof(Parser)}::{nameof(ParseCExpression)}");
        throw new UnreachableException();
    }
}
