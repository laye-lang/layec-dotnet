using LayeC.FrontEnd.Syntax;

namespace LayeC.FrontEnd;

public sealed class SyntaxDebugTreeVisualizer(bool useColor)
    : BaseTreeDebugVisualizer(useColor)
{
    public void PrintUnit(SyntaxModuleUnit unitSyntax)
    {
        Print(unitSyntax);
        Console.ResetColor();
    }

    protected override void Print(ITreeDebugNode node)
    {
        SetColor(ColorBase);
        Console.Write(node.DebugNodeName);

        switch (node)
        {
            case Token token: PrintTokenInfo(token); break;
        }

        Console.WriteLine();
        PrintChildren(node.Children);
    }

    private void PrintTokenInfo(Token token)
    {
        SetColor(ColorProperty);
        Console.Write(' ');
        Console.Write(token.Kind);

        if (token.IsAtStartOfLine)
        {
            SetColor(ColorProperty);
            Console.Write(" BOL");
        }

        SetColor(ColorMisc);
        Console.Write(' ');
        Console.Write(token.Source.Substring(token.Range));
    }
}
