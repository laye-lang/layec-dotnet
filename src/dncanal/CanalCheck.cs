using System.Text.RegularExpressions;

using Choir.Source;

namespace Canal;

public sealed class CanalCheck
{
    public required CanalDirective Directive { get; init; }
    public required SourceLocation Location { get; init; }

    public string? StringData { get; init; }
    public Regex? RegexData { get; init; }
    public EnvironmentRegex? EnvironmentRegexData { get; init; }
}
