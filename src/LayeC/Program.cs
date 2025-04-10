using System.Text;

using LayeC.Diagnostics;
using LayeC.Driver;

namespace LayeC;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        static IDiagnosticConsumer DiagProvider(bool useColor) => new FormattedDiagnosticWriter(Console.Out, useColor);

        string programName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "dnlayec");
        if (args is ["-cc1", ..])
        {
            args = args[1..];
            return CDriver.RunWithArgs(DiagProvider, args, $"{programName} -cc1");
        }

        return LayeCDriver.RunWithArgs(DiagProvider, args, programName);
    }
}
