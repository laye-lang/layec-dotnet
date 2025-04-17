namespace LayeC.FrontEnd.Syntax;

public enum CTypeSpecifierKind
    : long
{
    Unspecified = 0,

    Void = 1L << 0,
    Char = 1L << 1,
    Short = 1L << 2,
    Int = 1L << 3,
    Long = 1L << 4,
    LongLong = 1L << 5,
    Signed = 1L << 6,
    Unsigned = 1L << 7,
    Float = 1L << 8,
    Double = 1L << 9,
    Int128 = 1L << 10,
    BitInt = 1L << 11,
    Float16 = 1L << 12,
    BFloat16 = 1L << 13,
    Float128 = 1L << 14,
    IBM128 = 1L << 15,
    Bool = 1L << 16,
    Decimal32 = 1L << 17,
    Decimal64 = 1L << 18,
    Decimal128 = 1L << 19,
    Enum = 1L << 20,
    Union = 1L << 21,
    Struct = 1L << 22,
    Typedef = 1L << 23,
    TypeofType = 1L << 24,
    TypeofExpr = 1L << 25,
    TypeofUnqualType = 1L << 26,
    TypeofUnqualExpr = 1L << 27,
    Auto = 1L << 28,
    Atomic = 1L << 29,

    // might need more in the future and we were close to the edge so I made this a long instead of an int

    Error = 1L << 63,
}
