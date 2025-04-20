using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

using LayeC.Diagnostics;
using LayeC.Formatting;
using LayeC.FrontEnd;
using LayeC.Source;

namespace LayeC;

[Flags]
public enum IncludeKind
{
    System = 0,
    Local = 1 << 0,
    IncludeNext = 1 << 2,
}

public sealed class CompilerContext(IDiagnosticConsumer diagConsumer, Target target, Triple triple)
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
    public Triple Triple { get; } = triple;

    public required IncludePaths IncludePaths { get; init; }
    public List<StringView> PreprocessorDefines { get; } = [];

    private readonly Dictionary<string, SourceText> _fileCache = [];

    public Preprocessor CreatePreprocessor(LanguageOptions languageOptions, SourceText? source, PreprocessorMode mode = PreprocessorMode.Full)
    {
        var pp = new Preprocessor(this, languageOptions, mode);
        if (source is not null) pp.PushSourceTokenStream(source);

        if (PreprocessorDefines.Count > 0)
        {
            var commandLineDefinesBuilder = new StringBuilder();
            foreach (StringView ppDefine in PreprocessorDefines)
            {
                int eqIndex = ppDefine.IndexOf('=');
                if (eqIndex < 0)
                    commandLineDefinesBuilder.AppendLine($"define {ppDefine}");
                else commandLineDefinesBuilder.AppendLine($"define {ppDefine[..eqIndex]} {ppDefine[(eqIndex + 1)..]}");
            }

            var cliSource = new SourceText("<command-line>", commandLineDefinesBuilder.ToString(), SourceLanguage.C);
            pp.PushSourceTokenStream(cliSource);
        }

        var now = DateTime.Now;

        var builtInDefinesBuilder = new StringBuilder();
        builtInDefinesBuilder.AppendLine("#define __STDC__ 1");
        builtInDefinesBuilder.AppendLine("#define __STDC_HOSTED__ 1");
        builtInDefinesBuilder.AppendLine($"#define __DATE__ \"{now:MMM dd yyyy}\"");
        builtInDefinesBuilder.AppendLine($"#define __TIME__ \"{now:HH:mm:ss}\"");
        builtInDefinesBuilder.AppendLine("#define __LAYEC__ 1");

        switch (languageOptions.Standards.C)
        {
            case LanguageStandardKind.C89:
            {
            } break;
            
            case LanguageStandardKind.C94:
            {
                builtInDefinesBuilder.AppendLine("#define __STDC_VERSION__ 199409L");
            } break;
            
            case LanguageStandardKind.C99:
            {
                builtInDefinesBuilder.AppendLine("#define __STDC_VERSION__ 199901L");
            } break;
            
            case LanguageStandardKind.C11:
            {
                builtInDefinesBuilder.AppendLine("#define __STDC_VERSION__ 201112L");
            } break;
            
            case LanguageStandardKind.C17:
            {
                builtInDefinesBuilder.AppendLine("#define __STDC_VERSION__ 201710L");
            } break;
            
            case LanguageStandardKind.C23:
            {
                builtInDefinesBuilder.AppendLine("#define __STDC_VERSION__ 202311L");
            } break;

            default: break;
        }

        switch (Triple.Arch)
        {
            case Triple.ArchKind.X86:
            {
                builtInDefinesBuilder.AppendLine("#define i386");
                builtInDefinesBuilder.AppendLine("#define __i386");
                builtInDefinesBuilder.AppendLine("#define __i386__");
                builtInDefinesBuilder.AppendLine("#define _M_IX86");
                builtInDefinesBuilder.AppendLine("#define __X86__");
                builtInDefinesBuilder.AppendLine("#define _X86_");
            } break;

            case Triple.ArchKind.X86_64:
            {
                builtInDefinesBuilder.AppendLine("#define __amd64__");
                builtInDefinesBuilder.AppendLine("#define __amd64");
                builtInDefinesBuilder.AppendLine("#define __x86_64__");
                builtInDefinesBuilder.AppendLine("#define __x86_64");
                builtInDefinesBuilder.AppendLine("#define _M_X64");
                builtInDefinesBuilder.AppendLine("#define _M_AMD64");
            } break;

            case Triple.ArchKind.Wasm32:
            {
                builtInDefinesBuilder.AppendLine("#define __WASM__");
                builtInDefinesBuilder.AppendLine("#define __WASM32__");
                builtInDefinesBuilder.AppendLine("#define __wasm 1");
                builtInDefinesBuilder.AppendLine("#define __wasm__ 1");
                builtInDefinesBuilder.AppendLine("#define __wasm32 1");
                builtInDefinesBuilder.AppendLine("#define __wasm32__ 1");
            } break;

            case Triple.ArchKind.Wasm64:
            {
                builtInDefinesBuilder.AppendLine("#define __WASM__");
                builtInDefinesBuilder.AppendLine("#define __WASM64__");
                builtInDefinesBuilder.AppendLine("#define __wasm 1");
                builtInDefinesBuilder.AppendLine("#define __wasm__ 1");
                builtInDefinesBuilder.AppendLine("#define __wasm64 1");
                builtInDefinesBuilder.AppendLine("#define __wasm64__ 1");
            } break;
        }

        switch (Triple.OS)
        {
            case Triple.OSKind.Linux:
            {
                builtInDefinesBuilder.AppendLine("#define __linux__");
                builtInDefinesBuilder.AppendLine("#define __unix__");
                builtInDefinesBuilder.AppendLine("#define __unix");

                builtInDefinesBuilder.AppendLine("#define __INT64_TYPE__ long");
                builtInDefinesBuilder.AppendLine("#define __INT32_TYPE__ int");
                builtInDefinesBuilder.AppendLine("#define __INT16_TYPE__ short");
                builtInDefinesBuilder.AppendLine("#define __INT8_TYPE__ signed char");
                builtInDefinesBuilder.AppendLine("#define __SIZE_TYPE__ unsigned long");
                builtInDefinesBuilder.AppendLine("#define __PTRDIFF_TYPE__ long");
                builtInDefinesBuilder.AppendLine("#define __WCHAR_TYPE__ int");
            } break;

            case Triple.OSKind.Windows:
            {
                builtInDefinesBuilder.AppendLine("#define _WIN32");
                builtInDefinesBuilder.AppendLine("#define _WIN64");

                builtInDefinesBuilder.AppendLine("#define __INT64_TYPE__ long long");
                builtInDefinesBuilder.AppendLine("#define __INT32_TYPE__ int");
                builtInDefinesBuilder.AppendLine("#define __INT16_TYPE__ short");
                builtInDefinesBuilder.AppendLine("#define __INT8_TYPE__ signed char");
                builtInDefinesBuilder.AppendLine("#define __SIZE_TYPE__ unsigned long long");
                builtInDefinesBuilder.AppendLine("#define __PTRDIFF_TYPE__ long long");
                builtInDefinesBuilder.AppendLine("#define __WCHAR_TYPE__ unsigned short");
            } break;
        }

        var builtInSource = new SourceText("<built-in>", builtInDefinesBuilder.ToString(), SourceLanguage.C);
        pp.PushSourceTokenStream(builtInSource);

        return pp;
    }

    public SourceText? GetSourceTextForIncludeFilePath(string filePath, IncludeKind kind, string? relativeToPath = null)
    {
        bool isSystemHeader;
        bool includeNext = kind.HasFlag(IncludeKind.IncludeNext);
        if (kind.HasFlag(IncludeKind.Local))
        {
            Assert(relativeToPath is not null, "Cannot resolve a local include path if there was no relative path provided.");
            filePath = IncludePaths.ResolveIncludePath(filePath, relativeToPath, out isSystemHeader, ref includeNext);
        }
        else filePath = IncludePaths.ResolveIncludePath(filePath, out isSystemHeader, ref includeNext);

        if (!File.Exists(filePath))
            return null;

        string canonicalPath = Path.GetFullPath(filePath);
        if (_fileCache.TryGetValue(canonicalPath, out var source))
            return source;

        try
        {
            string sourceText = File.ReadAllText(filePath);
            source = _fileCache[canonicalPath] = new(filePath, sourceText, SourceLanguage.C);
            if (isSystemHeader) source.IsSystemHeader = true;
            return source;
        }
        catch
        {
            return null;
        }
    }

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

    public Diagnostic EmitDiagnostic(DiagnosticSemantic semantic, string id, string message)
    {
        return Diag.Emit(MapSemantic(semantic, id), message);
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
