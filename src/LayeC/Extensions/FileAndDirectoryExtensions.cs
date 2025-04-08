namespace LayeC.Extensions;

public static class FileAndDirectoryExtensions
{
    public static FileInfo ChildFile(this DirectoryInfo di, string childPath) => new(Path.Combine(di.FullName, childPath));
    public static DirectoryInfo ChildDirectory(this DirectoryInfo di, string childPath) => new(Path.Combine(di.FullName, childPath));
}
