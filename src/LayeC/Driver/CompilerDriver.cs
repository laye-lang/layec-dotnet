using LayeC.Diagnostics;

namespace LayeC.Driver;

public abstract class CompilerDriver(string programName, DiagnosticEngine diagnosticEngine)
    : IDisposable
{
    public string ProgramName { get; set; } = programName;
    public DiagnosticEngine Diag { get; } = diagnosticEngine;

    public abstract int ShowHelp();
    public abstract int Execute();

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        Diag.Dispose();
    }
}

public abstract class CompilerDriverWithContext(string programName, CompilerContext context)
    : CompilerDriver(programName, context.Diag)
{
    public CompilerContext Context { get; } = context;

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        Context.Diag.Dispose();
    }
}
