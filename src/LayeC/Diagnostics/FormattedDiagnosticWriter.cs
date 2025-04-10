using System.Diagnostics;
using System.Text;

using LayeC.Formatting;
using LayeC.Source;

namespace LayeC.Diagnostics;

public sealed class FormattedDiagnosticWriter(TextWriter writer, bool useColor, bool useByteLocations = true)
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
    public bool UseByteLocations { get; } = useByteLocations;

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
        var formatter = new FormattedDiagnosticMessageMarkupRenderer(UseColor);

        const int WellEdgeWidth = 2;
        const int WellNumberWidthMin = 3;

        int wellInnerWidth = WellNumberWidthMin;

        // calculate the width of the line number well
        foreach (var diag in group)
        {
            if (diag.Source is null) continue;

            var shortInfo = diag.Source.SeekLineColumn(diag.Location);
            int lineNumberWidth = 1 + (int)Math.Log10(shortInfo.Line + 1);
            wellInnerWidth = Math.Max(wellInnerWidth, lineNumberWidth);
        }

        int wellInnerLeftPadding = WellEdgeWidth + Math.Max(0, wellInnerWidth - 3);
        bool renderWellBottom = true;

        for (int gi = 0; gi < group.Length; gi++)
        {
            Diagnostic diag = group[gi];

            if (gi == 0)
                groupBuilder.Append('╭');
            else groupBuilder.Append('├');

            for (int i = 0; i < wellInnerLeftPadding - 1; i++)
                groupBuilder.Append('─');

            var levelColor = diag.Level switch
            {
                DiagnosticLevel.Note => MarkupColor.Green,
                DiagnosticLevel.Remark => MarkupColor.Yellow,
                DiagnosticLevel.Warning => MarkupColor.Magenta,
                DiagnosticLevel.Error => MarkupColor.Red,
                DiagnosticLevel.Fatal => MarkupColor.Cyan,
                _ => MarkupColor.White,
            };

            groupBuilder.Append('[');
            if (UseColor)
                groupBuilder.Append(GetColorEscapeForMarkupColor(levelColor));
            groupBuilder.Append(diag.Level);
            if (UseColor)
                groupBuilder.Append(Reset);
            groupBuilder.Append(']');

            if (diag.Source is not null)
            {
                var shortInfo = diag.Source.SeekLineColumn(diag.Location);
                groupBuilder.Append('@');
                groupBuilder.Append(diag.Source.Name);

                if (UseByteLocations)
                {
                    groupBuilder.Append('[');
                    groupBuilder.Append(diag.Location.Offset);
                    groupBuilder.Append(']');
                }
                else
                {
                    groupBuilder.Append('(');
                    groupBuilder.Append(shortInfo.Line);
                    groupBuilder.Append(", ");
                    groupBuilder.Append(shortInfo.Column);
                    groupBuilder.Append(')');
                }

                groupBuilder.AppendLine();
                groupBuilder.Append("│ ");
                for (int i = 0; i < wellInnerWidth; i++)
                    groupBuilder.Append(' ');
                groupBuilder.Append(" ├─ ");
            }
            else groupBuilder.Append(": ");

            groupBuilder.AppendLine(formatter.Render(new MarkupScopedStyle(MarkupStyle.Bold, diag.Message)));

            if (diag.Source is not null)
            {
                groupBuilder.Append("│ ");
                for (int i = 0; i < wellInnerWidth; i++)
                    groupBuilder.Append(' ');
                groupBuilder.AppendLine(" │");

                var lineInfo = diag.Source.Seek(diag.Location);
                SourceLocationInfo? prevInfo = null, nextInfo = null;

                if (lineInfo.LineStart > 0)
                {
                    prevInfo = diag.Source.Seek(lineInfo.LineStart - 1);
                    if (prevInfo.Value.LineLength == 0 || prevInfo.Value.LineText.All(char.IsWhiteSpace))
                        prevInfo = null;
                }

                if (lineInfo.LineStart + lineInfo.LineLength < diag.Source.Length)
                {
                    nextInfo = diag.Source.Seek(lineInfo.LineStart + lineInfo.LineLength + 1);
                    if (nextInfo.Value.LineLength == 0 || nextInfo.Value.LineText.All(char.IsWhiteSpace))
                        nextInfo = null;
                }

                int sharedLeadingSpace = 0;

                if (prevInfo is not null)
                    RenderLine(prevInfo.Value);

                RenderLine(lineInfo);

                if (gi == group.Length - 1 && nextInfo is null)
                {
                    renderWellBottom = false;
                    groupBuilder.Append("╰─");
                    for (int i = 0; i < wellInnerWidth; i++)
                        groupBuilder.Append('─');
                    groupBuilder.Append("─╯ ");
                }
                else
                {
                    groupBuilder.Append("│ ");
                    for (int i = 0; i < wellInnerWidth; i++)
                        groupBuilder.Append(' ');
                    groupBuilder.Append(" │ ");
                }

                for (int i = 0; i < lineInfo.Column - 1; i++)
                    groupBuilder.Append(' ');
                groupBuilder.AppendLine("^");

                if (nextInfo is not null)
                    RenderLine(nextInfo.Value);

                void RenderLine(SourceLocationInfo info)
                {
                    int lineNumberWidth = 1 + (int)Math.Log10(info.Line);
                    groupBuilder.Append('│');
                    for (int i = 0; i < 1 + (wellInnerWidth - lineNumberWidth); i++)
                        groupBuilder.Append(' ');
                    groupBuilder.Append(info.Line);
                    groupBuilder.Append(" │ ");
                    groupBuilder.AppendLine((string)info.LineText[sharedLeadingSpace..Math.Min(sharedLeadingSpace + 120, info.LineLength)]);
                }
            }
        }

        if (renderWellBottom)
        {
            ResetColor(groupBuilder);
            groupBuilder.Append('╰');
            for (int i = 0; i < wellInnerWidth + (2 * (WellEdgeWidth - 1)); i++)
                groupBuilder.Append('─');
            groupBuilder.Append('╯');
        }

        return groupBuilder.ToString();
    }

#pragma warning disable CS9113 // Parameter is unread.
    private sealed class FormattedDiagnosticMessageMarkupRenderer(bool useColor)
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
