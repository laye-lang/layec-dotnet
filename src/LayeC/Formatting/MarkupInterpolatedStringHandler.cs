using System.Runtime.CompilerServices;

namespace LayeC.Formatting;

[InterpolatedStringHandler]
public readonly struct MarkupInterpolatedStringHandler
{
    private readonly MarkupBuilder _builder;

    public Markup Markup => _builder.Markup;

    public MarkupInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        _builder = new();
    }

    public void AppendLiteral(string s)
    {
        _builder.Append(s);
    }

    public void AppendFormatted<T>(T t)
    {
        switch (t)
        {
            default: AppendLiteral(t?.ToString() ?? ""); break;
            case string literal: AppendLiteral(literal); break;
            case Markup markup: _builder.Append(markup); break;
            case IMarkupFormattable formattable: formattable.BuildMarkup(_builder); break;
        }
    }
}
