using Choir.FrontEnd.Semantics.Decls;
using Choir.FrontEnd.Syntax;

namespace Choir.FrontEnd;

public sealed class SyntaxDebugTreePrinter(bool useColor)
    : BaseTreeDebugPrinter(useColor)
{
    public void PrintToken(Token token)
    {
        Print(token);
        Console.ResetColor();
    }

    public void PrintModuleUnit(SyntaxModuleUnit unit)
    {
        Print(unit);
        Console.ResetColor();
    }

    public void PrintTranslationUnit(SemaDeclTranslationUnit unit)
    {
        Print(unit);
        Console.ResetColor();
    }

    protected override void Print(ITreeDebugNode node)
    {
        SetColor(ColorBase);
        Console.Write(node.DebugNodeName);

        SetColor(ColorLocation);
        Console.Write(" <");
        Console.Write(node.Location.Offset);
        Console.Write('>');

        switch (node)
        {
            case Token token: PrintTokenInfo(token); break;
            case TriviaList trivia: PrintTriviaList(trivia); break;
            case Trivium trivium: PrintTrivium(trivium); break;
        }

        Console.WriteLine();
        PrintChildren(node.Children);
    }

    private void PrintTokenInfo(Token token)
    {
        SetColor(ColorMisc);
        Console.Write(' ');
        Console.Write(token.Language);
        SetColor(ColorProperty);
        Console.Write(' ');
        Console.Write(token.Kind);

        if (token.IsAtStartOfLine)
        {
            SetColor(ColorProperty);
            Console.Write(" BOL");
        }

        if (token.IsAtEndOfLine)
        {
            SetColor(ColorProperty);
            Console.Write(" EOL");
        }

        SetColor(ColorMisc);
        Console.Write(' ');
        Console.Write(token.Spelling);
    }

    private void PrintTriviaList(TriviaList trivia)
    {
        SetColor(ColorProperty);
        Console.Write(trivia.IsLeading ? " Leading" : " Trailing");
    }

    private void PrintTrivium(Trivium trivium)
    {
        SetColor(ColorMisc);
        Console.Write(' ');
        switch (trivium)
        {
            case TriviumShebangComment: Console.Write(trivium.Source.Substring(trivium.Range)); break;
            case TriviumLineComment: Console.Write(trivium.Source.Substring(trivium.Range)); break;
            //case TriviumDelimitedComment: if (_includeComments) _writer.Write(source.Substring(trivium.Range)); break;
        }
    }
}
