using System.Diagnostics;
using System.Text;

using LayeC.Driver;
using LayeC.Formatting;
using LayeC.Source;

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
    private const string Grey = "\x1b[90m";
    private const string White = "\x1b[1m\x1b[97m";

    public const int MinRenderWidth = 8;

    private static string GetColorForDiagnosticLevel(DiagnosticLevel level) => level switch
    {
        DiagnosticLevel.Note => Green,
        DiagnosticLevel.Remark => Yellow,
        DiagnosticLevel.Warning => Magenta,
        DiagnosticLevel.Error => Red,
        DiagnosticLevel.Fatal => Cyan,
        _ => White,
    };

    private static string GetColorEscapeForMarkupColor(MarkupColor color) => color switch
    {
        MarkupColor.Black => Grey,
        MarkupColor.Red => Red,
        MarkupColor.Green => Green,
        MarkupColor.Yellow => Yellow,
        MarkupColor.Blue => Blue,
        MarkupColor.Magenta => Magenta,
        MarkupColor.Cyan => Cyan,
        MarkupColor.White => White,
        _ => White,
    };

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

        static string Impl(MarkupStyle style) => style switch
        {
            MarkupStyle.Bold => Bold,
            MarkupStyle.Italic => Italic,
            MarkupStyle.Underline => Underline,
            MarkupStyle.Monospace => Normal,
            MarkupStyle.None => Normal,
            _ => Normal,
        };
    }

    private static string GetColorEscapeForMarkupSemantic(MarkupSemantic semantic) => semantic switch
    {
        MarkupSemantic.Entity => White,
        MarkupSemantic.EntityParameter => White,
        MarkupSemantic.EntityLocal => White,
        MarkupSemantic.EntityGlobal => White,
        MarkupSemantic.EntityMember => White,
        MarkupSemantic.EntityFunction => White,
        MarkupSemantic.EntityType => White,
        MarkupSemantic.EntityTypeValue => White,
        MarkupSemantic.EntityTypeStruct => White,
        MarkupSemantic.EntityTypeEnum => White,
        MarkupSemantic.EntityNamespace => White,
        MarkupSemantic.Keyword => Blue,
        MarkupSemantic.KeywordControlFlow => Blue,
        MarkupSemantic.KeywordOperator => Blue,
        MarkupSemantic.KeywordType => Blue,
        MarkupSemantic.KeywordQualifier => Blue,
        MarkupSemantic.Literal => Yellow,
        MarkupSemantic.LiteralNumber => Yellow,
        MarkupSemantic.LiteralString => Yellow,
        MarkupSemantic.LiteralInvalid => Red,
        MarkupSemantic.LiteralKeyword => Blue,
        MarkupSemantic.Punctuation => Grey,
        MarkupSemantic.PunctuationDelimiter => Grey,
        MarkupSemantic.PunctuationOperator => Grey,
        _ => White,
    };

    public TextWriter Writer { get; } = writer;
    public bool UseColor { get; } = useColor;

    public bool UseByteLocations { get; } = true;
    public bool RenderMultipleSourceLines { get; } = false;

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

    private void ResetColor(StringBuilder builder)
    {
        if (!UseColor) return;
        builder.Append(Reset);
    }

    private void WriteColor(StringBuilder builder, DiagnosticLevel level)
    {
        if (!UseColor) return;
        builder.Append(GetColorForDiagnosticLevel(level));
    }

    private void WriteColor(StringBuilder builder, MarkupColor color)
    {
        if (!UseColor) return;
        builder.Append(GetColorEscapeForMarkupColor(color));
    }

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

            groupBuilder.Append('[');
            WriteColor(groupBuilder, diag.Level);
            string diagLevelString = diag.Level.ToString();
            groupBuilder.Append(diagLevelString);
            ResetColor(groupBuilder);
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

            string[] renderedLines = formatter.Render(new MarkupScopedStyle(MarkupStyle.Bold, diag.Message)).Split('\n');
            Debug.Assert(renderedLines.Length > 0);
            groupBuilder.AppendLine(renderedLines[0]);

            for (int i = 1; i < renderedLines.Length; i++)
            {
                groupBuilder.Append("│ ");
                for (int j = 0; j < wellInnerWidth; j++)
                    groupBuilder.Append(' ');
                groupBuilder.Append(" │");
                for (int j = 0; j < 6 + diagLevelString.Length - (4 + wellInnerWidth); j++)
                    groupBuilder.Append(' ');

                groupBuilder.AppendLine(renderedLines[i]);
            }

            if (diag.Source is not null)
            {
                groupBuilder.Append("│ ");
                for (int i = 0; i < wellInnerWidth; i++)
                    groupBuilder.Append(' ');
                groupBuilder.AppendLine(" │");

                var lineInfo = diag.Source.Seek(diag.Location);
                SourceLocationInfo? prevInfo = null, nextInfo = null;

                if (RenderMultipleSourceLines)
                {
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
                }

                int sharedLeadingSpace = 0;

                if (prevInfo is not null)
                    RenderLine(prevInfo.Value);

                RenderLine(lineInfo, true);

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
                WriteColor(groupBuilder, diag.Level);
                groupBuilder.AppendLine("^");
                ResetColor(groupBuilder);

                if (nextInfo is not null)
                    RenderLine(nextInfo.Value);

                void RenderLine(SourceLocationInfo info, bool isPrimary = false)
                {
                    int lineNumberWidth = 1 + (int)Math.Log10(info.Line);
                    groupBuilder.Append('│');
                    for (int i = 0; i < 1 + (wellInnerWidth - lineNumberWidth); i++)
                        groupBuilder.Append(' ');
                    groupBuilder.Append(info.Line);
                    groupBuilder.Append(" │ ");

                    if (!isPrimary)
                        WriteColor(groupBuilder, MarkupColor.Black);
                    groupBuilder.AppendLine((string)info.LineText[sharedLeadingSpace..Math.Min(sharedLeadingSpace + 120, info.LineLength)]);
                    if (!isPrimary)
                        ResetColor(groupBuilder);
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

        groupBuilder.AppendLine();
        return groupBuilder.ToString();
    }

    private sealed class FormattedDiagnosticMessageMarkupRenderer(bool useColor)
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

                case MarkupLineBreak:
                {
                    builder.Append(Reset);
                    builder.Append('\n');

                    foreach (string style in _styleEscapes)
                        builder.Append(style);

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    if (_colorEscapes.TryPeek(out string previousColor))
                        builder.Append(previousColor);
                    else builder.Append(DefaultColor);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                } break;

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
                        foreach (string style in _styleEscapes)
                            builder.Append(style);

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
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
