using LayeC.Source;

namespace LayeC.FrontEnd;

public interface ITokenStream
{
    public bool IsAtEnd { get; }
    public SourceLanguage Language { get; }
    public Token Read();
}

public sealed class LexerTokenStream(Lexer lexer)
    : ITokenStream
{
    public Lexer Lexer { get; } = lexer;

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

public sealed class BufferTokenStream(Token[] tokens, PreprocessorMacroDefinition? sourceMacro = null)
    : ITokenStream
{
    public static BufferTokenStream CreateFromFullPreprocess(CompilerContext context, LanguageOptions languageOptions, SourceText source)
    {
        var pp = new Preprocessor(context, languageOptions, PreprocessorMode.Full);
        var tokens = pp.PreprocessSource(source);
        return new BufferTokenStream(tokens);
    }

    public PreprocessorMacroDefinition? SourceMacro { get; } = sourceMacro;

    private readonly Token[] _tokens = tokens;
    private int _position = 0;

    public bool IsAtEnd => _position >= _tokens.Length;
    public SourceLanguage Language => IsAtEnd ? SourceLanguage.None : _tokens[_position].Language;

    public bool KeepWhenEmpty { get; init; } = false;

    public BufferTokenStream(IEnumerable<Token> tokens, PreprocessorMacroDefinition? sourceMacro = null)
        : this([.. tokens], sourceMacro)
    {
    }

    public Token Read()
    {
        var ppToken = _tokens[_position];
        _position++;
        return ppToken;
    }
}

public sealed class PreprocessorTokenStream
    : ITokenStream
{
    public static PreprocessorTokenStream CreateMinimal(CompilerContext context, LanguageOptions languageOptions, SourceText source)
    {
        var pp = new Preprocessor(context, languageOptions, PreprocessorMode.Minimal);
        pp.PushSourceTokenStream(source);
        return new PreprocessorTokenStream(pp);
    }

    private readonly Preprocessor _pp;

    private bool _isAtEnd = false;
    public bool IsAtEnd => _isAtEnd;
    public SourceLanguage Language => _pp.Language;

    private PreprocessorTokenStream(Preprocessor pp)
    {
        _pp = pp;
    }

    public Token Read()
    {
        _pp.Context.Assert(!IsAtEnd && !_pp.IsAtEnd, "Can't read from a preprocessor token stream past the end.");
        var ppToken = _pp.ReadToken();
        if (ppToken.Kind == TokenKind.EndOfFile)
            _isAtEnd = true;
        return ppToken;
    }
}
