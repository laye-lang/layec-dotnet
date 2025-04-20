using System.Text;

using Choir.Diagnostics;
using Choir.Driver;

namespace Choir;

public static class Program
{
    public static int Main(string[] args)
    {
        var previousInputEncoding = Console.InputEncoding;
        var previousOutputEncoding = Console.OutputEncoding;

        try
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            string programName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "dnlayec");
            if (args is ["-cc1", .. string[] rest])
                return CDriver.RunWithArgs(DiagProvider, rest, $"{programName} -cc1");
            else return LayeCDriver.RunWithArgs(DiagProvider, args, programName);

            static IDiagnosticConsumer DiagProvider(bool useColor)
            {
                return new FormattedDiagnosticWriter(Console.Out, useColor);
            }
        }
        finally
        {
            Console.InputEncoding = previousInputEncoding;
            Console.OutputEncoding = previousOutputEncoding;
        }
    }
}
