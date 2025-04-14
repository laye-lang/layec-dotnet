namespace LayeC.Driver;

[Flags]
public enum DriverStage
{
    None = 0,

    /// <summary>
    /// Intended only for use in source files which were preprocessed separately, or when reading just the module header information from a Laye source file.
    /// Still invokes parts of the preprocessor as necessary, but does not perform expansions.
    /// </summary>
    Lex = 1 << 0,

    /// <summary>
    /// The combination lex/presprocess stage.
    /// Preprocessing happens on lexed preprocessor tokens, and must be done interleaved with the lexing process.
    /// This means the two sub-stages cannot be run separately.
    /// </summary>
    Preprocess = 1 << 1,

    /// <summary>
    /// Parse the syntax of the source language into an untyped AST.
    /// When parsing C code, the following step (Sema) is implied unless within a Laye `pragma "C" ( )` expression.
    /// </summary>
    Parse = 1 << 2,

    /// <summary>
    /// Perform semantic analysis on the resulting parsed/untyped AST.
    /// This always happens for C code.
    /// </summary>
    Sema = 1 << 3,

    /// <summary>
    /// Generates code (in-memory) from the results of semantic analysis.
    /// </summary>
    Codegen = 1 << 4,

    /// <summary>
    /// The first stage which will have a file output by default.
    /// Compiling takes the generated code and emits it to a file of the appropriate output assembly format.
    /// </summary>
    Compile = 1 << 5,

    /// <summary>
    /// Assembles the result of the previous compilation step into (an) object file(s).
    /// </summary>
    Assemble = 1 << 6,

    /// <summary>
    /// Links the resulting object files, along with any dependencies, into an executable.
    /// The lowest-level compilers will not use this stage, but a compiler driver referencing this code as a library might.
    /// </summary>
    Link = 1 << 7,
}
