namespace Choir.Driver;

public enum InputFileKind
{
    Unknown = 0,

    LayeSource,
    LayeSourceNoPP,
    CSource,
    CSourceNoPP,
    CHeader,
    CHeaderNoPP,

    //Assembler,
    //AssemblerPP,
    //QBE,
    //LLVM,

    LayeModule,
    Object,
}

public static class InputFileKindExtensions
{
    public static SourceLanguage SourceLanguage(this InputFileKind kind)
    {
        if (kind is InputFileKind.LayeSource or InputFileKind.LayeSourceNoPP)
            return Choir.SourceLanguage.Laye;
        else if (kind is InputFileKind.CSource or InputFileKind.CSourceNoPP or InputFileKind.CHeader or InputFileKind.CHeaderNoPP)
            return Choir.SourceLanguage.C;
        else return Choir.SourceLanguage.None;
    }

    public static bool CanPreprocess(this InputFileKind kind)
    {
        return kind is InputFileKind.LayeSource or InputFileKind.CSource or InputFileKind.CHeader;
    }
}
