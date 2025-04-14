using System.Diagnostics;

namespace LayeC.Source;

// TODO(local): move the SourceLanguage field present in the Lexer and Parser into the source text itself, as each source has a "primary" language.
public sealed class SourceText(string name, string text, SourceLanguage language)
    : IEquatable<SourceText>
{
    public static readonly SourceText Unknown = new("<???>", "", SourceLanguage.None);

    private static int _counter = 0;

    public readonly int Id = Interlocked.Increment(ref _counter);
    public readonly string Name = name;
    public readonly string Text = text;
    public readonly int Length = text.Length;
    public readonly SourceLanguage Language = language;

    public bool IsSystemHeader { get; set; } = false;

    private int[]? _lineStartOffsets;

    public SourceLocationInfoShort SeekLineColumn(SourceLocation location)
    {
        EnsureLineStartOffsetsCalculated();
        Debug.Assert(_lineStartOffsets is not null);

        int lineIndex = Array.BinarySearch(_lineStartOffsets, location.Offset);
        if (lineIndex < 0) lineIndex = (~lineIndex) - 1;

        Debug.Assert(lineIndex >= 0 && lineIndex < _lineStartOffsets.Length);
        return new(1 + lineIndex, 1+ location.Offset - _lineStartOffsets[lineIndex]);
    }

    public SourceLocationInfo Seek(SourceLocation location)
    {
        EnsureLineStartOffsetsCalculated();
        Debug.Assert(_lineStartOffsets is not null);

        int lineIndex = Array.BinarySearch(_lineStartOffsets, location.Offset);
        if (lineIndex < 0) lineIndex = (~lineIndex) - 1;

        Debug.Assert(lineIndex >= 0 && lineIndex < _lineStartOffsets.Length);
        
        int lineStart = _lineStartOffsets[lineIndex];
        int nextLineStart = lineIndex + 1 < _lineStartOffsets.Length ? _lineStartOffsets[lineIndex + 1] : Text.Length;
        int lineLength = nextLineStart - lineStart;

        while (lineLength > 0 && Text[lineStart + lineLength - 1] is '\n' or '\r')
            lineLength--;

        return new(1 + lineIndex, 1 + location.Offset - lineStart, lineStart, lineLength, Text.AsMemory(lineStart..(lineStart + lineLength)));
    }

    public SourceLocationInfo[] GetLineInfos()
    {
        EnsureLineStartOffsetsCalculated();
        Debug.Assert(_lineStartOffsets is not null);

        var lineInfos = new SourceLocationInfo[_lineStartOffsets.Length];
        for (int lineIndex = 0; lineIndex < _lineStartOffsets.Length; lineIndex++)
        {
            int lineStart = _lineStartOffsets[lineIndex];
            int nextLineStart = lineIndex + 1 < _lineStartOffsets.Length ? _lineStartOffsets[lineIndex + 1] : Text.Length;
            int lineLength = nextLineStart - lineStart;

            while (lineLength > 0 && Text[lineStart + lineLength - 1] is '\n' or '\r')
                lineLength--;

            lineInfos[lineIndex] = new SourceLocationInfo(1 + lineIndex, 1, lineStart, lineLength, Text.AsMemory(lineStart..(lineStart + lineLength)));
        }

        return lineInfos;
    }

    private void EnsureLineStartOffsetsCalculated()
    {
        if (_lineStartOffsets is not null)
            return;

        var offsets = new List<int>() { 0 };
        for (int i = 0; i < Text.Length; )
        {
            char c = Text[i], n = i + 1 < Text.Length ? Text[i + 1] : '\0';
            switch (c)
            {
                default: i++; break;

                case '\n' when n == '\r':
                case '\r' when n == '\n':
                {
                    i += 2;
                    offsets.Add(i);
                } break;

                case '\n' or '\r':
                {
                    i++;
                    offsets.Add(i);
                } break;
            }
        }

        _lineStartOffsets = [.. offsets];
    }

    public override string ToString() => $"[{Id}] \"{Name}\"";
    public override int GetHashCode() => HashCode.Combine(Id);
    public override bool Equals(object? obj) => Equals(obj as SourceText);
    public bool Equals(SourceText? other) => other is not null && Id == other.Id;

    public StringView Slice(SourceLocation begin, SourceLocation end) => Text.AsMemory(begin.Offset..end.Offset);
    public StringView Slice(SourceRange range) => Text.AsMemory(range.Begin.Offset..range.End.Offset);

    public string Substring(SourceLocation begin, SourceLocation end) => Slice(begin, end).ToString();
    public string Substring(SourceRange range) => Slice(range).ToString();
}
