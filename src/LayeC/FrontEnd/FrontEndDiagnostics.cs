using LayeC.Diagnostics;
using LayeC.FrontEnd.C.Preprocess;
using LayeC.Source;

namespace LayeC.FrontEnd;

public static class FrontEndDiagnostics
{
    #region 0XXX - Miscellaneous Tooling/Internal Diagnostics

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

    #endregion

    #region 3XXX - Semantic Diagnostics

    #endregion
}
