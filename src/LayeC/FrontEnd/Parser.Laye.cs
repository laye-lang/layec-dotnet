using System.Diagnostics;

using LayeC.FrontEnd.Syntax;

namespace LayeC.FrontEnd;

public sealed partial class Parser
{
    #region Top-level

    private SyntaxNode ParseLayeTopLevelPragma(Token pragmaToken, Token pragmaKindToken)
    {
        AssertLaye();
        AssertPeekBufferEmpty();

        if (pragmaKindToken.Kind == TokenKind.LiteralString && pragmaKindToken.StringValue == "C")
        {
            if (TryConsume(TokenKind.OpenCurly, out var openCurlyToken))
            {
                AssertPeekBufferEmpty();

                List<SyntaxNode> nestedCNodes = [];
                using (var _ = PushLexerMode(SourceLanguage.C))
                {
                    // just grab one for now
                    var nestedCNode = ParseCTopLevel();
                    nestedCNodes.Add(nestedCNode);
                }

                var closeCurlyToken = Expect(TokenKind.CloseCurly, "'}'");

                AssertLaye();
                return new SyntaxTopLevelPragmaC(pragmaToken, pragmaKindToken, openCurlyToken, nestedCNodes, closeCurlyToken);
            }
        }

        Context.Todo($"{nameof(Parser)}::{nameof(ParseLayeTopLevelPragma)}");
        throw new UnreachableException();
    }

    #endregion

    #region Declarations

    private SyntaxNode ParseLayeBindingOrFunctionDeclStartingAtName(SyntaxNode declType)
    {
        AssertLaye();
        return ParseLayeFunctionDeclStartingAtName(declType);
    }

    private SyntaxNode ParseLayeFunctionDeclStartingAtName(SyntaxNode declType)
    {
        AssertLaye();
        var nameToken = ExpectIdentifier();
        var openParenToken = Expect(TokenKind.OpenParen, "'('");
        var closeParenToken = Expect(TokenKind.CloseParen, "')'");
        var semiColonToken = Expect(TokenKind.SemiColon, "';'");
        return new SyntaxDeclFunction(declType, nameToken, openParenToken, closeParenToken, semiColonToken);
    }

    #endregion

    #region Types

    private SyntaxNode ParseLayeQualifiedType()
    {
        AssertLaye();
        var intToken = Expect(TokenKind.Int, "'int'");
        var builtInType = new SyntaxTypeBuiltIn(intToken);
        //var qualType = new SyntaxQualMut(builtInType)
        return builtInType;
    }

    #endregion

    #region Expressions

    private SyntaxNode ParseLayeExpression()
    {
        AssertLaye();
        switch (CurrentToken.Kind)
        {
            case TokenKind.Pragma:
            {
                var pragmaToken = Consume();
                var pragmaKindToken = Consume();
                return ParseLayePragmaExpression(pragmaToken, pragmaKindToken);
            }

            default:
            {
                Context.Todo($"Unhandled token in {nameof(Parser)}::{nameof(ParseLayeExpression)}().");
                throw new UnreachableException();
            }
        }
    }

    private SyntaxNode ParseLayePragmaExpression(Token pragmaToken, Token pragmaKindToken)
    {
        AssertLaye();
        AssertPeekBufferEmpty();

        if (pragmaKindToken.Kind == TokenKind.LiteralString && pragmaKindToken.StringValue == "C")
        {
            if (TryConsume(TokenKind.OpenParen, out var openParenToken))
            {
                AssertPeekBufferEmpty();

                SyntaxNode nestedCExpr;
                using (var _ = PushLexerMode(SourceLanguage.C))
                    nestedCExpr = ParseCExpression();

                var closeParenToken = Expect(TokenKind.CloseParen, "')'");

                AssertLaye();
                return new SyntaxExprPragmaC(pragmaToken, pragmaKindToken, openParenToken, nestedCExpr, closeParenToken);
            }
        }

        Context.Todo($"{nameof(Parser)}::{nameof(ParseLayePragmaExpression)}");
        throw new UnreachableException();
    }

    #endregion
}
