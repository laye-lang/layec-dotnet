using System.Globalization;

namespace LayeC.FrontEnd;

public readonly struct KeywordStandardInfo(LanguageFeatures features, TokenKind keywordKind)
{
    public readonly LanguageFeatures Features = features;
    public readonly TokenKind KeywordKind = keywordKind;
}

public static class SyntaxFacts
{
    private static readonly Dictionary<StringView, KeywordStandardInfo> _cKeywordInfos = new()
    {
        { "alignas", new(LanguageFeatures.C23, TokenKind.KWAlignas) },
        { "_Alignas", new(LanguageFeatures.C11, TokenKind.KW_Alignas) },
        { "alignof", new(LanguageFeatures.C23, TokenKind.KWAlignof) },
        { "_Alignof", new(LanguageFeatures.C11, TokenKind.KWAlignof) },
        { "_Atomic", new(LanguageFeatures.C11, TokenKind.KW_Atomic) },
        { "__attribute__", new(LanguageFeatures.GNUMode, TokenKind.KW__Attribute__) },
        { "auto", new(LanguageFeatures.None, TokenKind.KWAuto) },
        { "_BitInt", new(LanguageFeatures.C23, TokenKind.KW_BitInt) },
        { "bool", new(LanguageFeatures.C23, TokenKind.KWBool) },
        { "_Bool", new(LanguageFeatures.C99, TokenKind.KWBool) },
        { "break", new(LanguageFeatures.None, TokenKind.KWBreak) },
        { "case", new(LanguageFeatures.None, TokenKind.KWCase) },
        { "char", new(LanguageFeatures.None, TokenKind.KWChar) },
        { "_Complex", new(LanguageFeatures.C99, TokenKind.KW_Complex) },
        { "const", new(LanguageFeatures.None, TokenKind.KWConst) },
        { "constexpr", new(LanguageFeatures.C23, TokenKind.KWConstexpr) },
        { "continue", new(LanguageFeatures.None, TokenKind.KWContinue) },
        { "_Decimal128", new(LanguageFeatures.C23, TokenKind.KW_Decimal128) },
        { "_Decimal32", new(LanguageFeatures.C23, TokenKind.KW_Decimal32) },
        { "_Decimal64", new(LanguageFeatures.C23, TokenKind.KW_Decimal64) },
        { "default", new(LanguageFeatures.None, TokenKind.KWDefault) },
        { "do", new(LanguageFeatures.None, TokenKind.KWDo) },
        { "double", new(LanguageFeatures.None, TokenKind.KWDouble) },
        { "else", new(LanguageFeatures.None, TokenKind.KWElse) },
        { "enum", new(LanguageFeatures.None, TokenKind.KWEnum) },
        { "extern", new(LanguageFeatures.None, TokenKind.KWExtern) },
        { "false", new(LanguageFeatures.C23, TokenKind.KWFalse) },
        { "float", new(LanguageFeatures.None, TokenKind.KWFloat) },
        { "for", new(LanguageFeatures.None, TokenKind.KWFor) },
        { "_Generic", new(LanguageFeatures.C11, TokenKind.KW_Generic) },
        { "goto", new(LanguageFeatures.None, TokenKind.KWGoto) },
        { "if", new(LanguageFeatures.None, TokenKind.KWIf) },
        { "_Imaginary", new(LanguageFeatures.C99, TokenKind.KW_Imaginary) },
        { "inline", new(LanguageFeatures.C99, TokenKind.KWInline) },
        { "int", new(LanguageFeatures.None, TokenKind.KWInt) },
        { "long", new(LanguageFeatures.None, TokenKind.KWLong) },
        { "nullptr", new(LanguageFeatures.C23, TokenKind.KWNullptr) },
        { "_Noreturn", new(LanguageFeatures.C11, TokenKind.KWNoreturn) },
        { "register", new(LanguageFeatures.None, TokenKind.KWRegister) },
        { "restrict", new(LanguageFeatures.C99, TokenKind.KWRestrict) },
        { "return", new(LanguageFeatures.None, TokenKind.KWReturn) },
        { "short", new(LanguageFeatures.None, TokenKind.KWShort) },
        { "signed", new(LanguageFeatures.None, TokenKind.KWSigned) },
        { "sizeof", new(LanguageFeatures.None, TokenKind.KWSizeof) },
        { "static", new(LanguageFeatures.None, TokenKind.KWStatic) },
        { "static_assert", new(LanguageFeatures.C23, TokenKind.KWStatic_Assert) },
        { "_Static_assert", new(LanguageFeatures.C11, TokenKind.KWStatic_Assert) },
        { "struct", new(LanguageFeatures.None, TokenKind.KWStruct) },
        { "switch", new(LanguageFeatures.None, TokenKind.KWSwitch) },
        { "__thread", new(LanguageFeatures.C23, TokenKind.KW__Thread) },
        { "thread_local", new(LanguageFeatures.C23, TokenKind.KWThread_Local) },
        { "_Thread_local", new(LanguageFeatures.C11, TokenKind.KW_Thread_Local) },
        { "true", new(LanguageFeatures.C23, TokenKind.KWTrue) },
        { "typedef", new(LanguageFeatures.None, TokenKind.KWTypedef) },
        { "typeof", new(LanguageFeatures.C23, TokenKind.KWTypeof) },
        { "typeof_unqual", new(LanguageFeatures.C23, TokenKind.KWTypeof_Unqual) },
        { "union", new(LanguageFeatures.None, TokenKind.KWUnion) },
        { "unsigned", new(LanguageFeatures.None, TokenKind.KWUnsigned) },
        { "void", new(LanguageFeatures.None, TokenKind.KWVoid) },
        { "volatile", new(LanguageFeatures.None, TokenKind.KWVolatile) },
        { "while", new(LanguageFeatures.None, TokenKind.KWWhile) },
    };

    private static readonly Dictionary<StringView, KeywordStandardInfo> _layeKeywordInfos = new()
    {
        { "alias", new(LanguageFeatures.None, TokenKind.KWAlias) },
        { "alignof", new(LanguageFeatures.None, TokenKind.KWAlignof) },
        { "and", new(LanguageFeatures.None, TokenKind.KWAnd) },
        { "as", new(LanguageFeatures.None, TokenKind.KWAs) },
        { "assert", new(LanguageFeatures.None, TokenKind.KWAssert) },
        { "bool", new(LanguageFeatures.None, TokenKind.KWBool) },
        { "break", new(LanguageFeatures.None, TokenKind.KWBreak) },
        { "__builtin_ffi_bool", new(LanguageFeatures.None, TokenKind.KWBuiltinFFIBool) },
        { "__builtin_ffi_char", new(LanguageFeatures.None, TokenKind.KWBuiltinFFIChar) },
        { "__builtin_ffi_short", new(LanguageFeatures.None, TokenKind.KWBuiltinFFIShort) },
        { "__builtin_ffi_int", new(LanguageFeatures.None, TokenKind.KWBuiltinFFIInt) },
        { "__builtin_ffi_long", new(LanguageFeatures.None, TokenKind.KWBuiltinFFILong) },
        { "__builtin_ffi_longlong", new(LanguageFeatures.None, TokenKind.KWBuiltinFFILongLong) },
        { "__builtin_ffi_float", new(LanguageFeatures.None, TokenKind.KWBuiltinFFIFloat) },
        { "__builtin_ffi_double", new(LanguageFeatures.None, TokenKind.KWBuiltinFFIDouble) },
        { "__builtin_ffi_longdouble", new(LanguageFeatures.None, TokenKind.KWBuiltinFFILongDouble) },
        { "callconv", new(LanguageFeatures.None, TokenKind.KWCallconv) },
        { "case", new(LanguageFeatures.None, TokenKind.KWCase) },
        { "cast", new(LanguageFeatures.None, TokenKind.KWCast) },
        { "const", new(LanguageFeatures.None, TokenKind.KWConst) },
        { "continue", new(LanguageFeatures.None, TokenKind.KWContinue) },
        { "countof", new(LanguageFeatures.None, TokenKind.KWCountof) },
        { "default", new(LanguageFeatures.None, TokenKind.KWDefault) },
        { "defer", new(LanguageFeatures.None, TokenKind.KWDefer) },
        { "delegate", new(LanguageFeatures.None, TokenKind.KWDelegate) },
        { "delete", new(LanguageFeatures.None, TokenKind.KWDelete) },
        { "discard", new(LanguageFeatures.None, TokenKind.KWDiscard) },
        { "discardable", new(LanguageFeatures.None, TokenKind.KWDiscardable) },
        { "do", new(LanguageFeatures.None, TokenKind.KWDo) },
        { "double", new(LanguageFeatures.None, TokenKind.KWDouble) },
        { "else", new(LanguageFeatures.None, TokenKind.KWElse) },
        { "enum", new(LanguageFeatures.None, TokenKind.KWEnum) },
        { "eval", new(LanguageFeatures.None, TokenKind.KWEval) },
        { "export", new(LanguageFeatures.None, TokenKind.KWExport) },
        { "false", new(LanguageFeatures.None, TokenKind.KWFalse) },
        { "fallthrough", new(LanguageFeatures.None, TokenKind.KWFallthrough) },
        { "float32", new(LanguageFeatures.None, TokenKind.KWFloat32) },
        { "float64", new(LanguageFeatures.None, TokenKind.KWFloat64) },
        { "for", new(LanguageFeatures.None, TokenKind.KWFor) },
        { "foreign", new(LanguageFeatures.None, TokenKind.KWForeign) },
        { "from", new(LanguageFeatures.None, TokenKind.KWFrom) },
        { "global", new(LanguageFeatures.None, TokenKind.KWGlobal) },
        { "goto", new(LanguageFeatures.None, TokenKind.KWGoto) },
        { "if", new(LanguageFeatures.None, TokenKind.KWIf) },
        { "import", new(LanguageFeatures.None, TokenKind.KWImport) },
        { "inline", new(LanguageFeatures.None, TokenKind.KWInline) },
        { "int", new(LanguageFeatures.None, TokenKind.KWInt) },
        { "is", new(LanguageFeatures.None, TokenKind.KWIs) },
        { "long", new(LanguageFeatures.None, TokenKind.KWLong) },
        { "module", new(LanguageFeatures.None, TokenKind.KWModule) },
        { "mut", new(LanguageFeatures.None, TokenKind.KWMut) },
        { "new", new(LanguageFeatures.None, TokenKind.KWNew) },
        { "nil", new(LanguageFeatures.None, TokenKind.KWNil) },
        { "noreturn", new(LanguageFeatures.None, TokenKind.KWNoreturn) },
        { "not", new(LanguageFeatures.None, TokenKind.KWNot) },
        { "offsetof", new(LanguageFeatures.None, TokenKind.KWOffsetof) },
        { "operator", new(LanguageFeatures.None, TokenKind.KWOperator) },
        { "or", new(LanguageFeatures.None, TokenKind.KWOr) },
        { "pragma", new(LanguageFeatures.None, TokenKind.KWPragma) },
        { "rankof", new(LanguageFeatures.None, TokenKind.KWRankof) },
        { "ref", new(LanguageFeatures.None, TokenKind.KWRef) },
        { "register", new(LanguageFeatures.None, TokenKind.KWRegister) },
        { "return", new(LanguageFeatures.None, TokenKind.KWReturn) },
        { "sizeof", new(LanguageFeatures.None, TokenKind.KWSizeof) },
        { "static", new(LanguageFeatures.None, TokenKind.KWStatic) },
        { "static_assert", new(LanguageFeatures.None, TokenKind.KWStatic_Assert) },
        { "strict", new(LanguageFeatures.None, TokenKind.KWStrict) },
        { "struct", new(LanguageFeatures.None, TokenKind.KWStruct) },
        { "switch", new(LanguageFeatures.None, TokenKind.KWSwitch) },
        { "template", new(LanguageFeatures.None, TokenKind.KWTemplate) },
        { "test", new(LanguageFeatures.None, TokenKind.KWTest) },
        { "true", new(LanguageFeatures.None, TokenKind.KWTrue) },
        { "typeof", new(LanguageFeatures.None, TokenKind.KWTypeof) },
        { "typeof_unqual", new(LanguageFeatures.None, TokenKind.KWTypeof_Unqual) },
        { "unreachable", new(LanguageFeatures.None, TokenKind.KWUnreachable) },
        { "var", new(LanguageFeatures.None, TokenKind.KWVar) },
        { "varargs", new(LanguageFeatures.None, TokenKind.KWVarargs) },
        { "variant", new(LanguageFeatures.None, TokenKind.KWVariant) },
        { "void", new(LanguageFeatures.None, TokenKind.KWVoid) },
        { "while", new(LanguageFeatures.None, TokenKind.KWWhile) },
        { "xor", new(LanguageFeatures.None, TokenKind.KWXor) },
        { "xyzzy", new(LanguageFeatures.None, TokenKind.KWXyzzy) },
        { "yield", new(LanguageFeatures.None, TokenKind.KWYield) },
    };

    public static bool TryGetCKeywordInfo(StringView identifierText, out KeywordStandardInfo info) =>
        _cKeywordInfos.TryGetValue(identifierText, out info);

    public static bool TryGetLayeKeywordInfo(StringView identifierText, out KeywordStandardInfo info) =>
        _layeKeywordInfos.TryGetValue(identifierText, out info);

    public static bool IsAsciiIdentifier(StringView ident)
    {
        if (ident.Length == 0)
            return false;

        if (ident[0] is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or '$'))
            return false;

        foreach (char c in ident[..1])
        {
            if (c is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '$'))
                return false;
        }

        return true;
    }

    public static bool IsIdentifierStart(SourceLanguage language, char c) => language switch
    {
        SourceLanguage.C => IsCIdentifierStart(c),
        SourceLanguage.Laye => IsLayeIdentifierStart(c),
        _ => false,
    };

    public static bool IsIdentifierContinue(SourceLanguage language, char c) => language switch
    {
        SourceLanguage.C => IsCIdentifierContinue(c),
        SourceLanguage.Laye => IsLayeIdentifierContinue(c),
        _ => false,
    };

    public static bool IsCIdentifierStart(char c)
    {
        if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or '$')
            return true;

        return false;
    }

    public static bool IsCIdentifierContinue(char c)
    {
        if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '$')
            return true;

        return false;
    }

    public static bool IsLayeIdentifierStart(char c)
    {
        if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or '$')
            return true;

        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter;
    }

    public static bool IsLayeIdentifierContinue(char c)
    {
        if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '$')
            return true;

        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.LetterNumber
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.Format;
    }
}
