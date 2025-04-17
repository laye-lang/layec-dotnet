using LayeC.Diagnostics;

namespace LayeC.Driver;

public abstract class CompilerDriver(string programName, DiagnosticEngine diagnosticEngine)
    : IDisposable
{
    public static string SelfExeDirectoryPath => AppContext.BaseDirectory;
    public static DirectoryInfo SelfExeDir => new FileInfo(SelfExeDirectoryPath).Directory!;

    protected static DirectoryInfo? FindRelativeDirectory(DirectoryInfo relativeToDir, params string[] relativeDirPaths)
    {
        DirectoryInfo? searchDir = relativeToDir;
        while (searchDir is not null)
        {
            foreach (string relativeDirPath in relativeDirPaths)
            {
                var checkDir = searchDir.ChildDirectory(relativeDirPath);
                if (checkDir.Exists)
                    return checkDir;
            }

            searchDir = searchDir.Parent;
        }

        return null;
    }

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
