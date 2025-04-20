namespace Canal;

public sealed class CanalPrefixState
{
    public required string Prefix { get; init; }

    public List<CanalCheck> Checks { get; } = [];
    public List<char> LiteralCharacters { get; } = [];

    public bool ForceRegex { get; set; }
    public bool NoCapture { get; set; }
    public bool CaptureTypes { get; set; }
}
