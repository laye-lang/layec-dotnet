using LayeC.Source;

namespace LayeC.FrontEnd;

public interface IHasSourceInfo
{
    public SourceText Source { get; }
    public SourceRange Range { get; }
}
