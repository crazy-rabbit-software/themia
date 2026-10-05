using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1520]..[S-1533] Apps, Search, Help and Themia: the rules that shape what they show, and their
/// ladders at the size tiers. Slots are the tier cards less the 44-DIP header where the widget
/// has one (Apps, Help) and the whole card where it starts headerless (Search, Themia).
/// </summary>
public sealed class AppWidgetsTests
{
    // Apps and Help: card minus the header.
    private static readonly WidgetArea XsH = new(120, 76);
    private static readonly WidgetArea SH = new(200, 106);
    private static readonly WidgetArea MH = new(320, 196);
    private static readonly WidgetArea WideH = new(480, 116);
    private static readonly WidgetArea TallH = new(200, 356);
    private static readonly WidgetArea LH = new(560, 376);
    private static readonly WidgetArea XlH = new(800, 516);

    // Search and Themia: headerless, the whole card.
    private static readonly WidgetArea Xs = new(120, 120);
    private static readonly WidgetArea M = new(320, 240);
    private static readonly WidgetArea Wide = new(480, 160);
    private static readonly WidgetArea Tall = new(200, 400);
    private static readonly WidgetArea L = new(560, 420);
    private static readonly WidgetArea Xl = new(800, 560);

    private static InstalledApp App(string name, string category = "") => new(name, category, $@"C:\Menu\{category}\{name}.lnk");

    // ---- Apps: config ---------------------------------------------------------------------------

    [Fact]
    public void S1520_apps_start_as_tiles_in_folders_opening_on_a_double_click()
    {
        var config = new AppsConfig();

        Assert.Equal(AppsViewMode.Grid, config.View);
        Assert.True(config.GroupByFolder);
        Assert.Equal(ItemClickMode.Double, config.ClickMode);
        Assert.Empty(config.HiddenApps);
        Assert.Empty(config.HiddenCategories);
    }

    [Fact]
    public void S1523_a_clone_does_not_share_the_hide_lists()
    {
        var config = new AppsConfig { HiddenApps = [@"C:\a.lnk"], HiddenCategories = ["Games"] };
        var clone = (AppsConfig)config.Clone();

        clone.HiddenApps.Add(@"C:\b.lnk");
        clone.HiddenCategories.Clear();

        Assert.Single(config.HiddenApps);
        Assert.Single(config.HiddenCategories);
    }

    [Fact]
    public void S1523_repair_drops_nulls_and_doubles_keeps_the_top_level_and_resets_unknown_enums()
    {
        var config = new AppsConfig
        {
            View = (AppsViewMode)42,
            ClickMode = (ItemClickMode)9,
            HiddenApps = [@"C:\a.lnk", null!, @"c:\A.LNK", "  "],
            HiddenCategories = ["", "Games", "games", null!],
        };

        var repairs = config.Repair();

        Assert.Equal(4, repairs.Count);
        Assert.Equal(AppsViewMode.Grid, config.View);
        Assert.Equal(ItemClickMode.Double, config.ClickMode);
        Assert.Equal([@"C:\a.lnk"], config.HiddenApps);
        Assert.Equal(["", "Games"], config.HiddenCategories);
    }

    [Fact]
    public void S1523_a_clean_config_needs_no_repair()
    {
        var config = new AppsConfig { HiddenApps = [@"C:\a.lnk"], HiddenCategories = [""] };
        Assert.Empty(config.Repair());
    }

    [Fact]
    public void S1523_null_lists_are_repaired_to_empty()
    {
        var config = new AppsConfig { HiddenApps = null!, HiddenCategories = null! };

        Assert.Equal(2, config.Repair().Count);
        Assert.Empty(config.HiddenApps);
        Assert.Empty(config.HiddenCategories);
    }

    // ---- Apps: what is shown --------------------------------------------------------------------

    [Fact]
    public void S1523_hidden_apps_and_folders_are_left_out_before_the_search_so_a_search_never_brings_them_back()
    {
        var apps = new List<InstalledApp> { App("Code", "Dev"), App("Chess", "Games"), App("Calc"), App("Clock") };

        var visible = InstalledAppRules.Visible(apps, [apps[3].Target.ToUpperInvariant()], ["games"], null, "c");

        Assert.Equal(["Code", "Calc"], visible.Select(a => a.Name));
        Assert.Equal(2, InstalledAppRules.HiddenCount(apps, [apps[3].Target], ["Games"]));
    }

    [Fact]
    public void S1527_a_folder_chip_narrows_to_that_folder_and_the_top_level_is_a_folder_too()
    {
        var apps = new List<InstalledApp> { App("Code", "Dev"), App("Calc"), App("Git", "dev") };

        Assert.Equal(["Code", "Git"], InstalledAppRules.Visible(apps, [], [], "Dev", null).Select(a => a.Name));
        Assert.Equal(["Calc"], InstalledAppRules.Visible(apps, [], [], "", null).Select(a => a.Name));
    }

    [Fact]
    public void S1521_sections_are_named_folders_in_order_then_the_top_level_as_other()
    {
        var apps = new List<InstalledApp> { App("Alpha"), App("Beta", "Zed"), App("Gamma", "accessories"), App("Delta", "zed") };

        var sections = InstalledAppRules.Sections(apps, group: true, Locale.En);

        Assert.Equal(["accessories", "Zed", "Other"], sections.Select(s => s.Title));
        Assert.Equal(["Beta", "Delta"], sections[1].Apps.Select(a => a.Name));
        Assert.Equal(string.Empty, sections[2].Category);
    }

    [Fact]
    public void S1521_not_grouping_is_one_untitled_section_and_nothing_is_no_sections()
    {
        var apps = new List<InstalledApp> { App("Alpha"), App("Beta", "Zed") };

        var flat = Assert.Single(InstalledAppRules.Sections(apps, group: false, Locale.En));
        Assert.Equal(string.Empty, flat.Title);
        Assert.Equal(2, flat.Apps.Count);
        Assert.Empty(InstalledAppRules.Sections([], group: true, Locale.En));
    }

    [Fact]
    public void S1524_categories_are_listed_once_in_section_order()
    {
        var apps = new List<InstalledApp> { App("A"), App("B", "Games"), App("C", "games"), App("D", "Accessories") };

        Assert.Equal(["Accessories", "Games", ""], InstalledAppRules.Categories(apps));
        Assert.Equal("Other", InstalledAppRules.CategoryTitle("", Locale.En));
        Assert.Equal("Sonstige", InstalledAppRules.CategoryTitle("", Locale.De));
        Assert.Equal("Games", InstalledAppRules.CategoryTitle("Games", Locale.De));
    }

    [Theory]
    [InlineData("Visual Studio Code", "VS")]
    [InlineData("notepad", "N")]
    [InlineData("  7-Zip   File Manager ", "7F")]
    [InlineData("Écran test", "ÉT")]
    [InlineData("", "?")]
    [InlineData("   ", "?")]
    public void S1522_initials_are_the_first_letters_of_up_to_two_words(string name, string expected)
        => Assert.Equal(expected, InstalledAppRules.Initials(name));

    [Theory]
    [InlineData("Visual Studio Code", 15)]
    [InlineData("Notepad", 247)]
    [InlineData("Microsoft Edge", 31)]
    [InlineData("7-Zip File Manager", 310)]
    [InlineData("A", 65)]
    [InlineData("", 0)]
    public void S1522_the_hue_is_the_originals_hash_so_an_app_keeps_its_colour(string name, int hue)
        => Assert.Equal(hue, InstalledAppRules.Hue(name));

    [Fact]
    public void S1522_a_long_name_wraps_the_hash_without_overflowing_the_range()
    {
        var hue = InstalledAppRules.Hue(new string('z', 500));
        Assert.InRange(hue, 0, 359);
    }

    // ---- Apps: keyboard ---------------------------------------------------------------------------

    [Theory]
    [InlineData(4, WidgetKey.Down, 6)] // second row, column 1 -> the short third row's column 1
    [InlineData(2, WidgetKey.Down, 4)] // column 2 -> the next row only has two: its last
    [InlineData(6, WidgetKey.Up, 4)]
    [InlineData(3, WidgetKey.Up, 0)]
    [InlineData(0, WidgetKey.Up, 0)]
    [InlineData(4, WidgetKey.Right, 5)]
    [InlineData(5, WidgetKey.Left, 4)]
    [InlineData(6, WidgetKey.Right, 6)]
    [InlineData(1, WidgetKey.PageDown, 6)]
    [InlineData(6, WidgetKey.PageUp, 1)]
    [InlineData(3, WidgetKey.Home, 0)]
    [InlineData(0, WidgetKey.End, 6)]
    public void S1526_the_cursor_walks_rows_of_sections_keeping_its_column(int from, WidgetKey key, int to)
    {
        // Sections of 5 and 2 in 3 columns: rows [0 1 2] [3 4] [5 6].
        var (index, used) = SectionCursor.Move([5, 2], 3, from, key);

        Assert.True(used);
        Assert.Equal(to, index);
    }

    [Fact]
    public void S1526_with_no_cursor_forward_lands_first_and_back_lands_last()
    {
        Assert.Equal((0, true), SectionCursor.Move([2, 3], 2, null, WidgetKey.Down));
        Assert.Equal((4, true), SectionCursor.Move([2, 3], 2, null, WidgetKey.Up));
        Assert.Equal((4, true), SectionCursor.Move([2, 3], 2, 99, WidgetKey.Left)); // gone: as none
    }

    [Fact]
    public void S1526_escape_clears_and_says_whether_there_was_a_cursor_and_other_keys_pass_on()
    {
        Assert.Equal((null, true), SectionCursor.Move([3], 1, 1, WidgetKey.Escape));
        Assert.Equal((null, false), SectionCursor.Move([3], 1, null, WidgetKey.Escape));
        Assert.Equal((1, false), SectionCursor.Move([3], 1, 1, WidgetKey.Enter));
        Assert.Equal((null, false), SectionCursor.Move([], 3, null, WidgetKey.Down));
        Assert.Equal((null, false), SectionCursor.Move([0, 0], 3, null, WidgetKey.Down));
    }

    // ---- Apps: the ladder -------------------------------------------------------------------------

    [Fact]
    public void S1527_the_filter_needs_a_regular_height_and_never_sits_on_a_strip()
    {
        Assert.False(AppsLayout.Filter(XsH));
        Assert.False(AppsLayout.Filter(SH));
        Assert.False(AppsLayout.Filter(WideH));
        Assert.True(AppsLayout.Filter(MH));
        Assert.True(AppsLayout.Filter(TallH));
        Assert.True(AppsLayout.Filter(LH));
    }

    [Fact]
    public void S1527_the_count_line_is_large_only_and_the_folder_chips_extra_large_only()
    {
        Assert.False(AppsLayout.Footer(MH));
        Assert.False(AppsLayout.Footer(TallH));
        Assert.True(AppsLayout.Footer(LH));
        Assert.False(AppsLayout.Chips(LH));
        Assert.True(AppsLayout.Chips(XlH));
    }

    [Fact]
    public void S1521_headings_need_two_sections_a_regular_height_and_not_compact_rows()
    {
        Assert.True(AppsLayout.Headers(MH, AppsViewMode.Grid, 2));
        Assert.True(AppsLayout.Headers(MH, AppsViewMode.List, 2));
        Assert.False(AppsLayout.Headers(MH, AppsViewMode.Compact, 2));
        Assert.False(AppsLayout.Headers(MH, AppsViewMode.Grid, 1));
        Assert.False(AppsLayout.Headers(WideH, AppsViewMode.Grid, 5));
        Assert.False(AppsLayout.Headers(XsH, AppsViewMode.Grid, 5));
    }

    [Fact]
    public void S1527_even_the_smallest_tile_shows_several_apps_and_a_strip_shows_a_row_of_them()
    {
        var xs = AppsLayout.Tiles(XsH, XsH.Width - 8, XsH.Height - 6, 200);
        Assert.True(xs.Columns * xs.Rows >= 2);

        var strip = AppsLayout.Tiles(WideH, WideH.Width - 8, WideH.Height - 6, 200);
        Assert.True(strip.Columns >= 5);
        Assert.True(strip.Labels);
    }

    [Fact]
    public void S1527_large_tiles_step_up_and_the_widest_list_runs_in_two_columns()
    {
        var m = AppsLayout.Tiles(MH, 300, 150, 200);
        var l = AppsLayout.Tiles(LH, 540, 300, 200);
        Assert.True(l.Icon > m.Icon);

        Assert.Equal(2, AppsLayout.Rows(XlH, AppsViewMode.List, headers: true).Columns);
        Assert.Equal(1, AppsLayout.Rows(MH, AppsViewMode.List, headers: true).Columns);
        Assert.Equal(2, AppsLayout.Rows(LH, AppsViewMode.List, headers: true).Columns);
        Assert.Equal(AppsLayout.MediumRow, AppsLayout.Rows(LH, AppsViewMode.List, headers: true).RowHeight);
    }

    [Fact]
    public void S1521_compact_rows_have_no_icon_and_show_the_folder_as_a_column_where_wide_enough()
    {
        var rows = AppsLayout.Rows(MH, AppsViewMode.Compact, headers: false);
        Assert.Equal(0, rows.Icon);
        Assert.False(rows.SecondLine);

        Assert.True(AppsLayout.CategoryColumn(MH, AppsViewMode.Compact, false, rows));
        Assert.False(AppsLayout.CategoryColumn(SH, AppsViewMode.Compact, false, AppsLayout.Rows(SH, AppsViewMode.Compact, false)));
        Assert.False(AppsLayout.CategoryColumn(MH, AppsViewMode.Grid, false, rows));
    }

    [Fact]
    public void S1521_a_large_list_says_the_folder_under_the_name_unless_the_headings_already_do()
    {
        Assert.True(AppsLayout.Rows(LH, AppsViewMode.List, headers: false).SecondLine);
        Assert.False(AppsLayout.Rows(LH, AppsViewMode.List, headers: true).SecondLine);
    }

    // ---- Search -----------------------------------------------------------------------------------

    [Fact]
    public void S1529_engine_chips_from_an_expanded_width_and_the_large_box_at_extra_large()
    {
        Assert.False(WebSearchLayout.Chips(Xs));
        Assert.False(WebSearchLayout.Chips(M));
        Assert.False(WebSearchLayout.Chips(Tall));
        Assert.True(WebSearchLayout.Chips(Wide));
        Assert.True(WebSearchLayout.Chips(L));
        Assert.False(WebSearchLayout.LargeBox(L));
        Assert.True(WebSearchLayout.LargeBox(Xl));
    }

    [Fact]
    public void S1529_more_recent_searches_on_larger_tiles()
    {
        Assert.Equal(5, WebSearchLayout.RecentWanted(M));
        Assert.Equal(8, WebSearchLayout.RecentWanted(Tall));
        Assert.Equal(8, WebSearchLayout.RecentWanted(L));
        Assert.Equal(RecentSearches.Capacity, WebSearchLayout.RecentWanted(Xl));
    }

    [Fact]
    public void S1530_recent_searches_are_trimmed_newest_first_without_doubles_and_capped()
    {
        var recent = new RecentSearches();
        var changes = 0;
        recent.Changed += () => changes++;

        Assert.False(recent.Add("   "));
        Assert.True(recent.Add(" cats "));
        recent.Add("dogs");
        recent.Add("CATS");

        Assert.Equal(["CATS", "dogs"], recent.Items);
        Assert.Equal(3, changes);

        for (var i = 0; i < 20; i++)
        {
            recent.Add($"q{i}");
        }

        Assert.Equal(RecentSearches.Capacity, recent.Items.Count);
        Assert.Equal("q19", recent.Items[0]);
    }

    [Fact]
    public void S1530_remove_and_clear_say_whether_anything_changed()
    {
        var recent = new RecentSearches();
        recent.Add("a");
        recent.Add("b");

        Assert.True(recent.Remove("A"));
        Assert.False(recent.Remove("a"));
        Assert.True(recent.Clear());
        Assert.False(recent.Clear());
        Assert.Empty(recent.Items);
    }

    [Fact]
    public void S1530_up_and_down_walk_the_list_like_a_shell_history()
    {
        var recent = new RecentSearches();
        Assert.Null(recent.Step(null, older: true));

        recent.Add("old");
        recent.Add("new");

        Assert.Equal(0, recent.Step(null, older: true));
        Assert.Equal(1, recent.Step(0, older: true));
        Assert.Equal(1, recent.Step(1, older: true));
        Assert.Equal(0, recent.Step(1, older: false));
        Assert.Null(recent.Step(0, older: false));
        Assert.Null(recent.Step(null, older: false));
    }

    // ---- Help -------------------------------------------------------------------------------------

    [Fact]
    public void S1531_help_arranges_its_pages_by_width()
    {
        Assert.Equal(HelpArrangement.PageOnly, HelpLayout.Arrangement(XsH));
        Assert.Equal(HelpArrangement.IconTabs, HelpLayout.Arrangement(SH));
        Assert.Equal(HelpArrangement.IconTabs, HelpLayout.Arrangement(MH));
        Assert.Equal(HelpArrangement.IconTabs, HelpLayout.Arrangement(TallH));
        Assert.Equal(HelpArrangement.TwoPane, HelpLayout.Arrangement(WideH));
        Assert.Equal(HelpArrangement.TwoPane, HelpLayout.Arrangement(LH));
        Assert.Equal(HelpArrangement.TwoPane, HelpLayout.Arrangement(XlH));
    }

    [Fact]
    public void S1531_the_buttons_come_at_large_and_the_keys_card_at_extra_large()
    {
        Assert.False(HelpLayout.Actions(WideH));
        Assert.True(HelpLayout.Actions(LH));
        Assert.False(HelpLayout.Keys(LH));
        Assert.True(HelpLayout.Keys(XlH));
    }

    [Fact]
    public void S1531_prose_is_body_wherever_the_page_keeps_a_readable_width()
    {
        Assert.False(HelpLayout.BodyProse(XsH));
        Assert.False(HelpLayout.BodyProse(SH));
        Assert.True(HelpLayout.BodyProse(MH));
        Assert.True(HelpLayout.BodyProse(WideH));
        Assert.True(HelpLayout.BodyProse(LH));
        Assert.True(HelpLayout.BodyProse(XlH));
        Assert.InRange(HelpLayout.PageWidth(XlH), 360, 480);
    }

    [Theory]
    [InlineData(HelpTab.Widgets, "Type to search")]
    [InlineData(HelpTab.Widgets, "follows the pointer")]
    [InlineData(HelpTab.Widgets, "Ctrl+click")]
    [InlineData(HelpTab.Widgets, "drag a box around them")]
    [InlineData(HelpTab.Widgets, "Bring to front")]
    [InlineData(HelpTab.Widgets, "Send to back")]
    [InlineData(HelpTab.Widgets, "Group")]
    [InlineData(HelpTab.Widgets, "Stack")]
    [InlineData(HelpTab.Widgets, "Tabs")]
    [InlineData(HelpTab.Widgets, "Move into")]
    [InlineData(HelpTab.Basics, "hands the keyboard back")]
    [InlineData(HelpTab.Views, "view bar")]
    [InlineData(HelpTab.Customize, "Find a setting")]
    public void S1532_the_guide_describes_the_app_as_it_is(HelpTab tab, string phrase)
    {
        var page = HelpGuide.Page(tab, Locale.En);
        var text = string.Join(" ", new[] { page.Intro }.Concat(page.Sections.SelectMany(s => s.Bullets)));

        Assert.Contains(phrase, text, StringComparison.Ordinal);
    }

    [Fact]
    public void S1531_the_keys_card_starts_with_edit_mode_and_every_key_says_what_it_does()
    {
        Assert.Equal("Ctrl+Alt+W", HelpGuide.KeysIn(Locale.En)[0].Keys);
        Assert.All(HelpGuide.KeysIn(Locale.En), key =>
        {
            Assert.False(string.IsNullOrWhiteSpace(key.Meaning));
            Assert.DoesNotContain('*', key.Keys + key.Meaning);
        });
    }

    // ---- Themia -----------------------------------------------------------------------------------

    [Fact]
    public void S1533_the_mark_sits_beside_the_words_on_a_strip_and_large_tiles_show_more_not_a_bigger_logo()
    {
        Assert.True(ThemiaLayout.Beside(Wide));
        Assert.False(ThemiaLayout.Beside(Xs));
        Assert.False(ThemiaLayout.Beside(Tall));
        Assert.False(ThemiaLayout.About(M));
        Assert.True(ThemiaLayout.About(L));
        Assert.True(ThemiaLayout.Actions(L));
        Assert.True(ThemiaLayout.About(Tall));
        Assert.False(ThemiaLayout.Actions(Tall));
        Assert.False(ThemiaLayout.Links(L));
        Assert.True(ThemiaLayout.Links(Xl));
        Assert.False(ThemiaLayout.Beside(L));
        Assert.True(ThemiaLayout.Beside(Xl));
        Assert.True(ThemiaLayout.Mark(Xl) > ThemiaLayout.Mark(L));
    }
}
