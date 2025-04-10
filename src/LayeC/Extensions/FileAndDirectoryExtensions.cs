namespace System.IO;

public static class FileAndDirectoryExtensions
{
    public static FileInfo ChildFile(this DirectoryInfo di, string childPath) => new(Path.Combine(di.FullName, childPath));
    public static DirectoryInfo ChildDirectory(this DirectoryInfo di, string childPath) => new(Path.Combine(di.FullName, childPath));

    public static string ReadAllText(this FileInfo fi) => File.ReadAllText(fi.FullName);
    public static string[] ReadAllLines(this FileInfo fi) => File.ReadAllLines(fi.FullName);
}
