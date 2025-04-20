namespace Choir.FrontEnd;

public enum BuiltInTypeKind
{
    Unknown = 0,

    Void,
    NoReturn,

    Bool,
    BoolSized,

    Int,
    IntSized,

    Float32,
    Float64,

    CChar,
    CUChar,
    CSChar,
    CShort,
    CUShort,
    CInt,
    CUInt,
    CLong,
    CULong,
    CLongLong,
    CULongLong,

    CFloat,
    CDouble,
    CLongDouble,

    // TODO(local): a few more C types need to go here
}
