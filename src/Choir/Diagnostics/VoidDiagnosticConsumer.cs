namespace Choir.Diagnostics;

public sealed class VoidDiagnosticConsumer
    : IDiagnosticConsumer
{
    public static readonly VoidDiagnosticConsumer Instance = new();
    private VoidDiagnosticConsumer() { }
    public void Consume(Diagnostic diag) { }
    public void Flush() { }
}
