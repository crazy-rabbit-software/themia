namespace Themia.Core.Localization;

/// <summary>CLDR plural categories, in the index order the generated tables store forms in.</summary>
public enum PluralCategory
{
    Zero = 0,
    One = 1,
    Two = 2,
    Few = 3,
    Many = 4,
    Other = 5,
}

/// <summary>
/// [S-1103] Which plural form a whole number takes in each language (CLDR cardinal rules, integers
/// only — the UI counts things, it does not pluralise "1.5 items").
/// <para>
/// Why it is not "1 → one, else other": French and Portuguese say "0 fichier" (0 is singular),
/// Polish has three forms (1 plik, 2–4 pliki, 5+ plików, but 22 pliki), Russian three on other
/// numbers (1 файл, 2 файла, 5 файлов, but 21 файл), Arabic six (0, 1, 2, 3–10, 11–99, 100…), and
/// Japanese, Chinese, Korean, Indonesian and Vietnamese have no plural at all. A string that ignores this reads wrong to every Polish user.
/// </para>
/// </summary>
public static class PluralRules
{
    public static PluralCategory For(Locale locale, long count)
    {
        // CLDR rules are on the absolute value: "-1 minute" is singular too.
        var n = count == long.MinValue ? long.MaxValue : Math.Abs(count);

        switch (locale)
        {
            case Locale.Ja:
            case Locale.Zh:
            case Locale.Id:
            case Locale.Vi:
            case Locale.Ko:
            case Locale.ZhHant:
                return PluralCategory.Other;

            case Locale.Fr:
            case Locale.Pt:
            case Locale.Hi:
            case Locale.Bn:
                // fr, pt: i = 0,1. hi, bn: i = 0 or n = 1 — the same for whole numbers.
                return n <= 1 ? PluralCategory.One : PluralCategory.Other;

            case Locale.Ru:
            {
                // one: 1, 21, 31 … but not 11; few: 2–4, 22–24 … but not 12–14; many: the rest
                // (0, 5–20, 25–30 …). "other" is for fractions only.
                var mod10 = n % 10;
                var mod100 = n % 100;
                if (mod10 == 1 && mod100 != 11)
                {
                    return PluralCategory.One;
                }

                if (mod10 is >= 2 and <= 4 && mod100 is not (>= 12 and <= 14))
                {
                    return PluralCategory.Few;
                }

                return PluralCategory.Many;
            }

            case Locale.Ar:
            {
                // zero 0, one 1, two 2, few n%100 = 3–10, many n%100 = 11–99, other the rest (100–102, 200 …).
                var mod100 = n % 100;
                return n switch
                {
                    0 => PluralCategory.Zero,
                    1 => PluralCategory.One,
                    2 => PluralCategory.Two,
                    _ when mod100 is >= 3 and <= 10 => PluralCategory.Few,
                    _ when mod100 is >= 11 and <= 99 => PluralCategory.Many,
                    _ => PluralCategory.Other,
                };
            }

            case Locale.Pl:
            {
                if (n == 1)
                {
                    return PluralCategory.One;
                }

                var mod10 = n % 10;
                var mod100 = n % 100;
                if (mod10 is >= 2 and <= 4 && mod100 is not (>= 12 and <= 14))
                {
                    return PluralCategory.Few;
                }

                // Every other integer: 0, 5–21, 25–31, 112–114 …
                return PluralCategory.Many;
            }

            default:
                // en, de, es, it, ur, mr, nl, tr: n = 1.
                return n == 1 ? PluralCategory.One : PluralCategory.Other;
        }
    }

    /// <summary>The categories a language uses for whole numbers (what a translator must supply).</summary>
    public static IReadOnlyList<PluralCategory> Categories(Locale locale) => locale switch
    {
        Locale.Ja or Locale.Zh or Locale.Id or Locale.Vi or Locale.Ko or Locale.ZhHant => [PluralCategory.Other],
        Locale.Pl or Locale.Ru => [PluralCategory.One, PluralCategory.Few, PluralCategory.Many, PluralCategory.Other],
        Locale.Ar => [PluralCategory.Zero, PluralCategory.One, PluralCategory.Two, PluralCategory.Few, PluralCategory.Many, PluralCategory.Other],
        _ => [PluralCategory.One, PluralCategory.Other],
    };
}
