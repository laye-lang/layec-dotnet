namespace Canal;

public sealed class CanalTestResult
{
    public required bool Success { get; init; }
    public string Output { get; init; } = "";
    public string ErrorMessage { get; init; } = "";
}

public sealed class CanalTest
{
    public required string RunDirective { get; init; }
    public required CanalPrefixState State { get; init; }
    public bool VerifyOnly { get; init; }
    public bool ExpectFailure { get; init; }
}
