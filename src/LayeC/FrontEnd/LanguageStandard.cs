global using LanguageStandardKinds = (LayeC.FrontEnd.LanguageStandardKind Laye, LayeC.FrontEnd.LanguageStandardKind C);

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using SL = LayeC.SourceLanguage;
using LF = LayeC.FrontEnd.LanguageFeatures;

namespace LayeC.FrontEnd;

public enum LanguageStandardKind
{
    Unspecified = 0,

    C89,
    C94,
    GNU89,
    C99,
    GNU99,
    C11,
    GNU11,
    C17,
    GNU17,
    C23,
    GNU23,
    C2Y,
    GNU2Y,

    LayeIndev,
    Laye25,
    Laye25E,
}

public static class LanguageStandardKindExtensions
{
    public static SourceLanguage GetSourceLanguage(this LanguageStandardKind kind) => kind switch
    {
           LanguageStandardKind.C89
        or LanguageStandardKind.C94
        or LanguageStandardKind.GNU89
        or LanguageStandardKind.C99
        or LanguageStandardKind.GNU99
        or LanguageStandardKind.C11
        or LanguageStandardKind.GNU11
        or LanguageStandardKind.C17
        or LanguageStandardKind.GNU17
        or LanguageStandardKind.C23
        or LanguageStandardKind.GNU23
        or LanguageStandardKind.C2Y
        or LanguageStandardKind.GNU2Y
            => SL.C,

           LanguageStandardKind.LayeIndev
        or LanguageStandardKind.Laye25
        or LanguageStandardKind.Laye25E
            => SL.Laye,

        _ => SL.None,
    };
}

public enum LanguageStandardAliasKind
{
    Primary,
    Alias,
    AliasDeprecated,
}

public sealed class LanguageStandardAlias
{
    public static LanguageStandardKind GetStandardKind(string shortName)
    {
        if (_aliases.TryGetValue(shortName, out var alias))
            return alias.StandardKind;
        else return LanguageStandardKind.Unspecified;
    }

    private static LanguageStandardAlias Create(string shortName, LanguageStandardKind standardKind, LanguageStandard standard, LanguageStandardAliasKind aliasKind = LanguageStandardAliasKind.Alias)
    {
        return _aliases[shortName] = new(shortName, standardKind, standard, aliasKind);
    }

    private static readonly Dictionary<string, LanguageStandardAlias> _aliases = [];

    public static IEnumerable<LanguageStandardAlias> AllAliases => _aliases.Values;
    public static IEnumerable<LanguageStandardAlias> PrimaryAliases => _aliases.Values.Where(a => a.IsPrimaryAlias);
    public static IEnumerable<LanguageStandardAlias> PrimaryCAliases => _aliases.Values.Where(a => a.IsPrimaryAlias && a.Standard.Language == SL.C);
    public static IEnumerable<LanguageStandardAlias> PrimaryLayeAliases => _aliases.Values.Where(a => a.IsPrimaryAlias && a.Standard.Language == SL.Laye);

    #region C Language Standards

    public static readonly LanguageStandardAlias C89          = Create("c89",            LanguageStandardKind.C89,     LanguageStandard.C89,  LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias C90          = Create("c90",            LanguageStandardKind.C89,     LanguageStandard.C89);
    public static readonly LanguageStandardAlias ISO9899_1990 = Create("iso9899:1990",   LanguageStandardKind.C89,     LanguageStandard.C89);
    public static readonly LanguageStandardAlias C94          = Create("iso9899:199409", LanguageStandardKind.C94,     LanguageStandard.C94);
    public static readonly LanguageStandardAlias GNU89        = Create("gnu89",          LanguageStandardKind.GNU89,   LanguageStandard.GNU89, LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias GNU90        = Create("gnu90",          LanguageStandardKind.GNU89,   LanguageStandard.GNU89);

    public static readonly LanguageStandardAlias C99          = Create("c99",            LanguageStandardKind.C99,     LanguageStandard.C99,   LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias ISO9899_1999 = Create("iso9899:1999",   LanguageStandardKind.C99,     LanguageStandard.C99);
    public static readonly LanguageStandardAlias C9X          = Create("c9x",            LanguageStandardKind.C99,     LanguageStandard.C99,   LanguageStandardAliasKind.AliasDeprecated);
    public static readonly LanguageStandardAlias ISO9899_199X = Create("iso9899:199x",   LanguageStandardKind.C99,     LanguageStandard.C99,   LanguageStandardAliasKind.AliasDeprecated);
    public static readonly LanguageStandardAlias GNU99        = Create("gnu99",          LanguageStandardKind.GNU99,   LanguageStandard.GNU99, LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias GNU9X        = Create("gnu9x",          LanguageStandardKind.GNU99,   LanguageStandard.GNU99, LanguageStandardAliasKind.AliasDeprecated);

    public static readonly LanguageStandardAlias C11          = Create("c11",            LanguageStandardKind.C11,     LanguageStandard.C11,   LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias ISO9899_2011 = Create("iso9899:2011",   LanguageStandardKind.C11,     LanguageStandard.C11);
    public static readonly LanguageStandardAlias C1X          = Create("c1x",            LanguageStandardKind.C11,     LanguageStandard.C11,   LanguageStandardAliasKind.AliasDeprecated);
    public static readonly LanguageStandardAlias ISO9899_201X = Create("iso9899:201x",   LanguageStandardKind.C11,     LanguageStandard.C11,   LanguageStandardAliasKind.AliasDeprecated);
    public static readonly LanguageStandardAlias GNU11        = Create("gnu11",          LanguageStandardKind.GNU11,   LanguageStandard.GNU11, LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias GNU1X        = Create("gnu1x",          LanguageStandardKind.GNU11,   LanguageStandard.GNU11, LanguageStandardAliasKind.AliasDeprecated);

    public static readonly LanguageStandardAlias C17          = Create("c17",            LanguageStandardKind.C17,     LanguageStandard.C17,   LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias ISO9899_2017 = Create("iso9899:2017",   LanguageStandardKind.C17,     LanguageStandard.C17);
    public static readonly LanguageStandardAlias C18          = Create("c18",            LanguageStandardKind.C17,     LanguageStandard.C17,   LanguageStandardAliasKind.AliasDeprecated);
    public static readonly LanguageStandardAlias ISO9899_2018 = Create("iso9899:2018",   LanguageStandardKind.C17,     LanguageStandard.C17,   LanguageStandardAliasKind.AliasDeprecated);
    public static readonly LanguageStandardAlias GNU17        = Create("gnu17",          LanguageStandardKind.GNU17,   LanguageStandard.GNU17, LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias GNU18        = Create("gnu18",          LanguageStandardKind.GNU17,   LanguageStandard.GNU17, LanguageStandardAliasKind.AliasDeprecated);

    public static readonly LanguageStandardAlias C23          = Create("c23",            LanguageStandardKind.C23,     LanguageStandard.C23,   LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias C2X          = Create("c2x",            LanguageStandardKind.C23,     LanguageStandard.C23,   LanguageStandardAliasKind.AliasDeprecated);
    public static readonly LanguageStandardAlias GNU23        = Create("gnu23",          LanguageStandardKind.GNU23,   LanguageStandard.GNU23, LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias GNU2X        = Create("gnu2x",          LanguageStandardKind.GNU23,   LanguageStandard.GNU23, LanguageStandardAliasKind.AliasDeprecated);
    
    public static readonly LanguageStandardAlias C2Y          = Create("c2y",            LanguageStandardKind.C2Y,     LanguageStandard.C2Y,   LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias GNU2Y        = Create("gnu2y",          LanguageStandardKind.GNU2Y,   LanguageStandard.GNU2Y, LanguageStandardAliasKind.Primary);

    #endregion

    #region Laye Language Standards

    public static readonly LanguageStandardAlias LayeIndev    = Create("laye-indev",     LanguageStandardKind.Laye25,  LanguageStandard.LayeIndev, LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias Laye25       = Create("laye25",         LanguageStandardKind.Laye25,  LanguageStandard.Laye25,    LanguageStandardAliasKind.Primary);
    public static readonly LanguageStandardAlias Laye25E      = Create("laye25e",        LanguageStandardKind.Laye25E, LanguageStandard.Laye25E,   LanguageStandardAliasKind.Primary);

    #endregion

    public string ShortName { get; }
    public LanguageStandardKind StandardKind { get; }
    public LanguageStandard Standard { get; }
    public LanguageStandardAliasKind AliasKind { get; }

    public bool IsPrimaryAlias => AliasKind == LanguageStandardAliasKind.Primary;
    public bool IsDeprecated => AliasKind == LanguageStandardAliasKind.AliasDeprecated;

    private LanguageStandardAlias(string shortName, LanguageStandardKind standardKind, LanguageStandard standard, LanguageStandardAliasKind aliasKind)
    {
        Debug.Assert(standardKind.GetSourceLanguage() == standard.Language);
        ShortName = shortName;
        StandardKind = standardKind;
        Standard = standard;
        AliasKind = aliasKind;
    }
}

public sealed class LanguageStandard
{
    public static bool TryGetStandard(LanguageStandardKind kind, [NotNullWhen(true)] out LanguageStandard? standard)
    {
        switch (kind)
        {
            default: break;
            case LanguageStandardKind.C89: standard = C89; return true;
            case LanguageStandardKind.C94: standard = C94; return true;
            case LanguageStandardKind.GNU89: standard = GNU89; return true;
            case LanguageStandardKind.C99: standard = C99; return true;
            case LanguageStandardKind.GNU99: standard = GNU99; return true;
            case LanguageStandardKind.C11: standard = C11; return true;
            case LanguageStandardKind.GNU11: standard = GNU11; return true;
            case LanguageStandardKind.C17: standard = C17; return true;
            case LanguageStandardKind.GNU17: standard = GNU17; return true;
            case LanguageStandardKind.C23: standard = C23; return true;
            case LanguageStandardKind.GNU23: standard = GNU23; return true;
            case LanguageStandardKind.C2Y: standard = C2Y; return true;
            case LanguageStandardKind.GNU2Y: standard = GNU2Y; return true;
            case LanguageStandardKind.LayeIndev: standard = LayeIndev; return true;
            case LanguageStandardKind.Laye25: standard = Laye25; return true;
            case LanguageStandardKind.Laye25E: standard = Laye25E; return true;
        }

        standard = null;
        return false;
    }

    #region C Language Standards

    public static readonly LanguageStandard C89 = new("ISO C 1990", SL.C, LF.None);
    public static readonly LanguageStandard C94 = new("ISO C 1990 with amendment 1", SL.C, LF.CDigraphs);
    public static readonly LanguageStandard GNU89 = new("ISO C 1990 with GNU extensions", SL.C, LF.CLineComment | LF.CDigraphs | LF.GNUMode);

    public static readonly LanguageStandard C99 = new("ISO C 1999", SL.C, LF.CLineComment | LF.C99 | LF.CDigraphs | LF.CHexFloat);
    public static readonly LanguageStandard GNU99 = new("ISO C 1999 with GNU extensions", SL.C, LF.CLineComment | LF.C99 | LF.CDigraphs | LF.GNUMode | LF.CHexFloat);

    public static readonly LanguageStandard C11 = new("ISO C 2011", SL.C, LF.CLineComment | LF.C99 | LF.C11 | LF.CDigraphs | LF.CHexFloat);
    public static readonly LanguageStandard GNU11 = new("ISO C 2011 with GNU extensions", SL.C, LF.CLineComment | LF.C99 | LF.C11 | LF.CDigraphs | LF.GNUMode | LF.CHexFloat);

    public static readonly LanguageStandard C17 = new("ISO C 2017", SL.C, LF.CLineComment | LF.C99 | LF.C11 | LF.C17 | LF.CDigraphs | LF.CHexFloat);
    public static readonly LanguageStandard GNU17 = new("ISO C 2017 with GNU extensions", SL.C, LF.CLineComment | LF.C99 | LF.C11 | LF.C17 | LF.CDigraphs | LF.GNUMode | LF.CHexFloat);

    public static readonly LanguageStandard C23 = new("ISO C 2023", SL.C, LF.CLineComment | LF.C99 | LF.C11 | LF.C17 | LF.C23 | LF.CDigraphs | LF.CHexFloat);
    public static readonly LanguageStandard GNU23 = new("ISO C 2023 with GNU extensions", SL.C, LF.CLineComment | LF.C99 | LF.C11 | LF.C17 | LF.C23 | LF.CDigraphs | LF.GNUMode | LF.CHexFloat);

    public static readonly LanguageStandard C2Y = new("Working Draft for ISO C2y", SL.C, LF.CLineComment | LF.C99 | LF.C11 | LF.C17 | LF.C23 | LF.C2Y | LF.CDigraphs | LF.CHexFloat);
    public static readonly LanguageStandard GNU2Y = new("Working Draft for ISO C2y with GNU extensions", SL.C, LF.CLineComment | LF.C99 | LF.C11 | LF.C17 | LF.C23 | LF.C2Y | LF.CDigraphs | LF.GNUMode | LF.CHexFloat);

    #endregion

    #region Laye Language Standards

    public static readonly LanguageStandard LayeIndev = new("Laye In-Development", SL.Laye, LF.None);
    public static readonly LanguageStandard Laye25 = new("Laye 2025", SL.Laye, LF.Laye25);
    public static readonly LanguageStandard Laye25E = new("Laye 2025 with embedded extensions", SL.Laye, LF.Laye25 | LF.Embedded);

    #endregion

    public string Description { get; }
    public SourceLanguage Language { get; }
    public LanguageFeatures Features { get; }

    public bool HasCLineComments => Features.HasFlag(LanguageFeatures.CLineComment);

    private LanguageStandard(string description, SourceLanguage language, LanguageFeatures features)
    {
        Description = description;
        Language = language;
        Features = features;
    }

    public override string ToString() => Description;
}
