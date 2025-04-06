namespace LayeC.FrontEnd;

[Flags]
public enum CTypeSpecifier
{
    None = 0,

    Void = 1 << 0,
    Bool = 1 << 1,
    Char = 1 << 2,
    Short = 1 << 3,
    Int = 1 << 4,
    Long = 1 << 5,
    Long2 = 1 << 6,
    Float = 1 << 7,
    Double = 1 << 8,
    Signed = 1 << 9,
    Unsigned = 1 << 10,
    Complex = 1 << 11,

    LongLong = Long | Long2,
}
