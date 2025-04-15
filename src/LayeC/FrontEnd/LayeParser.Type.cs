using System.Diagnostics;

using LayeC.FrontEnd.SyntaxTree;
using LayeC.FrontEnd.SyntaxTree.Err;
using LayeC.FrontEnd.SyntaxTree.Types;

namespace LayeC.FrontEnd;

public sealed partial class LayeParser
{
    public SyntaxNode ParseType()
    {
        if (IsAtEnd)
        {
            Context.ErrorExpectedType(Source, CurrentLocation);
            var missingToken = CreateMissingToken(CurrentLocation);
            return new SyntaxErrMissingType(missingToken);
        }

        SyntaxNode typeNode;
        switch (CurrentToken.Kind)
        {
            case TokenKind.KWVoid: typeNode = new SyntaxTypeBuiltIn(BuiltInTypeKind.Void, Consume()); break;

            default:
            {
                Context.ErrorExpectedType(CurrentToken.Source, CurrentLocation);
                var missingToken = CreateMissingToken(CurrentLocation);
                return new SyntaxErrMissingType(missingToken);
            }
        }

        return MaybeParseTypeContinuation(typeNode);
    }

    public SyntaxNode MaybeParseTypeContinuation(SyntaxNode innerType)
    {
        return innerType;
        //Context.Todo(nameof(MaybeParseTypeContinuation));
        //throw new UnreachableException();
    }
}
