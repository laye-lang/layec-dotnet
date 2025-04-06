using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using LayeC.Diagnostics;
using LayeC.Formatting;
using LayeC.FrontEnd;
using LayeC.Source;

namespace LayeC;

public sealed class CompilerContext(IDiagnosticConsumer diagConsumer, Target target)
{
    private readonly Dictionary<DiagnosticSemantic, DiagnosticLevel> _semanticLevels = new()
    {
        { DiagnosticSemantic.Note, DiagnosticLevel.Note },
        { DiagnosticSemantic.Remark, DiagnosticLevel.Remark },
        { DiagnosticSemantic.Warning, DiagnosticLevel.Warning },
        { DiagnosticSemantic.Extension, DiagnosticLevel.Ignore },
        { DiagnosticSemantic.ExtensionWarning, DiagnosticLevel.Warning },
        { DiagnosticSemantic.Error, DiagnosticLevel.Error },
    };

    public PedanticMode PedanticMode { get; set; } = PedanticMode.Normal;

    public DiagnosticEngine Diag { get; } = new(diagConsumer);
    public Target Target { get; } = target;

#pragma warning disable IDE0060 // Remove unused parameter
    private DiagnosticLevel MapSemantic(DiagnosticSemantic semantic, string id)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (semantic is DiagnosticSemantic.Extension)
        {
            if (PedanticMode is PedanticMode.Warning)
                semantic = DiagnosticSemantic.Warning;
            else if (PedanticMode is PedanticMode.Error)
                semantic = DiagnosticSemantic.Error;
        }
        else if (semantic is DiagnosticSemantic.ExtensionWarning)
        {
            if (PedanticMode is PedanticMode.Error)
                semantic = DiagnosticSemantic.Error;
        }

        return _semanticLevels[semantic];
    }

    public Diagnostic EmitDiagnostic(DiagnosticSemantic semantic, string id, SourceText source,
        SourceLocation location, SourceRange[] ranges, string message)
    {
        return Diag.Emit(MapSemantic(semantic, id), id, source, location, ranges, message);
    }

    public Diagnostic EmitDiagnostic(DiagnosticSemantic semantic, string id, SourceText source,
        SourceLocation location, SourceRange[] ranges, Markup message)
    {
        return Diag.Emit(MapSemantic(semantic, id), id, source, location, ranges, message);
    }

    public Diagnostic EmitDiagnostic(DiagnosticSemantic semantic, string id, SourceText source,
        SourceLocation location, SourceRange[] ranges, MarkupInterpolatedStringHandler message)
    {
        return Diag.Emit(MapSemantic(semantic, id), id, source, location, ranges, message.Markup);
    }

    public void Assert([DoesNotReturnIf(false)] bool condition, string message,
        [CallerArgumentExpression(nameof(condition))] string conditionExpressionText = "")
    {
        if (condition) return;
        Diag.Emit(DiagnosticLevel.Fatal, $"Assertion failed: {message}\nCondition: {conditionExpressionText}");
        throw new UnreachableException();
    }

    public void Assert([DoesNotReturnIf(false)] bool condition, SourceText source, SourceLocation location, string message,
        [CallerArgumentExpression(nameof(condition))] string conditionExpressionText = "")
    {
        if (condition) return;
        Diag.Emit(DiagnosticLevel.Fatal, source, location, $"Assertion failed: {message}\nCondition: {conditionExpressionText}");
        throw new UnreachableException();
    }

    public void Assert([DoesNotReturnIf(false)] bool condition, Markup message,
        [CallerArgumentExpression(nameof(condition))] string conditionExpressionText = "")
    {
        if (condition) return;
        Diag.Emit(DiagnosticLevel.Fatal, new MarkupSequence(["Assertion failed: ", message, $"\nCondition: {conditionExpressionText}"]));
        throw new UnreachableException();
    }

    public void Assert([DoesNotReturnIf(false)] bool condition, SourceText source, SourceLocation location, Markup message,
        [CallerArgumentExpression(nameof(condition))] string conditionExpressionText = "")
    {
        if (condition) return;
        Diag.Emit(DiagnosticLevel.Fatal, source, location, new MarkupSequence(["Assertion failed: ", message, $"\nCondition: {conditionExpressionText}"]));
        throw new UnreachableException();
    }

    public void Assert([DoesNotReturnIf(false)] bool condition, MarkupInterpolatedStringHandler message,
        [CallerArgumentExpression(nameof(condition))] string conditionExpressionText = "")
    {
        if (condition) return;
        Diag.Emit(DiagnosticLevel.Fatal, new MarkupSequence(["Assertion failed: ", message.Markup, $"\nCondition: {conditionExpressionText}"]));
        throw new UnreachableException();
    }

    public void Assert([DoesNotReturnIf(false)] bool condition, SourceText source, SourceLocation location, MarkupInterpolatedStringHandler message,
        [CallerArgumentExpression(nameof(condition))] string conditionExpressionText = "")
    {
        if (condition) return;
        Diag.Emit(DiagnosticLevel.Fatal, source, location, new MarkupSequence(["Assertion failed: ", message.Markup, $"\nCondition: {conditionExpressionText}"]));
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Todo(string message)
    {
        Diag.Emit(DiagnosticLevel.Fatal, $"TODO: {message}");
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Todo(SourceText source, SourceLocation location, string message)
    {
        Diag.Emit(DiagnosticLevel.Fatal, source, location, $"TODO: {message}");
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Todo(Markup message)
    {
        Diag.Emit(DiagnosticLevel.Fatal, new MarkupSequence(["TODO: ", message]));
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Todo(SourceText source, SourceLocation location, Markup message)
    {
        Diag.Emit(DiagnosticLevel.Fatal, source, location, new MarkupSequence(["TODO: ", message]));
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Todo(MarkupInterpolatedStringHandler message)
    {
        Diag.Emit(DiagnosticLevel.Fatal, new MarkupSequence(["TODO: ", message.Markup]));
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Todo(SourceText source, SourceLocation location, MarkupInterpolatedStringHandler message)
    {
        Diag.Emit(DiagnosticLevel.Fatal, source, location, new MarkupSequence(["TODO: ", message.Markup]));
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Unreachable([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        Diag.Emit(DiagnosticLevel.Fatal, $"Reached unreachable code on line {callerLineNumber} in \"{callerFilePath}\".");
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Unreachable(string message)
    {
        Diag.Emit(DiagnosticLevel.Fatal, message);
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Unreachable(Markup message)
    {
        Diag.Emit(DiagnosticLevel.Fatal, message);
        throw new UnreachableException();
    }

    [DoesNotReturn]
    public void Unreachable(MarkupInterpolatedStringHandler message)
    {
        Diag.Emit(DiagnosticLevel.Fatal, message.Markup);
        throw new UnreachableException();
    }
}
