namespace LayeC.Driver;

public abstract class CompilerDriver(string programName)
    : IDisposable
{
    public string ProgramName { get; set; } = programName;

    public abstract int ShowHelp();
    public abstract int Execute();

    public virtual void Dispose()
    {
    }
}

public abstract class CompilerDriverWithContext(string programName, CompilerContext context)
    : CompilerDriver(programName)
{
    public CompilerContext Context { get; } = context;

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        Context.Diag.Dispose();
    }
}
