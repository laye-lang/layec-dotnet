using System.Diagnostics;
using System.Drawing;
using System.Text;

using LayeC.Formatting;

namespace LayeC.Diagnostics;

public sealed class FormattedDiagnosticWriter(TextWriter writer, bool useColor)
    : IDiagnosticConsumer
{
    private const string Reset = "\x1b[0m";
    private const string DefaultColor = "\x1b[39m";
    private const string Normal = "\x1b[22m";
    private const string Bold = "\x1b[1m";
    private const string Italic = "\x1b[3m";
    private const string Underline = "\x1b[4m";
    private const string Red = "\x1b[91m";
    private const string Green = "\x1b[92m";
    private const string Yellow = "\x1b[93m";
    private const string Blue = "\x1b[94m";
    private const string Magenta = "\x1b[95m";
    private const string Cyan = "\x1b[96m";
    private const string Grey = "\x1b[97m";
    private const string White = "\x1b[1m\x1b[97m";

    public const int MinRenderWidth = 8;

    private static string GetColorEscapeForMarkupColor(MarkupColor color)
    {
        switch (color)
        {
            default: return White;
            case MarkupColor.Black: return Grey;
            case MarkupColor.Red: return Red;
            case MarkupColor.Green: return Green;
            case MarkupColor.Yellow: return Yellow;
            case MarkupColor.Blue: return Blue;
            case MarkupColor.Magenta: return Magenta;
            case MarkupColor.Cyan: return Cyan;
            case MarkupColor.White: return White;
        }
    }

    private static string GetStyleEscapeForMarkupStyle(MarkupStyle style)
    {
        string result = "";
        for (int i = 0; i < 4; i++)
        {
            var flag = (MarkupStyle)(1 << i);
            if (style.HasFlag(flag))
                result += Impl(flag);
        }

        return result;

        static string Impl(MarkupStyle style)
        {
            switch (style)
            {
                default: return Normal;
                case MarkupStyle.Bold: return Bold;
                case MarkupStyle.Italic: return Italic;
                case MarkupStyle.Underline: return Underline;
                case MarkupStyle.Monospace: return Normal;
            }
        }
    }

    private static string GetColorEscapeForMarkupSemantic(MarkupSemantic semantic)
    {
        switch (semantic)
        {
            default: return White;
            case MarkupSemantic.Entity: return White;
            case MarkupSemantic.EntityParameter: return White;
            case MarkupSemantic.EntityLocal: return White;
            case MarkupSemantic.EntityGlobal: return White;
            case MarkupSemantic.EntityMember: return White;
            case MarkupSemantic.EntityFunction: return White;
            case MarkupSemantic.EntityType: return White;
            case MarkupSemantic.EntityTypeValue: return White;
            case MarkupSemantic.EntityTypeStruct: return White;
            case MarkupSemantic.EntityTypeEnum: return White;
            case MarkupSemantic.EntityNamespace: return White;
            case MarkupSemantic.Keyword: return Blue;
            case MarkupSemantic.KeywordControlFlow: return Blue;
            case MarkupSemantic.KeywordOperator: return Blue;
            case MarkupSemantic.KeywordType: return Blue;
            case MarkupSemantic.KeywordQualifier: return Blue;
            case MarkupSemantic.Literal: return Yellow;
            case MarkupSemantic.LiteralNumber: return Yellow;
            case MarkupSemantic.LiteralString: return Yellow;
            case MarkupSemantic.LiteralInvalid: return Red;
            case MarkupSemantic.LiteralKeyword: return Blue;
            case MarkupSemantic.Punctuation: return Grey;
            case MarkupSemantic.PunctuationDelimiter: return Grey;
            case MarkupSemantic.PunctuationOperator: return Grey;
        }
    }

    public TextWriter Writer { get; } = writer;
    public bool UseColor { get; } = useColor;

    private readonly List<Diagnostic> _diagnosticGroup = new(10);

    private bool _hasPrinted = false;

    public void Consume(Diagnostic diag)
    {
        if (diag.Level != DiagnosticLevel.Note)
            Flush();

        _diagnosticGroup.Add(diag);
    }

    public void Dispose()
    {
        Flush();
    }

    public void Flush()
    {
        if (_diagnosticGroup.Count == 0)
            return;

        if (_hasPrinted)
            Writer.WriteLine();
        else _hasPrinted = true;

        bool isConsole = (Writer == Console.Out && !Console.IsOutputRedirected) || Writer == Console.Error;
        string groupText = RenderDiagnosticGroup([.. _diagnosticGroup], Math.Max(MinRenderWidth, isConsole ? Console.WindowWidth : 80));
        _diagnosticGroup.Clear();

        Writer.Write(groupText);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private void ResetColor(StringBuilder builder)
    {
        if (!UseColor) return;
        builder.Append(Reset);
    }

    private void WriteColor(StringBuilder builder, MarkupColor color)
    {
        if (!UseColor) return;
        builder.Append(GetColorEscapeForMarkupColor(color));
    }
#pragma warning restore IDE0060 // Remove unused parameter

    private string RenderDiagnosticGroup(Diagnostic[] group, int displayWidth)
    {
        Debug.Assert(group.Length > 0, "Attempt to render an empty diagnostic group.");
        Debug.Assert(displayWidth >= MinRenderWidth, "Attempt to render a diagnostic group with a specified width less than the configured minimum.");

        var groupBuilder = new StringBuilder();

        // render the contents of the diagnostics to a subset of the width, since there will be formatting characters at the left.
        int clientWidth = displayWidth - 3;

        for (int i = 0; i < group.Length; i++)
        {
            if (i > 0 && group[i - 1].Source is not null)
            {
                if (i == 1)
                    groupBuilder.AppendLine("│");
                else groupBuilder.AppendLine("┆");
            }

            // SourceLocation? previousLocation = i > 0 && group[i - 1].Source is not null ? group[i - 1].Location : null;
            string renderedText = FormatDiagnostic(group[i], clientWidth).TrimEnd('\r', '\n');

            bool printTrailingBoxClose = true;
            string[] renderedLines = renderedText.Split(Environment.NewLine);
            for (int j = 0; j < renderedLines.Length; j++)
            {
                ResetColor(groupBuilder);

                if (j == renderedLines.Length - 1 && i == group.Length - 1)
                {
                    if (group.Length == 1 && renderedLines.Length == 1)
                        groupBuilder.Append("── ");
                    else if (j != 0)
                    {
                        if (i == 0)
                            groupBuilder.Append("│  ");
                        else groupBuilder.Append("┆  ");
                    }
                    else
                    {
                        if (group[i].Source is not null)
                            groupBuilder.Append("├─ ");
                        else
                        {
                            printTrailingBoxClose = false;
                            groupBuilder.Append("╰─ ");
                        }
                    }
                }
                else if (j == 0)
                {
                    if (i == 0)
                        groupBuilder.Append("╭─ ");
                    else groupBuilder.Append("├─ ");
                }
                else
                {
                    if (i == 0)
                        groupBuilder.Append("│  ");
                    else groupBuilder.Append("┆  ");
                }

                groupBuilder.AppendLine(renderedLines[j]);
            }

            if (printTrailingBoxClose && i == group.Length - 1 && (group.Length > 1 || renderedLines.Length > 1))
            {
                ResetColor(groupBuilder);
                //groupBuilder.AppendLine("╯");
                groupBuilder.AppendLine("╰─ ");
            }
        }

        ResetColor(groupBuilder);
        return groupBuilder.ToString();
    }

    private string FormatDiagnostic(Diagnostic diag, int clientWidth)
    {
        var markupRenderer = new FormattedDiagnosticMessageMarkupRenderer(UseColor, clientWidth);
        var builder = new MarkupBuilder();

        if (diag.Source is not null)
        {
            builder.Append($"{diag.Source.Name}[{diag.Location.Offset}]:");
            builder.Append(MarkupLineBreak.Instance);
        }

        var color = diag.Level switch
        {
            DiagnosticLevel.Note => MarkupColor.Green,
            DiagnosticLevel.Remark => MarkupColor.Yellow,
            DiagnosticLevel.Warning => MarkupColor.Magenta,
            DiagnosticLevel.Error => MarkupColor.Red,
            DiagnosticLevel.Fatal => MarkupColor.Cyan,
            _ => MarkupColor.White,
        };

        builder.Append(new MarkupScopedColor(color, diag.Level.ToString()));
        builder.Append(": ");
        builder.Append(diag.Message);

        if (diag.Source is null)
            return markupRenderer.Render(builder.Markup);

        return markupRenderer.Render(builder.Markup);
    }

#pragma warning disable CS9113 // Parameter is unread.
    private sealed class FormattedDiagnosticMessageMarkupRenderer(bool useColor, int clientWidth)
#pragma warning restore CS9113 // Parameter is unread.
    {
        private readonly Stack<string> _colorEscapes = [];
        private readonly Stack<string> _styleEscapes = [];

        public string Render(Markup markup)
        {
            var builder = new StringBuilder();
            RenderImpl(builder, markup);
            return builder.ToString();
        }

        private void RenderImpl(StringBuilder builder, Markup markup)
        {
            switch (markup)
            {
                default: throw new InvalidOperationException($"Unhandled {nameof(Markup)} node in {nameof(MarkupStringRenderer)}: {markup.GetType().FullName}.");

                case MarkupLineBreak: builder.AppendLine(); break;
                case MarkupLiteral literal: builder.Append(literal.Literal); break;

                case MarkupScopedColor colored:
                {
                    string colorEscape = GetColorEscapeForMarkupColor(colored.Color);
                    _colorEscapes.Push(colorEscape);
                    if (useColor) builder.Append(colorEscape);

                    RenderImpl(builder, colored.Contents);
                    _colorEscapes.Pop();

                    if (useColor)
                    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                        if (_colorEscapes.TryPeek(out string previousColor))
                            builder.Append(previousColor);
                        else builder.Append(DefaultColor);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                    }
                } break;

                case MarkupScopedStyle styled:
                {
                    string styleEscape = GetStyleEscapeForMarkupStyle(styled.Style);
                    _styleEscapes.Push(styleEscape);
                    if (useColor) builder.Append(styleEscape);

                    RenderImpl(builder, styled.Contents);
                    _styleEscapes.Pop();

                    if (useColor)
                    {
                        builder.Append(Reset);
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                        foreach (string style in _styleEscapes)
                            builder.Append(style);

                        if (_colorEscapes.TryPeek(out string previousColor))
                            builder.Append(previousColor);
                        else builder.Append(DefaultColor);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                    }
                } break;

                case MarkupScopedSemantic semantic:
                {
                    string colorEscape = GetColorEscapeForMarkupSemantic(semantic.Semantic);
                    _colorEscapes.Push(colorEscape);
                    if (useColor) builder.Append(colorEscape);

                    RenderImpl(builder, semantic.Contents);
                    _colorEscapes.Pop();

                    if (useColor)
                    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                        if (_colorEscapes.TryPeek(out string previousColor))
                            builder.Append(previousColor);
                        else builder.Append(DefaultColor);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                    }
                } break;

                case MarkupSequence sequence:
                {
                    foreach (var child in sequence.Children)
                        RenderImpl(builder, child);
                } break;
            }
        }
    }
}
