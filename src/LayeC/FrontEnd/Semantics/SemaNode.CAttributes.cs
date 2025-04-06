using LayeC.Source;

namespace LayeC.FrontEnd.Semantics;

public sealed class SemaCAttributesBuilder(SourceText source)
{
    public SourceText Source { get; } = source;

    public SemaCAttributes Build()
    {
        return new(Source, default);
    }
}

public sealed class SemaCAttributes(SourceText source, SourceRange range)
    : SemaNode(source, range)
{
    protected override string DebugNodeName { get; } = nameof(SemaCAttributes);
    protected override IEnumerable<ITreeDebugNode> Children { get; } = [];
}

public abstract class SemaCAttribute(SourceText source, SourceRange range)
    : SemaNode(source, range)
{
}

public abstract class SemaCAttributeGNU(Token attributeToken)
    : SemaCAttribute(attributeToken.Source, attributeToken.Range)
{
}
