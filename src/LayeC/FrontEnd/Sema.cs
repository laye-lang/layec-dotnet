namespace LayeC.FrontEnd;

public sealed class Sema(CompilerContext context, LanguageOptions languageOptions)
{
    public CompilerContext Context { get; } = context;
    public LanguageOptions LanguageOptions { get; } = languageOptions;
}
