using System.Diagnostics;

using LayeC.FrontEnd.Syntax;

namespace LayeC.FrontEnd;

public sealed partial class LayeParser
{
    public SyntaxNode ParseBindingOrFunctionDeclStartingAtName(SyntaxNode declType)
    {
        Context.Todo(nameof(ParseBindingOrFunctionDeclStartingAtName));
        throw new UnreachableException();
    }
}
