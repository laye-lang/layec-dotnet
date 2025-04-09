namespace LayeC.Driver;

public enum DriverStage
{
    /// <summary>
    /// The combination lex/presprocess stage.
    /// Preprocessing happens on lexed preprocessor tokens, and must be done interleaved with the lexing process.
    /// This means the two sub-stages cannot be run separately.
    /// </summary>
    Preprocess,

    /// <summary>
    /// Parse the syntax of the source language into an untyped AST.
    /// When parsing C code, the following step (Sema) is implied unless within a Laye `pragma "C" ( )` expression.
    /// </summary>
    Parse,

    /// <summary>
    /// Perform semantic analysis on the resulting parsed/untyped AST.
    /// This always happens for C code.
    /// </summary>
    Sema,

    /// <summary>
    /// Generates code (in-memory) from the results of semantic analysis.
    /// </summary>
    Codegen,

    /// <summary>
    /// The first stage which will have a file output by default.
    /// Compiling takes the generated code and emits it to a file of the appropriate output assembly format.
    /// </summary>
    Compile,

    /// <summary>
    /// Assembles the result of the previous compilation step into (an) object file(s).
    /// </summary>
    Assemble,

    /// <summary>
    /// Links the resulting object files, along with any dependencies, into an executable.
    /// The lowest-level compilers will not use this stage, but a compiler driver referencing this code as a library might.
    /// </summary>
    Link,
}
