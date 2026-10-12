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
}

/// <summary>[S-1108] Turning the language setting (and Windows' language list) into a <see cref="Locale"/>.</summary>
public static class LanguageResolver
{
    /// <summary>Every locale, in enum order.</summary>
    public static IReadOnlyList<Locale> All { get; } =
        [Locale.En, Locale.De, Locale.Es, Locale.Fr, Locale.It, Locale.Ja, Locale.Pl, Locale.Pt, Locale.Zh,
         Locale.Hi, Locale.Ar, Locale.Bn, Locale.Ru, Locale.Id, Locale.Ur, Locale.Mr, Locale.Vi];

    /// <summary>The two-letter code, which is also the locale file's name (<c>de</c> → de.json).</summary>
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
    /// primary language subtag; null for a language Themia does not have. Every Chinese tag maps to
    /// <see cref="Locale.Zh"/> (Simplified), as in the original (issue #63).
    /// </summary>
    public static Locale? FromTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var span = tag.AsSpan().Trim();
        var end = span.IndexOfAny('-', '_');
        var primary = end < 0 ? span : span[..end];

        foreach (var locale in All)
        {
            if (primary.Equals(Code(locale), StringComparison.OrdinalIgnoreCase))
            {
                return locale;
            }
        }

        return null;
    }
}
