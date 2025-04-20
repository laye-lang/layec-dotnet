using Choir.Source;

namespace Choir.FrontEnd;

public sealed partial class LayeParser(CompilerContext context, LanguageOptions languageOptions, SourceText source, ITokenStream tokens)
    : Parser(context, languageOptions, source, tokens)
{
}
