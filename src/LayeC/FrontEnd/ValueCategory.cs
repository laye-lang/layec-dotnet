using System.Diagnostics;

namespace LayeC.FrontEnd;

public enum ValueCategory
{
    RValue,
    LValue,
    Register,
    Reference,
}

public static class ValueCategoryExtensions
{
    public static string ToHumanString(this ValueCategory vc, bool includeArticle = false)
    {
        return vc switch
        {
            ValueCategory.RValue => includeArticle ? "an r-value" : "r-value",
            ValueCategory.LValue => includeArticle ? "an l-value" : "l-value",
            ValueCategory.Register => includeArticle ? "a register" : "register",
            ValueCategory.Reference => includeArticle ? "a reference" : "reference",
            _ => throw new UnreachableException(),
        };
    }
}
