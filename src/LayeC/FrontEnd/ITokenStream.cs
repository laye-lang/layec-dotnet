using System.Diagnostics.Metrics;

using LayeC.Source;

namespace LayeC.FrontEnd;

public interface ITokenStream
{
    public bool IsAtEnd { get; }
    public SourceText Source { get; }
    public SourceLanguage Language { get; }
    public Token Read();
}

public sealed class LexerTokenStream(Lexer lexer)
    : ITokenStream
{
    public Lexer Lexer { get; } = lexer;
    public SourceText Source { get; } = lexer.Source;

    private bool _isAtEnd = false;
    public bool IsAtEnd => _isAtEnd;
    public SourceLanguage Language => Lexer.Language;

    public Token Read()
    {
        var ppToken = Lexer.ReadNextPPToken();
        if (ppToken.Kind == TokenKind.EndOfFile)
            _isAtEnd = true;
        return ppToken;
    }
}

public sealed class BufferTokenStream(IEnumerable<Token> tokens, PreprocessorMacroDefinition? sourceMacro = null)
    : ITokenStream
{
    public PreprocessorMacroDefinition? SourceMacro { get; } = sourceMacro;

    private readonly Token[] _tokens = [.. tokens];
    private int _position = 0;

    public bool IsAtEnd => _position >= _tokens.Length;
    public SourceText Source { get; } = tokens.FirstOrDefault()?.Source ?? new("???", "");
    public SourceLanguage Language => IsAtEnd ? SourceLanguage.None : _tokens[_position].Language;

    public bool KeepWhenEmpty { get; init; } = false;

    public Token Read()
    {
        var ppToken = _tokens[_position];
        _position++;
        return ppToken;
    }
}
