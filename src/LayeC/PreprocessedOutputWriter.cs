using LayeC.FrontEnd;

namespace LayeC;

public sealed class PreprocessedOutputWriter(TextWriter writer)
{
    private readonly TextWriter _writer = writer;

    private bool _first = true;

    public void WriteToken(IEnumerable<Token> tokens)
    {
        foreach (var token in tokens)
            WriteToken(token);
    }

    public void WriteToken(Token token)
    {
        if (_first)
            _first = false;
        else if (token.IsAtStartOfLine || token.Kind == TokenKind.EndOfFile)
            _writer.WriteLine();
        else if (token.HasWhiteSpaceBefore)
            _writer.Write(' ');

        if (token.Kind == TokenKind.EndOfFile)
            return;

        if (token.Kind is TokenKind.LiteralString or TokenKind.LiteralCharacter)
            WriteEscapedText(token);
        else _writer.Write(Preprocessor.StringizeToken(token, false));
    }

    private void WriteEscapedText(Token token)
    {
        StringView contents = token.StringValue;

        if (token.Kind is TokenKind.LiteralString)
            _writer.Write('"');
        else _writer.Write('\'');

        foreach (char c in contents)
        {
            switch (c)
            {
                default: _writer.Write(c); break;
                case '\n': _writer.Write("\\n"); break;
                case '\r': _writer.Write("\\r"); break;
                case '\t': _writer.Write("\\t"); break;
                case '\b': _writer.Write("\\b"); break;
                case '\f': _writer.Write("\\f"); break;
                case '\a': _writer.Write("\\a"); break;
                case '\v': _writer.Write("\\v"); break;
                case '\0': _writer.Write("\\0"); break;
                case '\'': _writer.Write("\\'"); break;
                case '\"': _writer.Write("\\\""); break;
                case '\\': _writer.Write("\\\\"); break;
            }
        }

        if (token.Kind is TokenKind.LiteralString)
            _writer.Write('"');
        else _writer.Write('\'');
    }
}
