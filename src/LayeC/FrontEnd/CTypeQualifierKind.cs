namespace LayeC.FrontEnd;

[Flags]
public enum CTypeQualifierKind
{
    None = 0,

    Const = 1 << 0,
    Restrict = 1 << 1,
    Volatile = 1 << 2,
    Atomic = 1 << 3,
}
