using Themia.Core.Localization;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1100..S-1110] The string layer, the locale files compiled into it, plural rules, language
/// resolution and the per-language formatting tables. Every test names its locale explicitly, so
/// none depends on (or changes) <see cref="Strings.Current"/> except the one that tests switching.
/// </summary>
public sealed class LocalizationTests
{
    private const char Nbsp = (char)0xA0;
    private const char NarrowNbsp = (char)0x202F;

    private static readonly Locale[] Translated = [.. LanguageResolver.All.Where(l => l != Locale.En)];

    public static TheoryData<Locale> AllLocales()
    {
        var data = new TheoryData<Locale>();
        foreach (var locale in LanguageResolver.All)
        {
            data.Add(locale);
        }

        return data;
    }

    // -----------------------------------------------------------------------------------------
    // [S-1100] / [S-1101] Lookup in the compiled tables
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1100_lookup_returns_the_language_text()
    {
        Assert.Equal("Save", Strings.Get(Locale.En, Str.Common_Save));
        Assert.Equal("Speichern", Strings.Get(Locale.De, Str.Common_Save));
        Assert.Equal("保存", Strings.Get(Locale.Ja, Str.Common_Save));
        Assert.Equal("Zegar", Strings.Get(Locale.Pl, Str.Widget_Clock_Label));
    }

    [Fact]
    public void S1100_keys_round_trip_between_text_and_enum()
    {
        Assert.Equal("common.save", Str.Common_Save.Key());
        Assert.True(Strings.TryParseKey("widget.clock.label", out var id));
        Assert.Equal(Str.Widget_Clock_Label, id);
        Assert.Equal("Uhr", Strings.Get(Locale.De, "widget.clock.label"));
    }

    [Fact]
    public void S1104_an_unknown_key_or_id_never_crashes_or_comes_back_empty()
    {
        Assert.False(Strings.TryParseKey("no.such.key", out _));
        Assert.Equal("no.such.key", Strings.Get(Locale.De, "no.such.key"));
        Assert.False(Strings.TryParseKey(null, out _));

        var bogus = (Str)(StringCatalog.Count + 41);
        Assert.False(string.IsNullOrEmpty(Strings.Get(Locale.Fr, bogus)));
        Assert.False(string.IsNullOrEmpty(Strings.Plural(Locale.Pl, bogus, 3)));
    }

    [Fact]
    public void S1104_a_missing_translation_falls_back_to_english()
    {
        var english = new LocaleTable(Locale.En, ["Save", "Open"], [(1, [null, "{count} file", null, null, null, "{count} files"])]);
        var polish = new LocaleTable(Locale.Pl, [null, null], []);
        var german = new LocaleTable(Locale.De, ["Speichern", "{count} Dateien"], [(1, [null, null, null, null, null, "{count} Dateien"])]);

        Assert.Equal("Save", Strings.Lookup(polish, english, 0));
        Assert.Equal("Speichern", Strings.Lookup(german, english, 0));

        // The language's own "other" before English; English's form for the count when it has none.
        Assert.Equal("{count} Dateien", Strings.PluralLookup(german, english, 1, 1));
        Assert.Equal("{count} file", Strings.PluralLookup(polish, english, 1, 1));
        Assert.Equal("{count} files", Strings.PluralLookup(polish, english, 1, 5));

        // Absent everywhere: the key, never an empty string.
        Assert.False(string.IsNullOrEmpty(Strings.Lookup(polish, new LocaleTable(Locale.En, [null, null], []), 0)));
    }

    [Fact]
    public void S1101_the_ported_catalog_is_compiled_in_file_order()
    {
        Assert.True(StringCatalog.Count > 1500, $"only {StringCatalog.Count} keys compiled");
        Assert.Equal(0, (int)Str.Common_Add);
        Assert.Equal("common.add", StringCatalog.Key((Str)0));
    }

    // -----------------------------------------------------------------------------------------
    // [S-1102] Placeholders
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1102_named_parameters_fill_in_one_pass()
    {
        Assert.Equal("Delete \"Work\"? This cannot be undone.", Strings.Format(Locale.En, Str.Common_DeleteEmptyConfirm, ("name", "Work")));
        Assert.Equal("„Work“ löschen? Das lässt sich nicht rückgängig machen.", Strings.Format(Locale.De, Str.Common_DeleteEmptyConfirm, ("name", "Work")));
    }

    [Fact]
    public void S1102_interpolation_edge_cases()
    {
        // A missing argument stays visible; a value is never re-scanned; non-placeholders stay.
        Assert.Equal("a {b} c", Strings.Interpolate("a {b} c", Locale.En, [("x", "1")]));
        Assert.Equal("{count} files", Strings.Interpolate("{name} files", Locale.En, [("name", "{count}")], 5));
        Assert.Equal("{ x } {} {a-b} {", Strings.Interpolate("{ x } {} {a-b} {", Locale.En, [("x", "1")]));
        Assert.Equal("1 and 1", Strings.Interpolate("{x} and {x}", Locale.En, [("x", "1")]));
        Assert.Equal("no braces", Strings.Interpolate("no braces", Locale.En, [("x", "1")]));
    }

    [Fact]
    public void S1102_numbers_are_formatted_for_the_language()
    {
        Assert.Equal("12,345 / 12.345", Strings.Interpolate("{n} / ", Locale.En, [("n", 12345)]) + Strings.Interpolate("{n}", Locale.De, [("n", 12345L)]));
    }

    [Fact]
    public void S1102_placeholder_names_are_extracted_sorted()
    {
        Assert.Equal(["count", "name"], Strings.Placeholders("{name} has {count} of {count}"));
        Assert.Empty(Strings.Placeholders("{} { a } {a-b}"));
    }

    // -----------------------------------------------------------------------------------------
    // [S-1103] Plurals
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Locale.En, 0, PluralCategory.Other)]
    [InlineData(Locale.En, 1, PluralCategory.One)]
    [InlineData(Locale.En, 2, PluralCategory.Other)]
    [InlineData(Locale.De, 1, PluralCategory.One)]
    [InlineData(Locale.Es, 1, PluralCategory.One)]
    [InlineData(Locale.It, 11, PluralCategory.Other)]
    [InlineData(Locale.Fr, 0, PluralCategory.One)]
    [InlineData(Locale.Fr, 1, PluralCategory.One)]
    [InlineData(Locale.Fr, 2, PluralCategory.Other)]
    [InlineData(Locale.Pt, 0, PluralCategory.One)]
    [InlineData(Locale.Pt, 2, PluralCategory.Other)]
    [InlineData(Locale.Ja, 1, PluralCategory.Other)]
    [InlineData(Locale.Zh, 1, PluralCategory.Other)]
    [InlineData(Locale.Pl, 0, PluralCategory.Many)]
    [InlineData(Locale.Pl, 1, PluralCategory.One)]
    [InlineData(Locale.Pl, 2, PluralCategory.Few)]
    [InlineData(Locale.Pl, 4, PluralCategory.Few)]
    [InlineData(Locale.Pl, 5, PluralCategory.Many)]
    [InlineData(Locale.Pl, 12, PluralCategory.Many)]
    [InlineData(Locale.Pl, 14, PluralCategory.Many)]
    [InlineData(Locale.Pl, 21, PluralCategory.Many)]
    [InlineData(Locale.Pl, 22, PluralCategory.Few)]
    [InlineData(Locale.Pl, 104, PluralCategory.Few)]
    [InlineData(Locale.Pl, 112, PluralCategory.Many)]
    [InlineData(Locale.En, -1, PluralCategory.One)]
    [InlineData(Locale.Ru, 0, PluralCategory.Many)]
    [InlineData(Locale.Ru, 1, PluralCategory.One)]
    [InlineData(Locale.Ru, 2, PluralCategory.Few)]
    [InlineData(Locale.Ru, 4, PluralCategory.Few)]
    [InlineData(Locale.Ru, 5, PluralCategory.Many)]
    [InlineData(Locale.Ru, 11, PluralCategory.Many)]
    [InlineData(Locale.Ru, 12, PluralCategory.Many)]
    [InlineData(Locale.Ru, 14, PluralCategory.Many)]
    [InlineData(Locale.Ru, 21, PluralCategory.One)]
    [InlineData(Locale.Ru, 22, PluralCategory.Few)]
    [InlineData(Locale.Ru, 101, PluralCategory.One)]
    [InlineData(Locale.Ru, 111, PluralCategory.Many)]
    [InlineData(Locale.Ru, 112, PluralCategory.Many)]
    [InlineData(Locale.Ar, 0, PluralCategory.Zero)]
    [InlineData(Locale.Ar, 1, PluralCategory.One)]
    [InlineData(Locale.Ar, 2, PluralCategory.Two)]
    [InlineData(Locale.Ar, 3, PluralCategory.Few)]
    [InlineData(Locale.Ar, 10, PluralCategory.Few)]
    [InlineData(Locale.Ar, 11, PluralCategory.Many)]
    [InlineData(Locale.Ar, 99, PluralCategory.Many)]
    [InlineData(Locale.Ar, 100, PluralCategory.Other)]
    [InlineData(Locale.Ar, 102, PluralCategory.Other)]
    [InlineData(Locale.Ar, 103, PluralCategory.Few)]
    [InlineData(Locale.Ar, 111, PluralCategory.Many)]
    [InlineData(Locale.Hi, 0, PluralCategory.One)]
    [InlineData(Locale.Hi, 1, PluralCategory.One)]
    [InlineData(Locale.Hi, 2, PluralCategory.Other)]
    [InlineData(Locale.Bn, 0, PluralCategory.One)]
    [InlineData(Locale.Bn, 5, PluralCategory.Other)]
    [InlineData(Locale.Mr, 0, PluralCategory.Other)]
    [InlineData(Locale.Mr, 1, PluralCategory.One)]
    [InlineData(Locale.Ur, 0, PluralCategory.Other)]
    [InlineData(Locale.Ur, 1, PluralCategory.One)]
    [InlineData(Locale.Id, 1, PluralCategory.Other)]
    [InlineData(Locale.Vi, 1, PluralCategory.Other)]
    [InlineData(Locale.Ko, 1, PluralCategory.Other)]
    [InlineData(Locale.ZhHant, 1, PluralCategory.Other)]
    [InlineData(Locale.Nl, 1, PluralCategory.One)]
    [InlineData(Locale.Nl, 0, PluralCategory.Other)]
    [InlineData(Locale.Tr, 1, PluralCategory.One)]
    [InlineData(Locale.Tr, 2, PluralCategory.Other)]
    public void S1103_plural_category_per_language(Locale locale, long count, PluralCategory expected)
        => Assert.Equal(expected, PluralRules.For(locale, count));

    [Fact]
    public void S1103_plural_picks_the_form_and_fills_count()
    {
        Assert.Equal("1 app", Strings.Plural(Locale.En, Str.Shortcut_AppCount, 1));
        Assert.Equal("3 apps", Strings.Plural(Locale.En, Str.Shortcut_AppCount, 3));
        Assert.Equal("1,500 apps", Strings.Plural(Locale.En, Str.Shortcut_AppCount, 1500));
        Assert.Equal("1 App", Strings.Plural(Locale.De, Str.Shortcut_AppCount, 1));
        Assert.Equal("1 aplikacja", Strings.Plural(Locale.Pl, Str.Shortcut_AppCount, 1));

        // Polish has no "few" form for this key yet: the language's "other" serves, not English.
        Assert.Equal("3 aplikacji", Strings.Plural(Locale.Pl, Str.Shortcut_AppCount, 3));
        Assert.Equal("1 個のアプリ", Strings.Plural(Locale.Ja, Str.Shortcut_AppCount, 1));
    }

    [Fact]
    public void S1103_russian_and_arabic_pick_their_forms_from_the_catalog()
    {
        Assert.Equal("1 приложение", Strings.Plural(Locale.Ru, Str.Shortcut_AppCount, 1));
        Assert.Equal("3 приложения", Strings.Plural(Locale.Ru, Str.Shortcut_AppCount, 3));
        Assert.Equal("5 приложений", Strings.Plural(Locale.Ru, Str.Shortcut_AppCount, 5));
        Assert.Equal("21 приложение", Strings.Plural(Locale.Ru, Str.Shortcut_AppCount, 21));
        // The count goes through LocaleFormat, so Arabic gets Arabic-Indic digits ([S-1115]).
        Assert.Equal("٢ تطبيقان", Strings.Plural(Locale.Ar, Str.Shortcut_AppCount, 2));
        Assert.Equal("٥ تطبيقات", Strings.Plural(Locale.Ar, Str.Shortcut_AppCount, 5));
        Assert.Equal("١١ تطبيقًا", Strings.Plural(Locale.Ar, Str.Shortcut_AppCount, 11));
        Assert.Equal("١٠٠ تطبيق", Strings.Plural(Locale.Ar, Str.Shortcut_AppCount, 100));
    }

    [Fact]
    public void S1103_russian_and_arabic_plurals_supply_every_category()
    {
        // A plural key that leaves one of the language's categories out reads wrong for those
        // counts (they fall back to "other"): Russian 21, Arabic 2 and 11.
        var gaps = new List<string>();
        foreach (var id in StringCatalog.All)
        {
            foreach (var locale in new[] { Locale.Ru, Locale.Ar })
            {
                if (StringCatalog.RawForms(locale, id) is { } forms)
                {
                    foreach (var category in PluralRules.Categories(locale))
                    {
                        if (forms[(int)category] is null)
                        {
                            gaps.Add($"{LanguageResolver.Code(locale)}:{id.Key()} lacks {category}");
                        }
                    }
                }
            }
        }

        Assert.True(gaps.Count == 0, string.Join("; ", gaps));
    }

    [Fact]
    public void S1103_plural_on_a_plain_key_just_formats()
        => Assert.Equal("Save", Strings.Plural(Locale.En, Str.Common_Save, 2));

    // -----------------------------------------------------------------------------------------
    // [S-1105] Completeness: every key everywhere (or English-only on purpose), placeholders intact
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1105_every_key_is_in_every_language_or_marked_english_only()
    {
        var missing = new List<string>();
        var stale = new List<string>();

        foreach (var id in StringCatalog.All)
        {
            var absent = Translated.Where(l => StringCatalog.Raw(l, id) is null).ToList();
            if (StringCatalog.IsEnglishOnly(id))
            {
                if (absent.Count == 0)
                {
                    stale.Add(id.Key());
                }
            }
            else if (absent.Count > 0)
            {
                missing.Add($"{id.Key()} ({string.Join(",", absent.Select(LanguageResolver.Code))})");
            }
        }

        Assert.True(missing.Count == 0, "missing translations (translate, or list under $englishOnly in en.json): " + string.Join("; ", missing));
        Assert.True(stale.Count == 0, "translated everywhere, remove from $englishOnly: " + string.Join("; ", stale));
    }

    [Fact]
    public void S1105_no_text_is_empty()
    {
        var empty = new List<string>();
        foreach (var locale in LanguageResolver.All)
        {
            foreach (var id in StringCatalog.All)
            {
                if (StringCatalog.Raw(locale, id) is { } text && string.IsNullOrWhiteSpace(text))
                {
                    empty.Add($"{LanguageResolver.Code(locale)}:{id.Key()}");
                }
            }
        }

        Assert.Empty(empty);
    }

    [Fact]
    public void S1105_placeholders_match_english_in_every_form()
    {
        var mismatched = new List<string>();
        foreach (var id in StringCatalog.All)
        {
            var english = string.Join(",", Strings.Placeholders(StringCatalog.Raw(Locale.En, id)!));
            foreach (var locale in Translated)
            {
                var texts = StringCatalog.RawForms(locale, id) is { } forms
                    ? forms.Where(f => f is not null).Select(f => f!)
                    : StringCatalog.Raw(locale, id) is { } single ? [single] : [];

                foreach (var text in texts)
                {
                    var own = string.Join(",", Strings.Placeholders(text));
                    if (own != english)
                    {
                        mismatched.Add($"{LanguageResolver.Code(locale)}:{id.Key()} has {{{own}}}, en {{{english}}}");
                    }
                }
            }
        }

        Assert.True(mismatched.Count == 0, string.Join("; ", mismatched));
    }

    [Fact]
    public void S1105_plural_keys_are_plural_everywhere_and_use_only_the_language_categories()
    {
        var problems = new List<string>();
        foreach (var id in StringCatalog.All)
        {
            var englishPlural = StringCatalog.RawForms(Locale.En, id) is not null;
            foreach (var locale in LanguageResolver.All)
            {
                if (StringCatalog.Raw(locale, id) is null)
                {
                    continue;
                }

                var forms = StringCatalog.RawForms(locale, id);
                if ((forms is not null) != englishPlural)
                {
                    problems.Add($"{LanguageResolver.Code(locale)}:{id.Key()} plural shape differs from en");
                    continue;
                }

                if (forms is null)
                {
                    continue;
                }

                var allowed = PluralRules.Categories(locale);
                for (var c = 0; c < forms.Count; c++)
                {
                    if (forms[c] is not null && !allowed.Contains((PluralCategory)c))
                    {
                        problems.Add($"{LanguageResolver.Code(locale)}:{id.Key()} has a '{(PluralCategory)c}' form the language does not use");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    // -----------------------------------------------------------------------------------------
    // [S-1108] Language resolution
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("zh-CN", Locale.Zh)]
    [InlineData("zh-Hans-CN", Locale.Zh)]
    [InlineData("zh-TW", Locale.ZhHant)]
    [InlineData("zh-HK", Locale.ZhHant)]
    [InlineData("zh-MO", Locale.ZhHant)]
    [InlineData("zh-Hant", Locale.ZhHant)]
    [InlineData("zh-Hant-TW", Locale.ZhHant)]
    [InlineData("zh_TW", Locale.ZhHant)]
    [InlineData("zh-Hans-HK", Locale.Zh)]
    [InlineData("zh-SG", Locale.Zh)]
    [InlineData("zh", Locale.Zh)]
    [InlineData("ko-KR", Locale.Ko)]
    [InlineData("nl-NL", Locale.Nl)]
    [InlineData("nl-BE", Locale.Nl)]
    [InlineData("tr-TR", Locale.Tr)]
    [InlineData("de-CH", Locale.De)]
    [InlineData("DE", Locale.De)]
    [InlineData("pt_BR", Locale.Pt)]
    [InlineData("pt-PT", Locale.Pt)]
    [InlineData("ja-JP", Locale.Ja)]
    [InlineData("pl", Locale.Pl)]
    [InlineData("en-GB", Locale.En)]
    [InlineData("hi-IN", Locale.Hi)]
    [InlineData("ar-SA", Locale.Ar)]
    [InlineData("ar-EG", Locale.Ar)]
    [InlineData("bn-BD", Locale.Bn)]
    [InlineData("bn-IN", Locale.Bn)]
    [InlineData("ru-RU", Locale.Ru)]
    [InlineData("id-ID", Locale.Id)]
    [InlineData("ur-PK", Locale.Ur)]
    [InlineData("ur-IN", Locale.Ur)]
    [InlineData("mr-IN", Locale.Mr)]
    [InlineData("vi-VN", Locale.Vi)]
    public void S1108_tags_map_by_primary_subtag(string tag, Locale expected)
        => Assert.Equal(expected, LanguageResolver.FromTag(tag));

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("")]
    [InlineData("dei")]
    [InlineData("-de")]
    public void S1108_unknown_tags_are_not_guessed(string tag)
        => Assert.Null(LanguageResolver.FromTag(tag));

    [Fact]
    public void S1108_system_takes_the_first_supported_preference_else_english()
    {
        Assert.Equal(Locale.De, LanguageResolver.Resolve(AppLanguage.System, ["sv-SE", "de-DE", "fr-FR"]));
        Assert.Equal(Locale.En, LanguageResolver.Resolve(AppLanguage.System, ["sv-SE", "fi-FI"]));
        Assert.Equal(Locale.En, LanguageResolver.Resolve(AppLanguage.System, []));
        Assert.Equal(Locale.Ja, LanguageResolver.Resolve(AppLanguage.Japanese, ["de-DE"]));
        Assert.Equal(Locale.En, LanguageResolver.Resolve(AppLanguage.English, ["de-DE"]));
    }

    [Fact]
    public void S1112_twenty_one_languages_and_each_setting_round_trips()
    {
        Assert.Equal(21, LanguageResolver.All.Count);
        Assert.Equal(LanguageResolver.All.Count, Enum.GetValues<Locale>().Length);
        Assert.Equal(LanguageResolver.All.Count + 1, Enum.GetValues<AppLanguage>().Length);
        foreach (var locale in LanguageResolver.All)
        {
            Assert.Equal(locale, LanguageResolver.Resolve(LanguageResolver.Setting(locale), ["de-DE"]));
            Assert.Equal(locale, LanguageResolver.FromTag(LanguageResolver.Code(locale)));
        }
    }

    [Fact]
    public void S1108_every_explicit_language_has_its_locale()
    {
        foreach (var language in Enum.GetValues<AppLanguage>().Where(l => l != AppLanguage.System))
        {
            var locale = LanguageResolver.Resolve(language, []);
            Assert.True(locale != Locale.En || language == AppLanguage.English, $"{language} resolved to English");
        }
    }

    // -----------------------------------------------------------------------------------------
    // [S-1106] Formatting tables, per language
    // -----------------------------------------------------------------------------------------

    private static readonly DateOnly Sunday = new(2026, 10, 4);

    [Theory]
    [InlineData(Locale.En, "10/4/2026", "Oct 4, 2026", "October 4, 2026", "Sunday, October 4, 2026", "October 4", "Oct 4", "October 2026", "Sun, Oct 4")]
    [InlineData(Locale.De, "04.10.2026", "04.10.2026", "4. Oktober 2026", "Sonntag, 4. Oktober 2026", "4. Oktober", "4. Okt.", "Oktober 2026", "So., 4. Okt.")]
    [InlineData(Locale.Es, "4/10/2026", "4 oct 2026", "4 de octubre de 2026", "domingo, 4 de octubre de 2026", "4 de octubre", "4 oct", "octubre de 2026", "dom, 4 oct")]
    [InlineData(Locale.Fr, "04/10/2026", "4 oct. 2026", "4 octobre 2026", "dimanche 4 octobre 2026", "4 octobre", "4 oct.", "octobre 2026", "dim. 4 oct.")]
    [InlineData(Locale.It, "04/10/2026", "4 ott 2026", "4 ottobre 2026", "domenica 4 ottobre 2026", "4 ottobre", "4 ott", "ottobre 2026", "dom 4 ott")]
    [InlineData(Locale.Ja, "2026/10/04", "2026/10/04", "2026年10月4日", "2026年10月4日日曜日", "10月4日", "10月4日", "2026年10月", "10月4日(日)")]
    [InlineData(Locale.Pl, "04.10.2026", "4 paź 2026", "4 października 2026", "niedziela, 4 października 2026", "4 października", "4 paź", "październik 2026", "niedz., 4 paź")]
    [InlineData(Locale.Pt, "04/10/2026", "4 de out. de 2026", "4 de outubro de 2026", "domingo, 4 de outubro de 2026", "4 de outubro", "4 de out.", "outubro de 2026", "dom., 4 de out.")]
    [InlineData(Locale.Zh, "2026/10/4", "2026年10月4日", "2026年10月4日", "2026年10月4日星期日", "10月4日", "10月4日", "2026年10月", "10月4日周日")]
    [InlineData(Locale.Hi, "4/10/2026", "4 अक्तू॰ 2026", "4 अक्तूबर 2026", "रविवार, 4 अक्तूबर 2026", "4 अक्तूबर", "4 अक्तू॰", "अक्तूबर 2026", "रवि, 4 अक्तू॰")]
    [InlineData(Locale.Ar, "٤/١٠/٢٠٢٦", "٠٤/١٠/٢٠٢٦", "٤ أكتوبر ٢٠٢٦", "الأحد، ٤ أكتوبر ٢٠٢٦", "٤ أكتوبر", "٤ أكتوبر", "أكتوبر ٢٠٢٦", "الأحد، ٤ أكتوبر")]
    [InlineData(Locale.Bn, "4/10/2026", "4 অক্টো, 2026", "4 অক্টোবর, 2026", "রবিবার, 4 অক্টোবর, 2026", "4 অক্টোবর", "4 অক্টো", "অক্টোবর 2026", "রবি, 4 অক্টো")]
    [InlineData(Locale.Ru, "04.10.2026", "4 окт. 2026 г.", "4 октября 2026 г.", "воскресенье, 4 октября 2026 г.", "4 октября", "4 окт.", "октябрь 2026 г.", "вс, 4 окт.")]
    [InlineData(Locale.Id, "04/10/2026", "4 Okt 2026", "4 Oktober 2026", "Minggu, 04 Oktober 2026", "4 Oktober", "4 Okt", "Oktober 2026", "Min, 4 Okt")]
    [InlineData(Locale.Ur, "4/10/2026", "4 اکتوبر، 2026", "4 اکتوبر، 2026", "اتوار، 4 اکتوبر، 2026", "4 اکتوبر", "4 اکتوبر", "اکتوبر 2026", "اتوار، 4 اکتوبر")]
    [InlineData(Locale.Mr, "4/10/2026", "4 ऑक्टो, 2026", "4 ऑक्टोबर, 2026", "रविवार, 4 ऑक्टोबर, 2026", "4 ऑक्टोबर", "4 ऑक्टो", "ऑक्टोबर 2026", "रवि, 4 ऑक्टो")]
    [InlineData(Locale.Vi, "04/10/2026", "4 thg 10, 2026", "4 tháng 10, 2026", "Chủ Nhật, 4 tháng 10, 2026", "4 tháng 10", "4 thg 10", "Tháng 10 năm 2026", "CN, 4 thg 10")]
    [InlineData(Locale.Ko, "2026. 10. 4.", "2026. 10. 4.", "2026년 10월 4일", "2026년 10월 4일 일요일", "10월 4일", "10월 4일", "2026년 10월", "10월 4일 (일)")]
    [InlineData(Locale.Nl, "04-10-2026", "4 okt 2026", "4 oktober 2026", "zondag 4 oktober 2026", "4 oktober", "4 okt", "oktober 2026", "zo 4 okt")]
    [InlineData(Locale.Tr, "4.10.2026", "4 Eki 2026", "4 Ekim 2026", "4 Ekim 2026 Pazar", "4 Ekim", "4 Eki", "Ekim 2026", "4 Eki Paz")]
    [InlineData(Locale.ZhHant, "2026/10/4", "2026年10月4日", "2026年10月4日", "2026年10月4日 星期日", "10月4日", "10月4日", "2026年10月", "10月4日 週日")]
    public void S1106_dates_per_language(Locale locale, string shortDate, string medium, string longDate, string full, string monthDay, string monthDayShort, string monthYear, string weekdayShort)
    {
        Assert.Equal(shortDate, LocaleFormat.Date(locale, Sunday, DateStyle.Short));
        Assert.Equal(medium, LocaleFormat.Date(locale, Sunday, DateStyle.Medium));
        Assert.Equal(longDate, LocaleFormat.Date(locale, Sunday, DateStyle.Long));
        Assert.Equal(full, LocaleFormat.Date(locale, Sunday, DateStyle.Full));
        Assert.Equal(monthDay, LocaleFormat.Date(locale, Sunday, DateStyle.MonthDay));
        Assert.Equal(monthDayShort, LocaleFormat.Date(locale, Sunday, DateStyle.MonthDayShort));
        Assert.Equal(monthYear, LocaleFormat.Date(locale, Sunday, DateStyle.MonthYear));
        Assert.Equal(weekdayShort, LocaleFormat.Date(locale, Sunday, DateStyle.WeekdayMonthDayShort));
    }

    [Theory]
    [InlineData(Locale.En, "3:05 PM", "15:05", "3:05:09 PM", "12:30 AM")]
    [InlineData(Locale.De, "15:05", "15:05", "15:05:09", "12:30 AM")]
    [InlineData(Locale.Es, "15:05", "15:05", "15:05:09", "12:30 a. m.")]
    [InlineData(Locale.Fr, "15:05", "15:05", "15:05:09", "12:30 AM")]
    [InlineData(Locale.It, "15:05", "15:05", "15:05:09", "12:30 AM")]
    [InlineData(Locale.Ja, "15:05", "15:05", "15:05:09", "午前0:30")]
    [InlineData(Locale.Pl, "15:05", "15:05", "15:05:09", "12:30 AM")]
    [InlineData(Locale.Pt, "15:05", "15:05", "15:05:09", "12:30 AM")]
    [InlineData(Locale.Zh, "15:05", "15:05", "15:05:09", "上午12:30")]
    [InlineData(Locale.Hi, "3:05 pm", "15:05", "3:05:09 pm", "12:30 am")]
    [InlineData(Locale.Ar, "٣:٠٥ م", "١٥:٠٥", "٣:٠٥:٠٩ م", "١٢:٣٠ ص")]
    [InlineData(Locale.Bn, "3:05 PM", "15:05", "3:05:09 PM", "12:30 AM")]
    [InlineData(Locale.Ru, "15:05", "15:05", "15:05:09", "12:30 AM")]
    [InlineData(Locale.Id, "15.05", "15.05", "15.05.09", "12.30 AM")]
    [InlineData(Locale.Ur, "3:05 PM", "15:05", "3:05:09 PM", "12:30 AM")]
    [InlineData(Locale.Mr, "3:05 PM", "15:05", "3:05:09 PM", "12:30 AM")]
    [InlineData(Locale.Vi, "15:05", "15:05", "15:05:09", "12:30 SA")]
    [InlineData(Locale.Ko, "오후 3:05", "15:05", "오후 3:05:09", "오전 12:30")]
    [InlineData(Locale.Nl, "15:05", "15:05", "15:05:09", "12:30 a.m.")]
    [InlineData(Locale.Tr, "15:05", "15:05", "15:05:09", "ÖÖ 12:30")]
    [InlineData(Locale.ZhHant, "下午3:05", "15:05", "下午3:05:09", "上午12:30")]
    public void S1106_times_per_language(Locale locale, string localeDefault, string h24, string withSeconds, string halfPastMidnight12)
    {
        var afternoon = new TimeOnly(15, 5, 9);
        Assert.Equal(localeDefault, LocaleFormat.Time(locale, afternoon));
        Assert.Equal(h24, LocaleFormat.Time(locale, afternoon, HourCycle.H24));
        Assert.Equal(withSeconds, LocaleFormat.Time(locale, afternoon, seconds: true));
        Assert.Equal(halfPastMidnight12, LocaleFormat.Time(locale, new TimeOnly(0, 30), HourCycle.H12));
    }

    [Fact]
    public void S1106_twelve_hour_edges_and_single_digit_hours()
    {
        Assert.Equal("12:00 PM", LocaleFormat.Time(Locale.En, new TimeOnly(12, 0)));
        Assert.Equal("9:05", LocaleFormat.Time(Locale.Es, new TimeOnly(9, 5)));
        Assert.Equal("09:05", LocaleFormat.Time(Locale.De, new TimeOnly(9, 5)));
        Assert.Equal("午後3:05", LocaleFormat.Time(Locale.Ja, new TimeOnly(15, 5), HourCycle.H12));
        Assert.Equal("下午3:05", LocaleFormat.Time(Locale.Zh, new TimeOnly(15, 5), HourCycle.H12));
        Assert.Equal("3:05 p. m.", LocaleFormat.Time(Locale.Es, new TimeOnly(15, 5), HourCycle.H12));
    }

    [Theory]
    [InlineData(Locale.En, "1,234,567.89", "1,234", "-1,234.5")]
    [InlineData(Locale.De, "1.234.567,89", "1.234", "-1.234,5")]
    [InlineData(Locale.Es, "1.234.567,89", "1234", "-1234,5")]
    [InlineData(Locale.Fr, "1\u202F234\u202F567,89", "1\u202F234", "-1\u202F234,5")]
    [InlineData(Locale.It, "1.234.567,89", "1.234", "-1.234,5")]
    [InlineData(Locale.Ja, "1,234,567.89", "1,234", "-1,234.5")]
    [InlineData(Locale.Pl, "1\u00A0234\u00A0567,89", "1234", "-1234,5")]
    [InlineData(Locale.Pt, "1.234.567,89", "1.234", "-1.234,5")]
    [InlineData(Locale.Zh, "1,234,567.89", "1,234", "-1,234.5")]
    [InlineData(Locale.Hi, "12,34,567.89", "1,234", "-1,234.5")]
    [InlineData(Locale.Ar, "١٬٢٣٤٬٥٦٧٫٨٩", "١٬٢٣٤", "-١٬٢٣٤٫٥")]
    [InlineData(Locale.Bn, "12,34,567.89", "1,234", "-1,234.5")]
    [InlineData(Locale.Ru, "1\u00A0234\u00A0567,89", "1\u00A0234", "-1\u00A0234,5")]
    [InlineData(Locale.Id, "1.234.567,89", "1.234", "-1.234,5")]
    [InlineData(Locale.Ur, "1,234,567.89", "1,234", "-1,234.5")]
    [InlineData(Locale.Mr, "12,34,567.89", "1,234", "-1,234.5")]
    [InlineData(Locale.Vi, "1.234.567,89", "1.234", "-1.234,5")]
    [InlineData(Locale.Ko, "1,234,567.89", "1,234", "-1,234.5")]
    [InlineData(Locale.Nl, "1.234.567,89", "1.234", "-1.234,5")]
    [InlineData(Locale.Tr, "1.234.567,89", "1.234", "-1.234,5")]
    [InlineData(Locale.ZhHant, "1,234,567.89", "1,234", "-1,234.5")]
    public void S1106_numbers_per_language(Locale locale, string big, string fourDigits, string negative)
    {
        Assert.Equal(big, LocaleFormat.Number(locale, 1234567.891, 2));
        Assert.Equal(fourDigits, LocaleFormat.Integer(locale, 1234));
        Assert.Equal(negative, LocaleFormat.Number(locale, -1234.5, 1));
    }

    [Fact]
    public void S1106_number_edges()
    {
        Assert.Equal("12.345", LocaleFormat.Integer(Locale.Es, 12345));
        Assert.Equal($"12{Nbsp}345", LocaleFormat.Integer(Locale.Pl, 12345));
        Assert.Equal("0.00", LocaleFormat.Number(Locale.En, -0.001, 2));
        Assert.Equal("3", LocaleFormat.Number(Locale.En, 2.5, 0));
        Assert.Equal("-9,223,372,036,854,775,808", LocaleFormat.Integer(Locale.En, long.MinValue));
        Assert.Equal("100", LocaleFormat.Integer(Locale.En, 100));
        Assert.Equal("NaN", LocaleFormat.Number(Locale.De, double.NaN, 1));
        Assert.Equal("42%", LocaleFormat.Percent(Locale.En, 42));
        Assert.Equal($"42{Nbsp}%", LocaleFormat.Percent(Locale.De, 42));
        Assert.Equal($"42,5{NarrowNbsp}%", LocaleFormat.Percent(Locale.Fr, 42.5, 1));

        // The Indian system groups by two after the first three: lakh and crore.
        Assert.Equal("1,23,45,67,890", LocaleFormat.Integer(Locale.Hi, 1234567890));
        Assert.Equal("12,345", LocaleFormat.Integer(Locale.Mr, 12345));
        Assert.Equal("123", LocaleFormat.Integer(Locale.Bn, 123));
        Assert.Equal("-12,34,567", LocaleFormat.Integer(Locale.Hi, -1234567));
        Assert.Equal($"42{Nbsp}%", LocaleFormat.Percent(Locale.Ru, 42));
        Assert.Equal("%42", LocaleFormat.Percent(Locale.Tr, 42));
        Assert.Equal("٤٢٪", LocaleFormat.Percent(Locale.Ar, 42));
    }

    [Fact]
    public void S1106_russian_and_vietnamese_decline_months_inside_a_date()
    {
        var may = new DateOnly(2026, 5, 4);
        Assert.Equal("май", LocaleFormat.MonthName(Locale.Ru, 5));
        Assert.Equal("4 мая", LocaleFormat.Date(Locale.Ru, may, DateStyle.MonthDayShort));
        Assert.Equal("4 мая 2026 г.", LocaleFormat.Date(Locale.Ru, may, DateStyle.Medium));
        Assert.Equal("май 2026 г.", LocaleFormat.Date(Locale.Ru, may, DateStyle.MonthYear));
        Assert.Equal("Tháng 5", LocaleFormat.MonthName(Locale.Vi, 5));
        Assert.Equal("4 tháng 5", LocaleFormat.Date(Locale.Vi, may, DateStyle.MonthDay));
    }

    [Theory]
    [InlineData(Locale.En, "January", "Jan", "Monday", "Mon", "M", DayOfWeek.Sunday)]
    [InlineData(Locale.De, "Januar", "Jan.", "Montag", "Mo.", "M", DayOfWeek.Monday)]
    [InlineData(Locale.Es, "enero", "ene", "lunes", "lun", "L", DayOfWeek.Monday)]
    [InlineData(Locale.Fr, "janvier", "janv.", "lundi", "lun.", "L", DayOfWeek.Monday)]
    [InlineData(Locale.It, "gennaio", "gen", "lunedì", "lun", "L", DayOfWeek.Monday)]
    [InlineData(Locale.Ja, "1月", "1月", "月曜日", "月", "月", DayOfWeek.Sunday)]
    [InlineData(Locale.Pl, "styczeń", "sty", "poniedziałek", "pon.", "P", DayOfWeek.Monday)]
    [InlineData(Locale.Pt, "janeiro", "jan.", "segunda-feira", "seg.", "S", DayOfWeek.Sunday)]
    [InlineData(Locale.Zh, "一月", "1月", "星期一", "周一", "一", DayOfWeek.Monday)]
    [InlineData(Locale.Hi, "जनवरी", "जन॰", "सोमवार", "सोम", "सो", DayOfWeek.Sunday)]
    [InlineData(Locale.Ar, "يناير", "يناير", "الاثنين", "الاثنين", "ن", DayOfWeek.Sunday)]
    [InlineData(Locale.Bn, "জানুয়ারী", "জানু", "সোমবার", "সোম", "সো", DayOfWeek.Sunday)]
    [InlineData(Locale.Ru, "январь", "янв.", "понедельник", "пн", "П", DayOfWeek.Monday)]
    [InlineData(Locale.Id, "Januari", "Jan", "Senin", "Sen", "S", DayOfWeek.Sunday)]
    [InlineData(Locale.Ur, "جنوری", "جنوری", "پیر", "پیر", "پ", DayOfWeek.Monday)]
    [InlineData(Locale.Mr, "जानेवारी", "जाने", "सोमवार", "सोम", "सो", DayOfWeek.Sunday)]
    [InlineData(Locale.Vi, "Tháng 1", "Thg 1", "Thứ Hai", "Th 2", "T2", DayOfWeek.Monday)]
    [InlineData(Locale.Ko, "1월", "1월", "월요일", "월", "월", DayOfWeek.Sunday)]
    [InlineData(Locale.Nl, "januari", "jan", "maandag", "ma", "M", DayOfWeek.Monday)]
    [InlineData(Locale.Tr, "Ocak", "Oca", "Pazartesi", "Pzt", "P", DayOfWeek.Monday)]
    [InlineData(Locale.ZhHant, "1月", "1月", "星期一", "週一", "一", DayOfWeek.Sunday)]
    public void S1106_names_and_first_day_per_language(Locale locale, string january, string jan, string monday, string mon, string m, DayOfWeek firstDay)
    {
        Assert.Equal(january, LocaleFormat.MonthName(locale, 1));
        Assert.Equal(jan, LocaleFormat.MonthName(locale, 1, NameWidth.Short));
        Assert.Equal(monday, LocaleFormat.WeekdayName(locale, DayOfWeek.Monday));
        Assert.Equal(mon, LocaleFormat.WeekdayName(locale, DayOfWeek.Monday, NameWidth.Short));
        Assert.Equal(m, LocaleFormat.WeekdayName(locale, DayOfWeek.Monday, NameWidth.Narrow));
        Assert.Equal(firstDay, LocaleFormat.FirstDayOfWeek(locale));
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void S1106_every_table_is_complete(Locale locale)
    {
        for (var month = 1; month <= 12; month++)
        {
            Assert.False(string.IsNullOrWhiteSpace(LocaleFormat.MonthName(locale, month)));
            Assert.False(string.IsNullOrWhiteSpace(LocaleFormat.MonthName(locale, month, NameWidth.Short)));
            Assert.False(string.IsNullOrWhiteSpace(LocaleFormat.Date(locale, new DateOnly(2026, month, 28), DateStyle.Full)));
        }

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            Assert.False(string.IsNullOrWhiteSpace(LocaleFormat.WeekdayName(locale, day, NameWidth.Narrow)));
        }

        foreach (var unit in Enum.GetValues<RelativeUnit>())
        {
            foreach (var style in Enum.GetValues<RelativeStyle>())
            {
                foreach (var count in new long[] { -1, -2, -5, -22, 1, 3, 12 })
                {
                    var text = LocaleFormat.Relative(locale, unit, count, style);

                    // Arabic says one and two in words ("قبل دقيقتين"), as CLDR has it; every
                    // other form shows the number.
                    if (!(locale == Locale.Ar && Math.Abs(count) <= 2))
                    {
                        Assert.Contains(LocaleFormat.Integer(locale, Math.Abs(count)), text);
                    }

                    Assert.DoesNotContain("{0}", text);
                }
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // [S-1107] Relative times
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Locale.En, "3 minutes ago", "1 minute ago", "in 2 days", "now")]
    [InlineData(Locale.De, "vor 3 Minuten", "vor 1 Minute", "in 2 Tagen", "jetzt")]
    [InlineData(Locale.Es, "hace 3 minutos", "hace 1 minuto", "dentro de 2 días", "ahora")]
    [InlineData(Locale.Fr, "il y a 3 minutes", "il y a 1 minute", "dans 2 jours", "maintenant")]
    [InlineData(Locale.It, "3 minuti fa", "1 minuto fa", "tra 2 giorni", "ora")]
    [InlineData(Locale.Ja, "3 分前", "1 分前", "2 日後", "今")]
    [InlineData(Locale.Pl, "3 minuty temu", "1 minutę temu", "za 2 dni", "teraz")]
    [InlineData(Locale.Pt, "há 3 minutos", "há 1 minuto", "em 2 dias", "agora")]
    [InlineData(Locale.Zh, "3分钟前", "1分钟前", "2天后", "现在")]
    [InlineData(Locale.Hi, "3 मिनट पहले", "1 मिनट पहले", "2 दिन में", "अभी")]
    [InlineData(Locale.Ar, "قبل ٣ دقائق", "قبل دقيقة واحدة", "خلال يومين", "الآن")]
    [InlineData(Locale.Bn, "3 মিনিট আগে", "1 মিনিট আগে", "2 দিনের মধ্যে", "এখন")]
    [InlineData(Locale.Ru, "3 минуты назад", "1 минуту назад", "через 2 дня", "сейчас")]
    [InlineData(Locale.Id, "3 menit yang lalu", "1 menit yang lalu", "dalam 2 hari", "sekarang")]
    [InlineData(Locale.Ur, "3 منٹ پہلے", "1 منٹ پہلے", "2 دن میں", "ابھی")]
    [InlineData(Locale.Mr, "3 मिनिटांपूर्वी", "1 मिनिटापूर्वी", "2 दिवसांमध्ये", "आत्ता")]
    [InlineData(Locale.Vi, "3 phút trước", "1 phút trước", "sau 2 ngày", "bây giờ")]
    [InlineData(Locale.Ko, "3분 전", "1분 전", "2일 후", "지금")]
    [InlineData(Locale.Nl, "3 minuten geleden", "1 minuut geleden", "over 2 dagen", "nu")]
    [InlineData(Locale.Tr, "3 dakika önce", "1 dakika önce", "2 gün sonra", "şimdi")]
    [InlineData(Locale.ZhHant, "3 分鐘前", "1 分鐘前", "2 天後", "現在")]
    public void S1107_relative_times_per_language(Locale locale, string threeMinutesAgo, string oneMinuteAgo, string inTwoDays, string now)
    {
        var at = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(threeMinutesAgo, LocaleFormat.Relative(locale, at.AddMinutes(-3), at));
        Assert.Equal(oneMinuteAgo, LocaleFormat.Relative(locale, at.AddSeconds(-61), at));
        Assert.Equal(inTwoDays, LocaleFormat.Relative(locale, at.AddDays(2).AddHours(3), at));
        Assert.Equal(now, LocaleFormat.Relative(locale, at.AddSeconds(-59), at));
    }

    [Fact]
    public void S1107_units_and_polish_forms()
    {
        Assert.Equal("5 minut temu", LocaleFormat.Relative(Locale.Pl, RelativeUnit.Minute, -5));
        Assert.Equal("22 minuty temu", LocaleFormat.Relative(Locale.Pl, RelativeUnit.Minute, -22));
        Assert.Equal("12 minut temu", LocaleFormat.Relative(Locale.Pl, RelativeUnit.Minute, -12));
        Assert.Equal("1 hour ago", LocaleFormat.Relative(Locale.En, TimeSpan.FromMinutes(-119)));
        Assert.Equal("2 weeks ago", LocaleFormat.Relative(Locale.En, TimeSpan.FromDays(-14)));
        Assert.Equal("3 months ago", LocaleFormat.Relative(Locale.En, TimeSpan.FromDays(-95)));
        Assert.Equal("2 years ago", LocaleFormat.Relative(Locale.En, TimeSpan.FromDays(-800)));
        Assert.Equal("3 min. ago", LocaleFormat.Relative(Locale.En, TimeSpan.FromMinutes(-3), RelativeStyle.Short));
        Assert.Equal("1,200 years ago", LocaleFormat.Relative(Locale.En, RelativeUnit.Year, -1200));
        Assert.Equal("now", LocaleFormat.Relative(Locale.En, TimeSpan.Zero));
        Assert.Equal("5 минут назад", LocaleFormat.Relative(Locale.Ru, RelativeUnit.Minute, -5));
        Assert.Equal("21 минуту назад", LocaleFormat.Relative(Locale.Ru, RelativeUnit.Minute, -21));
        Assert.Equal("через 5 лет", LocaleFormat.Relative(Locale.Ru, RelativeUnit.Year, 5));
        Assert.Equal("قبل ١١ دقيقة", LocaleFormat.Relative(Locale.Ar, RelativeUnit.Minute, -11));
        Assert.Equal("قبل ١٠٠ يوم", LocaleFormat.Relative(Locale.Ar, RelativeUnit.Day, -100));
    }

    // -----------------------------------------------------------------------------------------
    // [S-1115] Native digits
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1115_only_arabic_writes_its_own_digits()
    {
        Assert.Equal([Locale.Ar], LanguageResolver.All.Where(LocaleFormat.HasNativeDigits));
        Assert.Equal("٠١٢٣٤٥٦٧٨٩", LocaleFormat.Digits(Locale.Ar, "0123456789"));
        Assert.Equal("0123456789", LocaleFormat.Digits(Locale.Ur, "0123456789"));
        Assert.Equal("0123456789", LocaleFormat.Digits(Locale.Bn, "0123456789"));
        Assert.Equal("09:41", LocaleFormat.Digits(Locale.En, "09:41"));
    }

    [Fact]
    public void S1115_mapping_touches_only_ascii_digits_and_keeps_the_length()
    {
        const string mixed = "UTC+5:30 Teams ٣ x9";
        var mapped = LocaleFormat.Digits(Locale.Ar, mixed);
        Assert.Equal("UTC+٥:٣٠ Teams ٣ x٩", mapped);
        Assert.Equal(mixed.Length, mapped.Length);
        Assert.Equal(string.Empty, LocaleFormat.Digits(Locale.Ar, string.Empty));
    }

    /// <summary>
    /// Regression: an agenda row's "09:00–09:15 · Teams" came out with Latin digits in Arabic,
    /// because the layout's contextual substitution follows the Latin word beside the time.
    /// </summary>
    [Fact]
    public void S1115_a_time_range_beside_latin_text_keeps_arabic_digits()
    {
        var start = LocaleFormat.Time(Locale.Ar, new TimeOnly(9, 0), HourCycle.H24);
        var end = LocaleFormat.Time(Locale.Ar, new TimeOnly(9, 15), HourCycle.H24);
        var row = $"{start}–{end} · Teams";

        Assert.Equal("٠٩:٠٠–٠٩:١٥ · Teams", row);
        Assert.DoesNotContain(row, char.IsAsciiDigit);
    }

    [Fact]
    public void S1115_every_arabic_format_is_free_of_latin_digits()
    {
        var at = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        foreach (var style in Enum.GetValues<DateStyle>())
        {
            Assert.DoesNotContain(LocaleFormat.Date(Locale.Ar, Sunday, style), char.IsAsciiDigit);
        }

        Assert.DoesNotContain(LocaleFormat.Time(Locale.Ar, new TimeOnly(23, 59, 58), HourCycle.H12, seconds: true), char.IsAsciiDigit);
        Assert.DoesNotContain(LocaleFormat.Relative(Locale.Ar, at.AddDays(-12), at), char.IsAsciiDigit);
        Assert.DoesNotContain(LocaleFormat.Number(Locale.Ar, 1234567.25, 2), char.IsAsciiDigit);
        Assert.DoesNotContain(Strings.Format(Locale.Ar, Str.WidgetsB_Time_MinutesCompact, ("count", 42L)), char.IsAsciiDigit);
    }

    [Fact]
    public void S1115_latin_digit_languages_are_unchanged()
    {
        Assert.Equal("09:00", LocaleFormat.Time(Locale.Ur, new TimeOnly(9, 0), HourCycle.H24));
        Assert.Equal("09:00", LocaleFormat.Time(Locale.Bn, new TimeOnly(9, 0), HourCycle.H24));
        Assert.Equal("1,234", LocaleFormat.Integer(Locale.Ur, 1234));
    }
}

/// <summary>
/// The only tests that switch the process-wide language. Everything else passes a <see cref="Locale"/>
/// explicitly; these run in a collection of their own, never beside another test class, because a
/// test reading <see cref="Strings.Current"/> while one of these has it on German would fail at random.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalLocaleCollection
{
    public const string Name = "Global locale";
}

/// <summary>[S-1110] Live switching of <see cref="Strings.Current"/>.</summary>
[Collection(GlobalLocaleCollection.Name)]
public class LiveLanguageSwitchTests
{
    [Fact]
    public void S1110_switching_raises_changed_once_and_lookups_follow()
    {
        var seen = new List<Locale>();
        void OnChanged(Locale l) => seen.Add(l);

        Strings.SetLocale(Locale.En);
        Strings.Changed += OnChanged;
        try
        {
            Strings.SetLocale(Locale.De);
            Strings.SetLocale(Locale.De);
            Assert.Equal("Speichern", Str.Common_Save.Text());
            Assert.Equal("3 Apps", Str.Shortcut_AppCount.Plural(3));
            Strings.SetLocale((Locale)99);
            Assert.Equal(Locale.De, Strings.Current);
        }
        finally
        {
            Strings.Changed -= OnChanged;
            Strings.SetLocale(Locale.En);
        }

        Assert.Equal([Locale.De], seen);
    }
}
