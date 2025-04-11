using LayeC.FrontEnd;

namespace LayeC;

public sealed class PreprocessedOutputWriter(TextWriter writer, bool includeComments = false)
{
    private readonly TextWriter _writer = writer;
    private readonly bool _includeComments = includeComments;

    public void WriteToken(IEnumerable<Token> tokens)
    {
        foreach (var token in tokens)
            WriteToken(token);
    }

    public void WriteToken(Token token)
    {
        TryPrintTrivia(token.LeadingTrivia);

        if (token.Kind == TokenKind.EndOfFile)
            return;

        if (token.Kind is TokenKind.LiteralString or TokenKind.LiteralCharacter)
            WriteEscapedText(token);
        else _writer.Write(Preprocessor.StringizeToken(token, false));

        TryPrintTrivia(token.TrailingTrivia);
    }

    private void TryPrintTrivia(TriviaList trivia)
    {
        foreach (var trivium in trivia.Trivia)
        {
            var source = trivium.Source;
            switch (trivium)
            {
                case TriviumLiteral l: _writer.Write((string)l.Literal); break;
                case TriviumShebangComment: _writer.Write(source.Substring(trivium.Range)); break;
                case TriviumWhiteSpace: _writer.Write(source.Substring(trivium.Range)); break;
                case TriviumNewLine: _writer.WriteLine(); break;
                case TriviumLineComment: if (_includeComments) _writer.Write(source.Substring(trivium.Range)); break;
                case TriviumDelimitedComment: if (_includeComments) _writer.Write(source.Substring(trivium.Range)); break;
            }
        }
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
