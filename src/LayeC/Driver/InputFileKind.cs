namespace LayeC.Driver;

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
            return LayeC.SourceLanguage.Laye;
        else if (kind is InputFileKind.CSource or InputFileKind.CSourceNoPP or InputFileKind.CHeader or InputFileKind.CHeaderNoPP)
            return LayeC.SourceLanguage.C;
        else return LayeC.SourceLanguage.None;
    }

    public static bool CanPreprocess(this InputFileKind kind)
    {
        return kind is InputFileKind.LayeSource or InputFileKind.CSource or InputFileKind.CHeader;
    }
}
