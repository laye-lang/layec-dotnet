namespace LayeC.Driver;

[Flags]
public enum InputFileKind
{
    Unknown = 0,

    LayeSource = 1 << 0,
    LayeModule = 1 << 1,
    CSource = 1 << 2,
}
