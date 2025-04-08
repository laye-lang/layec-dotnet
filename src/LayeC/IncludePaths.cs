using LayeC.Extensions;

namespace LayeC;

public sealed class IncludePaths
{
    private readonly List<string> _localIncludePaths = [];
    private readonly List<string> _systemIncludePaths = [];

    public void AddLocalIncludePath(string path)
    {
        _localIncludePaths.Add(path);
    }

    public void AddSystemIncludePath(string path)
    {
        _systemIncludePaths.Add(path);
    }

    public string ResolveIncludePath(string filePath, string relativeToPath)
    {
        if (ResolveLocalIncludePath(ref filePath, relativeToPath))
            return filePath;

        return ResolveIncludePath(filePath);
    }

    public string ResolveIncludePath(string filePath)
    {
        if (ResolveSystemIncludePath(ref filePath))
            return filePath;

        if (ResolveEnvironmentIncludePath(ref filePath))
            return filePath;

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

        foreach (string localIncludePath in _localIncludePaths)
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

    private bool ResolveSystemIncludePath(ref string filePath)
    {
        foreach (string systemIncludePath in _systemIncludePaths)
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

    private bool ResolveEnvironmentIncludePath(ref string filePath)
    {
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
