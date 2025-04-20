namespace Canal;

public enum CanalDirective
{
    CheckAny,
    CheckNext,
    CheckNotSame,
    CheckNotAny,
    CheckNotNext,
    RegexCheckAny,
    RegexCheckNext,
    RegexCheckNotSame,
    RegexCheckNotAny,
    RegexCheckNotNext,
    Begin,
    Define,
    ExpandDefine,
    Undefine,
    Pragma,
    Prefix,
    Run,
    Verify,
    ExpectFail,
}
