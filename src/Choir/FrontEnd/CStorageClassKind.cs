namespace Choir.FrontEnd;

[Flags]
public enum CStorageClassKind
{
    None = 0,

    Typedef = 1 << 0,
    Extern = 1 << 1,
    Static = 1 << 2,
    Auto = 1 << 3,
    Register = 1 << 4,
    ThreadLocal = 1 << 5,
}
