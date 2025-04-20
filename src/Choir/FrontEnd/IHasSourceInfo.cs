using Choir.Source;

namespace Choir.FrontEnd;

public interface IHasSourceInfo
{
    public SourceText Source { get; }
    public SourceRange Range { get; }
}
