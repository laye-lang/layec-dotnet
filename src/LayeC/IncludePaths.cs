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

    public void AddSystemIncludePath(string path, bool prepend = false)
    {
        if (prepend)
            _systemIncludePaths.Insert(0, path);
        else _systemIncludePaths.Add(path);
    }

    public void AddIncludePath(string path)
    {
        _includePaths.Add(path);
    }

    public string ResolveIncludePath(string filePath, string relativeToPath, out bool isSystemHeader, ref bool includeNext)
    {
        isSystemHeader = false;

        if (ResolveLocalIncludePath(ref filePath, relativeToPath, ref includeNext))
            return filePath;

        return ResolveIncludePath(filePath, out isSystemHeader, ref includeNext);
    }

    public string ResolveIncludePath(string filePath, out bool isSystemHeader, ref bool includeNext)
    {
        if (ResolveIncludePath(ref filePath, out isSystemHeader, ref includeNext))
            return filePath;

        if (ResolveEnvironmentIncludePath(ref filePath, out isSystemHeader, ref includeNext))
            return filePath;

        isSystemHeader = false;
        return filePath;
    }

    private bool ResolveLocalIncludePath(ref string filePath, string relativeToPath, ref bool includeNext)
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
                if (includeNext)
                {
                    includeNext = false;
                    continue;
                }

                filePath = localFilePath;
                return true;
            }
        }

        return false;
    }

    private bool ResolveIncludePath(ref string filePath, out bool isSystemHeader, ref bool includeNext)
    {
        isSystemHeader = false;

        foreach (string systemIncludePath in _systemIncludePaths)
        {
            string systemFilePath = Path.Combine(systemIncludePath, filePath);
            if (File.Exists(systemFilePath))
            {
                if (includeNext)
                {
                    includeNext = false;
                    continue;
                }

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
                if (includeNext)
                {
                    includeNext = false;
                    continue;
                }

                isSystemHeader = false;
                filePath = systemFilePath;
                return true;
            }
        }

        return false;
    }

    private bool ResolveEnvironmentIncludePath(ref string filePath, out bool isSystemHeader, ref bool includeNext)
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
                if (includeNext)
                {
                    includeNext = false;
                    continue;
                }

                filePath = systemFilePath;
                return true;
            }
        }

        return false;
    }
}
