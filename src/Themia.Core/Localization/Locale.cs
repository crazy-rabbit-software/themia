using Themia.Core.Model;

namespace Themia.Core.Localization;

/// <summary>
/// [S-1100] A language the app's text actually comes in: what <see cref="AppLanguage"/> resolves to
/// once "follow Windows" has been decided. Every string table and formatting table is keyed by it.
/// </summary>
public enum Locale
{
    En = 0,
    De = 1,
    Es = 2,
    Fr = 3,
    It = 4,
    Ja = 5,
    Pl = 6,
    Pt = 7,
    Zh = 8,
    Hi = 9,
    Ar = 10,
    Bn = 11,
    Ru = 12,
    Id = 13,
    Ur = 14,
    Mr = 15,
    Vi = 16,
    Ko = 17,
    Nl = 18,
    Tr = 19,

    /// <summary>Traditional Chinese (Taiwan). <see cref="Zh"/> is Simplified.</summary>
    ZhHant = 20,
}

/// <summary>[S-1108] Turning the language setting (and Windows' language list) into a <see cref="Locale"/>.</summary>
public static class LanguageResolver
{
    /// <summary>Every locale, in enum order.</summary>
    public static IReadOnlyList<Locale> All { get; } =
        [Locale.En, Locale.De, Locale.Es, Locale.Fr, Locale.It, Locale.Ja, Locale.Pl, Locale.Pt, Locale.Zh,
         Locale.Hi, Locale.Ar, Locale.Bn, Locale.Ru, Locale.Id, Locale.Ur, Locale.Mr, Locale.Vi,
         Locale.Ko, Locale.Nl, Locale.Tr, Locale.ZhHant];

    /// <summary>
    /// The language's code, which is also the locale file's name (<c>de</c> → de.json): two
    /// letters, except Traditional Chinese, which is told apart by its script (<c>zh-Hant</c>).
    /// <c>pt</c> is Brazilian Portuguese and <c>zh</c> Simplified Chinese (zh-Hans); each language
    /// has one file, so the codes stay short.
    /// </summary>
    public static string Code(Locale locale) => locale switch
    {
        Locale.De => "de",
        Locale.Es => "es",
        Locale.Fr => "fr",
        Locale.It => "it",
        Locale.Ja => "ja",
        Locale.Pl => "pl",
        Locale.Pt => "pt",
        Locale.Zh => "zh",
        Locale.Hi => "hi",
        Locale.Ar => "ar",
        Locale.Bn => "bn",
        Locale.Ru => "ru",
        Locale.Id => "id",
        Locale.Ur => "ur",
        Locale.Mr => "mr",
        Locale.Vi => "vi",
        Locale.Ko => "ko",
        Locale.Nl => "nl",
        Locale.Tr => "tr",
        Locale.ZhHant => "zh-Hant",
        _ => "en",
    };

    /// <summary>
    /// The locale to show: the chosen language, or for <see cref="AppLanguage.System"/> the first of
    /// Windows' preferred UI languages that Themia has, else English.
    /// <para>
    /// Walks the whole list rather than only its head (the original read one locale): someone whose
    /// Windows runs in a language Themia lacks, with German as their second choice, gets German —
    /// the same fallback Windows itself applies.
    /// </para>
    /// </summary>
    public static Locale Resolve(AppLanguage language, IEnumerable<string> preferredUiLanguages)
    {
        ArgumentNullException.ThrowIfNull(preferredUiLanguages);

        return language switch
        {
            AppLanguage.English => Locale.En,
            AppLanguage.German => Locale.De,
            AppLanguage.Spanish => Locale.Es,
            AppLanguage.French => Locale.Fr,
            AppLanguage.Italian => Locale.It,
            AppLanguage.Japanese => Locale.Ja,
            AppLanguage.Polish => Locale.Pl,
            AppLanguage.Portuguese => Locale.Pt,
            AppLanguage.Chinese => Locale.Zh,
            AppLanguage.Hindi => Locale.Hi,
            AppLanguage.Arabic => Locale.Ar,
            AppLanguage.Bengali => Locale.Bn,
            AppLanguage.Russian => Locale.Ru,
            AppLanguage.Indonesian => Locale.Id,
            AppLanguage.Urdu => Locale.Ur,
            AppLanguage.Marathi => Locale.Mr,
            AppLanguage.Vietnamese => Locale.Vi,
            AppLanguage.Korean => Locale.Ko,
            AppLanguage.Dutch => Locale.Nl,
            AppLanguage.Turkish => Locale.Tr,
            AppLanguage.ChineseTraditional => Locale.ZhHant,
            _ => FromSystem(preferredUiLanguages),
        };
    }

    /// <summary>The explicit language setting that resolves to <paramref name="locale"/> (never <see cref="AppLanguage.System"/>).</summary>
    public static AppLanguage Setting(Locale locale) => locale switch
    {
        Locale.De => AppLanguage.German,
        Locale.Es => AppLanguage.Spanish,
        Locale.Fr => AppLanguage.French,
        Locale.It => AppLanguage.Italian,
        Locale.Ja => AppLanguage.Japanese,
        Locale.Pl => AppLanguage.Polish,
        Locale.Pt => AppLanguage.Portuguese,
        Locale.Zh => AppLanguage.Chinese,
        Locale.Hi => AppLanguage.Hindi,
        Locale.Ar => AppLanguage.Arabic,
        Locale.Bn => AppLanguage.Bengali,
        Locale.Ru => AppLanguage.Russian,
        Locale.Id => AppLanguage.Indonesian,
        Locale.Ur => AppLanguage.Urdu,
        Locale.Mr => AppLanguage.Marathi,
        Locale.Vi => AppLanguage.Vietnamese,
        Locale.Ko => AppLanguage.Korean,
        Locale.Nl => AppLanguage.Dutch,
        Locale.Tr => AppLanguage.Turkish,
        Locale.ZhHant => AppLanguage.ChineseTraditional,
        _ => AppLanguage.English,
    };

    /// <summary>The first tag in the list that names a language Themia has; English when none does.</summary>
    public static Locale FromSystem(IEnumerable<string> preferredUiLanguages)
    {
        ArgumentNullException.ThrowIfNull(preferredUiLanguages);

        foreach (var tag in preferredUiLanguages)
        {
            if (FromTag(tag) is { } locale)
            {
                return locale;
            }
        }

        return Locale.En;
    }

    /// <summary>
    /// A BCP-47 tag (<c>zh-CN</c>, <c>zh-Hant-TW</c>, <c>pt_BR</c>, <c>DE</c>) to a locale, by its
    /// primary language subtag; null for a language Themia does not have. Chinese is told apart by
    /// its script, as Windows and Karpo do: Traditional for <c>zh-Hant</c> and for <c>zh-TW</c>,
    /// <c>zh-HK</c> and <c>zh-MO</c> without a script, Simplified for the rest (<c>zh-CN</c>,
    /// <c>zh-SG</c>, <c>zh-Hans-HK</c>). Every Portuguese tag is the Brazilian table.
    /// </summary>
    public static Locale? FromTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var parts = tag.Trim().Split(['-', '_']);
        var primary = parts[0];

        if (primary.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return IsTraditionalChinese(parts) ? Locale.ZhHant : Locale.Zh;
        }

        foreach (var locale in All)
        {
            if (locale != Locale.ZhHant && primary.Equals(Code(locale), StringComparison.OrdinalIgnoreCase))
            {
                return locale;
            }
        }

        return null;
    }

    private static bool IsTraditionalChinese(string[] parts)
    {
        var script = parts.Skip(1).FirstOrDefault(p => p.Length == 4);
        if (script is not null)
        {
            return script.Equals("Hant", StringComparison.OrdinalIgnoreCase);
        }

        return parts.Skip(1).Any(p => p.Equals("TW", StringComparison.OrdinalIgnoreCase)
            || p.Equals("HK", StringComparison.OrdinalIgnoreCase)
            || p.Equals("MO", StringComparison.OrdinalIgnoreCase));
    }
}
