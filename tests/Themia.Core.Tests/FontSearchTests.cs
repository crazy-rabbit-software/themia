using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6440]..[S-6442] The font picker's list and its search.</summary>
public sealed class FontSearchTests
{
    private static readonly FontOption[] Presets =
    [
        new(DesignLook.SystemFont, "System default"),
        new(DesignLook.SansFont, "Sans serif"),
        new(DesignLook.SerifFont, "Serif"),
        new(DesignLook.MonoFont, "Monospace"),
    ];

    private static readonly string[] Installed =
    [
        "Arial", "Cascadia Code", "Cascadia Mono", "Consolas", "Courier New", "Fira Sans Condensed",
        "Georgia", "Noto Sans Mono", "Segoe Print", "Segoe UI", "Segoe UI Variable", "Señor Sans",
    ];

    private static InstalledFonts Fonts(params string[] names) => InstalledFonts.From(names.Select(n => (n, false)));

    private static IReadOnlyList<FontOption> All => FontSearch.Options(Presets, Fonts(Installed), DesignLook.SystemFont);

    private static string[] Labels(IEnumerable<FontOption> options) => [.. options.Select(o => o.Label)];

    [Fact]
    public void S6440_lists_the_presets_then_the_installed_families_in_order()
    {
        Assert.Equal(
            ["System default", "Sans serif", "Serif", "Monospace", .. Installed],
            Labels(All));
    }

    [Fact]
    public void S6440_a_set_family_that_is_not_installed_is_listed_after_the_presets()
    {
        var options = FontSearch.Options(Presets, Fonts(Installed), "Gone Font");

        Assert.Equal("Gone Font", options[4].Value);
        Assert.Equal(Presets.Length + Installed.Length + 1, options.Count);
    }

    [Theory]
    [InlineData("segoe ui")]
    [InlineData("  Segoe UI ")]
    public void S6440_an_installed_set_family_keeps_its_place_and_is_not_listed_twice(string current)
    {
        var options = FontSearch.Options(Presets, Fonts(Installed), current);

        Assert.Single(options, o => o.Value.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(Presets.Length + Installed.Length, options.Count);
    }

    [Fact]
    public void S6440_an_implausible_value_or_a_preset_adds_no_entry()
    {
        Assert.Equal(Presets.Length + Installed.Length, FontSearch.Options(Presets, Fonts(Installed), "x;<script>").Count);
        Assert.Equal(Presets.Length + Installed.Length, FontSearch.Options(Presets, Fonts(Installed), "MONO").Count);
        Assert.Equal(Presets.Length + Installed.Length, FontSearch.Options(Presets, Fonts(Installed), null).Count);
    }

    [Fact]
    public void S6440_duplicate_or_bad_installed_names_are_dropped()
    {
        var options = FontSearch.Options(Presets, Fonts("Arial", "arial", "", "bad;name"), null);

        Assert.Equal(["System default", "Sans serif", "Serif", "Monospace", "Arial"], Labels(options));
    }

    [Theory]
    [InlineData("Consolas", "Consolas")]
    [InlineData("CONSOLAS", "Consolas")]
    [InlineData("serif", DesignLook.SerifFont)]
    [InlineData("Not Installed", DesignLook.SystemFont)]
    [InlineData("", DesignLook.SystemFont)]
    [InlineData(null, DesignLook.SystemFont)]
    public void S6441_the_ticked_entry_is_the_one_the_setting_names_or_the_system_font(string? current, string ticked)
        => Assert.Equal(ticked, FontSearch.Chosen(All, current));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void S6442_a_blank_query_keeps_the_whole_list(string? query)
    {
        var all = All;
        Assert.Same(all, FontSearch.Filter(all, query));
    }

    [Fact]
    public void S6442_matches_anywhere_ignoring_case_with_prefix_matches_first()
    {
        var hits = Labels(FontSearch.Filter(All, "SEG"));

        Assert.Equal(["System default", "Segoe Print", "Segoe UI", "Segoe UI Variable"], hits);
    }

    [Fact]
    public void S6442_name_prefix_ranks_above_word_prefix_above_anywhere()
    {
        Assert.Equal(["System default", "Consolas"], Labels(FontSearch.Filter(All, "cons")));

        // "Consolas" starts with it, "Fira Sans Condensed" has a word starting with it, "Iconic" just contains it.
        var con = Labels(FontSearch.Filter([.. All, new("Iconic", "Iconic")], "con"));
        Assert.Equal(["System default", "Consolas", "Fira Sans Condensed", "Iconic"], con);

        var mono = Labels(FontSearch.Filter(All, "mono"));
        Assert.Equal(["System default", "Monospace", "Cascadia Mono", "Noto Sans Mono"], mono);

        var ans = Labels(FontSearch.Filter(All, "ans"));
        Assert.Equal(["System default", "Sans serif", "Fira Sans Condensed", "Noto Sans Mono", "Señor Sans"], ans);
    }

    [Fact]
    public void S6442_the_word_start_is_found_after_an_earlier_mid_word_hit()
    {
        // "an" sits inside "Sans" before it starts "Anything".
        var hits = Labels(FontSearch.Filter([new("system", "System"), new("Mid", "Sans Anything"), new("Plain", "Banana")], "an"));

        Assert.Equal(["System", "Sans Anything", "Banana"], hits);
    }

    [Fact]
    public void S6442_accents_fold_both_ways()
    {
        Assert.Contains("Señor Sans", Labels(FontSearch.Filter(All, "senor")));
        Assert.Contains("Segoe UI", Labels(FontSearch.Filter([.. All, new("Ségoe", "Ségoe")], "SÉGOE UI")));
    }

    [Fact]
    public void S6442_spaces_may_be_left_out()
    {
        Assert.Equal(["System default", "Segoe UI", "Segoe UI Variable"], Labels(FontSearch.Filter(All, "segoeui")));
        Assert.Equal(["System default", "Cascadia Code"], Labels(FontSearch.Filter(All, "cascadiac")));
    }

    [Fact]
    public void S6442_the_system_entry_stays_on_top_when_nothing_else_matches()
    {
        Assert.Equal(["System default"], Labels(FontSearch.Filter(All, "zzz")));
    }

    [Fact]
    public void S6442_the_system_entry_is_not_duplicated_when_it_matches()
    {
        Assert.Equal(["System default"], Labels(FontSearch.Filter(All, "system")));
    }

    [Theory]
    [InlineData("cons", "Consolas")]
    [InlineData("seg", "Segoe Print")]
    [InlineData("system", DesignLook.SystemFont)]
    [InlineData("SYST", DesignLook.SystemFont)]
    public void S6442_enter_picks_the_best_match_not_the_pinned_default(string query, string value)
        => Assert.Equal(value, FontSearch.Best(All, query)?.Value);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("zzz")]
    public void S6442_enter_picks_nothing_for_a_blank_query_or_no_match(string query)
        => Assert.Null(FontSearch.Best(All, query));

    [Fact]
    public void S6440_a_family_reported_only_under_the_symbol_charset_is_a_symbol_font()
    {
        var fonts = InstalledFonts.From([("Wingdings", true), ("Segoe UI", false), ("Segoe UI", true), ("@Wingdings", true), ("Arial", false)]);

        Assert.Equal(["Arial", "Segoe UI", "Wingdings"], fonts.Families);
        Assert.Equal(["Wingdings"], fonts.Symbol);

        var options = FontSearch.Options(Presets, fonts, null);
        Assert.True(options.Single(o => o.Value == "Wingdings").Symbol);
        Assert.False(options.Single(o => o.Value == "Segoe UI").Symbol);
        Assert.All(Presets, p => Assert.False(options.Single(o => o.Value == p.Value).Symbol));
    }
}
