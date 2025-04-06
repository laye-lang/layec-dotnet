namespace LayeC.FrontEnd;

public sealed class SyntaxDebugTreePrinter(bool useColor)
    : BaseTreeDebugPrinter(useColor)
{
    public void PrintToken(Token token)
    {
        Print(token);
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
        Console.Write(token.Source.Substring(token.Range));
    }
}
