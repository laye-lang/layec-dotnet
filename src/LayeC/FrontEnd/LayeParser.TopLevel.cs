using LayeC.FrontEnd.SyntaxTree;
using LayeC.FrontEnd.SyntaxTree.Decls;

namespace LayeC.FrontEnd;

public sealed partial class LayeParser
{
    public SyntaxModuleUnit ParseModuleUnit()
    {
        var header = ParseModuleUnitHeader();
        return new SyntaxModuleUnit(Source, header, []);
    }

    public SyntaxNode ParseTopLevel()
    {
        Context.Assert(!IsAtEnd, $"Cannot call {nameof(ParseTopLevel)} when the parser has reached the end of the token stream.");

        switch (CurrentToken.Kind)
        {
            case TokenKind.EndOfFile: return new SyntaxEndOfFile(Consume());

            default:
            {
                var unknownToken = Consume();
                Context.ErrorExpectedToken(unknownToken.Source, unknownToken.Location, "a top-level declaration");
                return new SyntaxUnknownTopLevel([], unknownToken);
            }
        }
    }
}
