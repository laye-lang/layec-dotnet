namespace LayeC.FrontEnd;

[Flags]
public enum LanguageFeatures
    : long
{
    None,

    CLineComment = 1L << 0,
    C99 = 1L << 1,
    C11 = 1L << 2,
    C17 = 1L << 3,
    C23 = 1L << 4,
    C2Y = 1L << 5,

    CDigraphs = 1L << 16,
    CHexFloat = 1L << 17,

    GNUMode = 1L << 24,
    ClangMode = 1L << 25,
    MSVCMode = 1L << 26,

    Laye25 = 1L << 32,

    Embedded = 1L << 40,
}
