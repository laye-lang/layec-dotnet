using System.Diagnostics;

using LayeC.FrontEnd.Syntax;

namespace LayeC.FrontEnd;

public sealed partial class Parser
{
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

    private SyntaxNode ParseCExpression()
    {
        AssertC();

        Context.Todo($"{nameof(Parser)}::{nameof(ParseCExpression)}");
        throw new UnreachableException();
    }
}
