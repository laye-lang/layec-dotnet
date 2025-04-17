using LayeC.FrontEnd.Syntax;
using LayeC.FrontEnd.Syntax.Decls;
using LayeC.FrontEnd.Syntax.Err;
using LayeC.FrontEnd.Syntax.Exprs;

namespace LayeC.FrontEnd;

public enum PragmaCKind
{
    Declaration,
    Expression,
}

public sealed partial class LayeParser
{
    public SyntaxNode ParsePragmaC(PragmaCKind kind)
    {
        Context.Assert(At(TokenKind.KWPragma) && PeekAt(1, TokenKind.LiteralString) && PeekToken(1).StringValue == "C", $"Cannot call {nameof(ParsePragmaC)} when not at 'pragma \"C\"'.");

        var pragmaKeywordToken = Consume();
        var cStringToken = Consume();

        if (!At(TokenKind.OpenParen, TokenKind.OpenCurly))
            return new SyntaxErrLonePragmaC(pragmaKeywordToken, cStringToken);

        var openDelimiterToken = Consume();
        // TODO(local): make actual diagnostics for 'pragma "C"' expression versus declaration contexts, so we can explain the difference to the user.
        if (kind == PragmaCKind.Expression && openDelimiterToken.Kind != TokenKind.OpenParen)
            Context.ErrorExpectedToken(openDelimiterToken.Source, openDelimiterToken.Location, "'('");
        else if (kind == PragmaCKind.Declaration && openDelimiterToken.Kind != TokenKind.OpenCurly)
            Context.ErrorExpectedToken(openDelimiterToken.Source, openDelimiterToken.Location, "'{'");

        var cTokens = new List<Token>();
        while (!IsAtEnd && CurrentToken.Language == SourceLanguage.C)
        {
            var cToken = Consume();
            cTokens.Add(cToken);
        }

        Token closeDelimiterToken;
        if (openDelimiterToken.Kind == TokenKind.OpenParen)
            closeDelimiterToken = Expect("')'", TokenKind.CloseParen);
        else closeDelimiterToken = Expect("'}'", TokenKind.CloseCurly);

        Context.Assert(closeDelimiterToken.Language != SourceLanguage.C, "Should have consumed all the consecutive C tokens in 'pragma \"C\"'.");
        if (openDelimiterToken.Kind == TokenKind.OpenParen)
            return new SyntaxExprDelayedPragmaC(pragmaKeywordToken, cStringToken, openDelimiterToken, cTokens, closeDelimiterToken);
        else return new SyntaxDeclDelayedPragmaC(pragmaKeywordToken, cStringToken, openDelimiterToken, cTokens, closeDelimiterToken);
    }
}
