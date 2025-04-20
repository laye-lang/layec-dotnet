namespace Choir.FrontEnd;

[Flags]
public enum CFunctionSpecifier
{
    None = 0,

    Inline = 1 << 0,
    NoReturn = 1 << 1,
}
