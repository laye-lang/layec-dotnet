#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.IO;
#pragma warning restore IDE0130 // Namespace does not match folder structure
public static class FileAndDirectoryExtensions
{
    public static FileInfo ChildFile(this DirectoryInfo di, string childPath) => new(Path.Combine(di.FullName, childPath));
    public static DirectoryInfo ChildDirectory(this DirectoryInfo di, string childPath) => new(Path.Combine(di.FullName, childPath));

    public static string ReadAllText(this FileInfo fi) => File.ReadAllText(fi.FullName);
    public static string[] ReadAllLines(this FileInfo fi) => File.ReadAllLines(fi.FullName);

    public static void WriteAllLines(this FileInfo fi, string[] lines) => File.WriteAllLines(fi.FullName, lines);
    public static void WriteAllLines(this FileInfo fi, IEnumerable<string> lines) => File.WriteAllLines(fi.FullName, lines);
}
