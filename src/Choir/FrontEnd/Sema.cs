namespace Choir.FrontEnd;

public sealed partial class Sema(CompilerContext context, LanguageOptions languageOptions)
{
    public CompilerContext Context { get; } = context;
    public LanguageOptions LanguageOptions { get; } = languageOptions;
}
