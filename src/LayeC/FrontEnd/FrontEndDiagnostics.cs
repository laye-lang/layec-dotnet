using System.Diagnostics.CodeAnalysis;

using LayeC.Diagnostics;
using LayeC.FrontEnd.C.Preprocess;
using LayeC.Source;

namespace LayeC.FrontEnd;

public static class FrontEndDiagnostics
{
    #region 0XXX - Miscellaneous Tooling/Internal Diagnostics

    public static void ErrorNotSupported(this CompilerContext context, SourceText source, SourceLocation location, string what) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "0001", source, location, [], $"{what} is currently not supported.");

    [DoesNotReturn]
    public static void ErrorParseUnrecoverable(this CompilerContext context, SourceText source, SourceLocation location, string? note = null)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "0002", source, location, [], "The parser decided it could not recover after an error.");
        if (note is not null) context.EmitDiagnostic(DiagnosticSemantic.Note, "0002", source, location, [], note);
        context.Diag.Emit(DiagnosticLevel.Fatal, "Terminating the compiler.");
    }

    #endregion

    #region 1XXX - Lexical Diagnostics

    public static void ErrorUnexpectedCharacter(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "1001", source, location, [], "Unexpected character.");

    public static void ErrorUnclosedComment(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "1002", source, location, [], "Unclosed comment.");

    public static void WarningExtraTokensAtEndOfDirective(this CompilerContext context, SourceText source, SourceLocation location, string directiveKind) =>
        context.EmitDiagnostic(DiagnosticSemantic.Warning, "1003", source, location, [], "Extra tokens at end of #endif directive.");

    public static void ErrorUnrecognizedEscapeSequence(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "1004", source, location, [], "Unrecognized escape sequence.");

    #endregion

    #region 2XXX - Syntactic Diagnostics

    public static void ErrorExpectedToken(this CompilerContext context, SourceText source, SourceLocation location, string tokenKindString) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2001", source, location, [], $"Expected {tokenKindString}.");

    public static void ErrorExpectedMatchingCloseDelimiter(this CompilerContext context, SourceText source, char openDelimiter, char closeDelimiter, SourceLocation closeLocation, SourceLocation openLocation)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2002", source, closeLocation, [], $"Expected a closing delimiter '{closeDelimiter}'...");
        context.EmitDiagnostic(DiagnosticSemantic.Note, "2002", source, openLocation, [], $"... to match the opening '{openDelimiter}'.");
    }

    public static void ErrorMacroNameMissing(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2003", source, location, [], "Macro name missing.");

    public static void ErrorUnterminatedFunctionLikeMacro(this CompilerContext context, SourceText source, SourceLocation location, CToken macroDefToken)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2004", source, location, [], "Unterminated function-like macro invocation.");
        context.EmitDiagnostic(DiagnosticSemantic.Note, "2004", source, macroDefToken.Location, [], $"Macro '{macroDefToken.StringValue}' defined here.");
    }

    public static void ErrorFunctionSpecifierNotAllowed(this CompilerContext context, SourceText source, SourceLocation location, StringView tokenSpelling) =>
        context.EmitDiagnostic(DiagnosticSemantic.Note, "2005", source, location, [], $"Function specifier '{tokenSpelling}' is not allowed here.");

    #endregion

    #region 3XXX - Semantic Diagnostics

    #endregion
}
