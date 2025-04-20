using Choir.Source;

namespace Choir.FrontEnd;

public sealed partial class CParser(Sema sema, SourceText source, ITokenStream tokens)
    : Parser(sema.Context, sema.LanguageOptions, source, tokens)
{
    public Sema Sema { get; } = sema;
}
