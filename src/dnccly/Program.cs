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

            string programName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "dnccly");
            return CDriver.RunWithArgs(DiagProvider, args, programName);

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
