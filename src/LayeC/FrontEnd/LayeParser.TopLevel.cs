using LayeC.FrontEnd.SyntaxTree;
using LayeC.FrontEnd.SyntaxTree.Decls;

namespace LayeC.FrontEnd;

public sealed partial class LayeParser
{
    public SyntaxModuleUnit ParseModuleUnit()
    {
        var header = ParseModuleUnitHeader();

        var topLevelNodes = new List<SyntaxNode>();
        while (!IsAtEnd)
        {
            var topLevelNode = ParseTopLevel();
            topLevelNodes.Add(topLevelNode);
        }

        return new SyntaxModuleUnit(Source, header, topLevelNodes);
    }

    public SyntaxNode ParseTopLevel()
    {
        Context.Assert(!IsAtEnd, $"Cannot call {nameof(ParseTopLevel)} when the parser has reached the end of the token stream.");

        // TODO(local): parse template parameters
        // TODO(local): parse decl attributes
        // TODO(local): if not static-if, then conditional attributes

        switch (CurrentToken.Kind)
        {
            case TokenKind.EndOfFile: return new SyntaxEndOfFile(Consume());

            case TokenKind.KWPragma when PeekAt(1, TokenKind.LiteralString) && PeekToken(1).StringValue == "C":
                return ParsePragmaC(PragmaCKind.Declaration);

            default:
            {
                var unknownToken = Consume();
                Context.ErrorExpectedToken(unknownToken.Source, unknownToken.Location, "a top-level declaration");
                return new SyntaxUnknownTopLevel([], unknownToken);
            }
        }
    }
}
