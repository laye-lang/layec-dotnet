using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using LayeC.Diagnostics;
using LayeC.Formatting;
using LayeC.Source;

namespace LayeC.FrontEnd;

public static class FrontEndDiagnostics
{
    private static MarkupScopedSemantic KW(string text) => new(MarkupSemantic.Keyword, text);

    #region 0XXX - Miscellaneous Tooling/Internal Diagnostics

    public static void ErrorNotSupported(this CompilerContext context, SourceText source, SourceLocation location, StringView what) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "0001", source, location, [], $"{what} is currently not supported.");

    [DoesNotReturn]
    public static void ErrorParseUnrecoverable(this CompilerContext context, SourceText source, SourceLocation location, string? note = null)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "0002", source, location, [], "The parser decided it could not recover after an error.");
        if (note is not null) context.EmitDiagnostic(DiagnosticSemantic.Note, "0002", source, location, [], note);
        context.Diag.Emit(DiagnosticLevel.Fatal, "Terminating the compiler.");
        throw new UnreachableException();
    }

    public static void ErrorCannotOpenSourceFile(this CompilerContext context, SourceText source, SourceLocation location, StringView filePath) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "0003", source, location, [], $"Cannot open source file '{filePath}'.");

    #endregion

    #region 1XXX - Extension Diagnostics

    public static void ExtVAOpt(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "1001", source, location, [], $"'{KW("__VA_OPT__")}' is a C23 extension.");

    #endregion

    #region 2XXX - Lexical Diagnostics

    public static void ErrorUnexpectedCharacter(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2001", source, location, [], "Unexpected character.");

    public static void ErrorUnclosedComment(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2002", source, location, [], "Unclosed comment.");

    public static void ErrorUnrecognizedEscapeSequence(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2003", source, location, [], "Unrecognized escape sequence.");

    public static void ErrorTooManyCharactersInCharacterLiteral(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2004", source, location, [], "Too many characters in character literal.");

    public static void ErrorUnclosedStringOrCharacterLiteral(this CompilerContext context, SourceText source, SourceLocation location, StringView literalKind) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2005", source, location, [], $"Unclosed {literalKind} literal.");

    public static void ErrorBitWidthOutOfRange(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "2006", source, location, [], "Type bit width must be in the range [1, 65536).");

    #endregion

    #region 3XXX - Preprocessing Diagnostics

    public static void WarningExtraTokensAtEndOfDirective(this CompilerContext context, Token directiveToken) =>
        context.EmitDiagnostic(DiagnosticSemantic.Warning, "3001", directiveToken.Source, directiveToken.Location, [], $"Extra tokens at end of '{directiveToken.StringValue}' directive.");

    public static void ErrorCStylePreprocessingDirective(this CompilerContext context, SourceText source, SourceLocation location)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3002", source, location, [], "C-style preprocessing directives are not allowed in Laye.");
        context.EmitDiagnostic(DiagnosticSemantic.Note, "3002", source, location, [], "Use `pragma` for a limited subset of C preprocessor directives.");
    }

    public static void ErrorExpectedPreprocessorDirective(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3003", source, location, [], "Expected a preprocessor directive.");

    public static void ErrorUnknownPreprocessorDirective(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3004", source, location, [], "Unknown or unsupported preprocessor directive.");

    public static void ErrorExpectedMacroName(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3005", source, location, [], "Expected a macro name.");

    public static void ErrorDuplicateMacroParameter(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3006", source, location, [], "Duplicate macro parameter name.");

    public static void ErrorMacroNameMissing(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3007", source, location, [], "Macro name missing.");

    public static void ErrorUnterminatedFunctionLikeMacro(this CompilerContext context, SourceText source, SourceLocation location, Token macroDefToken)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3008", source, location, [], "Unterminated function-like macro invocation.");
        context.EmitDiagnostic(DiagnosticSemantic.Note, "3008", source, macroDefToken.Location, [], $"Macro '{macroDefToken.StringValue}' defined here.");
    }

    public static void ErrorMissingEndif(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3009", source, location, [], "Missing '#endif' at end of file.");

    public static void ErrorVariadicTokenInNonVariadicMacro(this CompilerContext context, Token token) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3010", token.Source, token.Location, [], $"'{token.Spelling}' can only be used within a variadic macro.");

    public static void ErrorAdjacentConcatenationTokens(this CompilerContext context, Token token) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3011", token.Source, token.Location, [], $"'##' cannot be followed by another '##'.");

    public static void ErrorConcatenationTokenCannotStartMacro(this CompilerContext context, Token token) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3012", token.Source, token.Location, [], $"'##' cannot be the first token inside a macro.");

    public static void ErrorConcatenationTokenCannotEndMacro(this CompilerContext context, Token token) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3013", token.Source, token.Location, [], $"'##' cannot be the last token inside a macro.");

    public static void ErrorConcatenationTokenCannotStartVAOpt(this CompilerContext context, Token token) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3014", token.Source, token.Location, [], $"'##' cannot be the first token inside '__VA_OPT__'.");

    public static void ErrorConcatenationTokenCannotEndVAOpt(this CompilerContext context, Token token) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3015", token.Source, token.Location, [], $"'##' cannot be the last token inside '__VA_OPT__'.");

    public static void ErrorVAOptCannotBeNested(this CompilerContext context, Token token) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3016", token.Source, token.Location, [], $"'__VA_OPT__' cannot be nested.");

    public static void ErrorExpectedMacroParamOrVAOptAfterHash(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3017", source, location, [], $"Expected a parameter name or '__VA_OPT__' after '#'.");

    public static void WarningPotentialPreprocessorDirectiveInPragmaCExpression(this CompilerContext context, Token token)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3018", token.Source, token.Location, [], "C preprocessor directives are not processed within 'pragma \"C\" ( )' expressions.");
        context.EmitDiagnostic(DiagnosticSemantic.Note, "3018", token.Source, token.Location, [], "Did you mean `pragma \"C\" { }`?");
    }

    public static void ErrorConcatenationShouldOnlyResultInOneToken(this CompilerContext context, SourceText source, SourceLocation location) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3019", source, location, [], "Concatenation should only result in one token.");

    public static void ErrorConcatenationFormedInvalidToken(this CompilerContext context, SourceText source, SourceLocation location, StringView tokenText) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "3020", source, location, [], $"Concatenation resulted in '{tokenText}', an invalid token.");

    #endregion

    #region 4XXX - Syntactic Diagnostics

    public static void ErrorExpectedToken(this CompilerContext context, SourceText source, SourceLocation location, StringView tokenKindString) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "4001", source, location, [], $"Expected {tokenKindString}.");

    public static void ErrorExpectedMatchingCloseDelimiter(this CompilerContext context, SourceText source, char openDelimiter, char closeDelimiter, SourceLocation closeLocation, SourceLocation openLocation)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "4002", source, closeLocation, [], $"Expected a closing delimiter '{closeDelimiter}'...");
        context.EmitDiagnostic(DiagnosticSemantic.Note, "4002", source, openLocation, [], $"... to match the opening '{openDelimiter}'.");
    }

    public static void ErrorFunctionSpecifierNotAllowed(this CompilerContext context, SourceText source, SourceLocation location, StringView tokenSpelling) =>
        context.EmitDiagnostic(DiagnosticSemantic.Error, "4003", source, location, [], $"Function specifier '{tokenSpelling}' is not allowed here.");

    #endregion

    #region 5XXX - Semantic Diagnostics

    #endregion

#if false

    public static void ErrorInvalidCTokenDidYouMean(this CompilerContext context, SourceText source, SourceLocation location, StringView tokenText, string? maybeText = null)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "1005", source, location, [], $"'{tokenText}' is not a valid C token.");
        if (maybeText is not null)
            context.EmitDiagnostic(DiagnosticSemantic.Note, "1005", source, location, [], $"Did you mean '{maybeText}'?");
    }

    public static void ErrorInvalidLayeTokenDidYouMean(this CompilerContext context, SourceText source, SourceLocation location, StringView tokenText, string? maybeText = null)
    {
        context.EmitDiagnostic(DiagnosticSemantic.Error, "1006", source, location, [], $"'{tokenText}' is not a valid Laye token.");
        if (maybeText is not null)
            context.EmitDiagnostic(DiagnosticSemantic.Note, "1006", source, location, [], $"Did you mean '{maybeText}'?");
    }

#endif
}
