using System.Diagnostics;

using Choir.FrontEnd.Syntax;

namespace Choir.FrontEnd;

public sealed partial class LayeParser
{
    public SyntaxNode ParseBindingOrFunctionDeclStartingAtName(SyntaxNode declType)
    {
        Context.Todo(nameof(ParseBindingOrFunctionDeclStartingAtName));
        throw new UnreachableException();
    }
}
