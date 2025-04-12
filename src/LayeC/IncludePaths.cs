namespace LayeC;

public sealed class IncludePaths
{
    private readonly List<string> _quoteIncludePaths = [];
    private readonly List<string> _systemIncludePaths = [];
    private readonly List<string> _includePaths = [];

    public void AddQuoteIncludePath(string path)
    {
        _quoteIncludePaths.Add(path);
    }

    public void AddSystemIncludePath(string path)
    {
        _systemIncludePaths.Add(path);
    }

    public void AddIncludePath(string path)
    {
        _includePaths.Add(path);
    }

    public string ResolveIncludePath(string filePath, string relativeToPath, out bool isSystemHeader)
    {
        isSystemHeader = false;

        if (ResolveLocalIncludePath(ref filePath, relativeToPath))
            return filePath;

        return ResolveIncludePath(filePath, out isSystemHeader);
    }

    public string ResolveIncludePath(string filePath, out bool isSystemHeader)
    {
        if (ResolveIncludePath(ref filePath, out isSystemHeader))
            return filePath;

        if (ResolveEnvironmentIncludePath(ref filePath, out isSystemHeader))
            return filePath;

        isSystemHeader = false;
        return filePath;
    }

    private bool ResolveLocalIncludePath(ref string filePath, string relativeToPath)
    {
        var relativeDir = new FileInfo(relativeToPath).Directory;
        var siblingFile = relativeDir?.ChildFile(filePath);
        if (siblingFile is not null && siblingFile.Exists)
        {
            filePath = siblingFile.FullName;
            return true;
        }

        foreach (string localIncludePath in _quoteIncludePaths)
        {
            string localFilePath = Path.Combine(localIncludePath, filePath);
            if (File.Exists(localFilePath))
            {
                filePath = localFilePath;
                return true;
            }
        }

        return false;
    }

    private bool ResolveIncludePath(ref string filePath, out bool isSystemHeader)
    {
        isSystemHeader = false;

        foreach (string systemIncludePath in _systemIncludePaths)
        {
            string systemFilePath = Path.Combine(systemIncludePath, filePath);
            if (File.Exists(systemFilePath))
            {
                isSystemHeader = true;
                filePath = systemFilePath;
                return true;
            }
        }

        foreach (string includePath in _includePaths)
        {
            string systemFilePath = Path.Combine(includePath, filePath);
            if (File.Exists(systemFilePath))
            {
                isSystemHeader = false;
                filePath = systemFilePath;
                return true;
            }
        }

        return false;
    }

    private bool ResolveEnvironmentIncludePath(ref string filePath, out bool isSystemHeader)
    {
        isSystemHeader = false;

        string? envIncludePaths = Environment.GetEnvironmentVariable("Include") ?? Environment.GetEnvironmentVariable("INCLUDE");
        if (envIncludePaths is null)
            return false;

        foreach (string systemIncludePath in envIncludePaths.Split(Path.PathSeparator))
        {
            string systemFilePath = Path.Combine(systemIncludePath, filePath);
            if (File.Exists(systemFilePath))
            {
                filePath = systemFilePath;
                return true;
            }
        }

        return false;
    }
}
