namespace LayeC;

public enum AssemblerFormat
{
    /// <summary>
    /// Choose a sensible default for the platform, target, and/or other CLI flags.
    /// </summary>
    Default,

    /// <summary>
    /// Generate assembly code for the GNU assembler.
    /// </summary>
    GAS,

    /// <summary>
    /// Generate assembly code for NASM.
    /// </summary>
    NASM,

    /// <summary>
    /// Generate assembly code for FASM.
    /// </summary>
    FASM,

    /// <summary>
    /// Generate QBE code.
    /// </summary>
    QBE,

    /// <summary>
    /// Generate LLVM code.
    /// </summary>
    LLVM,
}
