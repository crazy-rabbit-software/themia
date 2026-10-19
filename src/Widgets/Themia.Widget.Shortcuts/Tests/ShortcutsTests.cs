using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Shortcuts.Tests;

/// <summary>Shortcuts widget: config, ladder, selection and rules (S-1380..S-1393).</summary>
public sealed class ShortcutsTests
{
    // ---- config ------------------------------------------------------------------------------------

    [Fact]
    public void S1380_defaults_are_tiles_medium_double_click()
    {
        var config = new ShortcutsConfig();

        Assert.Equal(ShortcutsStyle.Grid, config.Style);
        Assert.Equal(ShortcutItemSize.Medium, config.GridSize);
        Assert.Equal(ShortcutItemSize.Medium, config.ListSize);
        Assert.Equal(ItemClickMode.Double, config.ClickMode);
        Assert.False(config.GridIconOnly);
        Assert.False(config.GridStretchHeight);
        Assert.False(config.ShowFileExtension);
    }

    [Fact]
    public void S1380_repair_brings_unknown_enums_back_and_mends_ids()
    {
        var config = new ShortcutsConfig
        {
            GridSize = (ShortcutItemSize)9,
            ListSize = (ShortcutItemSize)(-1),
            ClickMode = (ItemClickMode)5,
            Items =
            [
                new ShortcutItem { Id = "a", Target = "https://a.example/" },
                new ShortcutItem { Id = "a", Target = "https://b.example/" },
                new ShortcutItem { Id = " ", Target = "https://c.example/" },
            ],
        };

        var repairs = config.Repair();

        Assert.NotEmpty(repairs);
        Assert.Equal(ShortcutItemSize.Medium, config.GridSize);
        Assert.Equal(ShortcutItemSize.Medium, config.ListSize);
        Assert.Equal(ItemClickMode.Double, config.ClickMode);
        Assert.Equal(3, config.Items.Select(i => i.Id).Distinct().Count());
        Assert.Equal("a", config.Items[0].Id);
    }

    [Fact]
    public void S1380_repair_drops_null_items_and_a_null_list()
    {
        var config = new ShortcutsConfig { Items = null! };
        config.Repair();
        Assert.Empty(config.Items);

        config.Items = [null!, ShortcutItem.Create("https://example.com/")];
        var repairs = config.Repair();
        Assert.Single(config.Items);
        Assert.Contains(repairs, r => r.Contains("broken item", StringComparison.Ordinal));
    }

    [Fact]
    public void S1380_clone_copies_every_display_setting()
    {
        var config = new ShortcutsConfig
        {
            Style = ShortcutsStyle.List,
            GridSize = ShortcutItemSize.Small,
            ListSize = ShortcutItemSize.Large,
            GridIconOnly = true,
            GridStretchHeight = true,
            ShowFileExtension = true,
            ClickMode = ItemClickMode.Single,
        };

        var copy = Assert.IsType<ShortcutsConfig>(config.Clone());

        Assert.Equal(config.Style, copy.Style);
        Assert.Equal(config.GridSize, copy.GridSize);
        Assert.Equal(config.ListSize, copy.ListSize);
        Assert.True(copy.GridIconOnly);
        Assert.True(copy.GridStretchHeight);
        Assert.True(copy.ShowFileExtension);
        Assert.Equal(ItemClickMode.Single, copy.ClickMode);
    }

    [Fact]
    public void S1393_a_new_widget_starts_with_the_recycle_bin_and_this_pc()
    {
        var config = Assert.IsType<ShortcutsConfig>(WidgetConfig.CreateDefault(new WidgetId("shortcuts")));

        Assert.Equal(["shell:RecycleBinFolder", "shell:MyComputerFolder"], config.Items.Select(i => i.Target));
        Assert.All(config.Items, item => Assert.True(LaunchTargets.Classify(item.Target).IsValid));
    }

    // ---- names and probing -------------------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\Docs\report.pdf", false, "report")]
    [InlineData(@"C:\Docs\report.pdf", true, "report.pdf")]
    [InlineData(@"C:\Docs\archive.tar.gz", true, "archive.tar.gz")]
    [InlineData(@"C:\Docs", true, "Docs")]
    [InlineData("https://www.example.com/x", true, "example.com")]
    [InlineData("shell:Downloads", true, "Downloads")]
    public void S1381_file_names_keep_their_extension_only_when_asked(string target, bool show, string expected)
        => Assert.Equal(expected, LaunchTargets.DisplayName(null, target, show));

    [Fact]
    public void S1381_a_label_wins_over_the_extension_setting()
        => Assert.Equal("Mine", LaunchTargets.DisplayName("Mine", @"C:\Docs\report.pdf", showExtension: true));

    [Theory]
    [InlineData(@"C:\Windows\notepad.exe", true)]
    [InlineData("shell:RecycleBinFolder", true)]
    [InlineData(@"\\server\share\file.txt", false)]
    [InlineData("https://example.com/", false)]
    [InlineData("javascript:alert(1)", false)]
    public void S1386_network_paths_web_and_refused_targets_are_never_probed(string target, bool expected)
        => Assert.Equal(expected, LaunchTargets.MayProbe(LaunchTargets.Classify(target)));

    [Fact]
    public void S1386_only_probeable_local_paths_are_checked()
    {
        var asked = new List<string>();
        TargetHealth Probe(string path)
        {
            asked.Add(path);
            return TargetHealth.Missing;
        }

        Assert.Equal(TargetHealth.Missing, ShortcutRules.Check(LaunchTargets.Classify(@"C:\gone.txt"), Probe));
        Assert.Equal(TargetHealth.Unknown, ShortcutRules.Check(LaunchTargets.Classify(@"\\evil\share\x"), Probe));
        Assert.Equal(TargetHealth.Unknown, ShortcutRules.Check(LaunchTargets.Classify("https://example.com/"), Probe));
        Assert.Equal(TargetHealth.Unknown, ShortcutRules.Check(LaunchTargets.Classify("shell:Downloads"), Probe));

        Assert.Equal([@"C:\gone.txt"], asked);
    }

    [Fact]
    public void S1384_kind_column_names_what_a_target_is()
    {
        Assert.Equal("Web address", ShortcutRules.KindLabel(Locale.En, LaunchTargets.Classify("https://a.example/"), TargetHealth.Unknown));
        Assert.Equal("Windows location", ShortcutRules.KindLabel(Locale.En, LaunchTargets.Classify("shell:Desktop"), TargetHealth.Unknown));
        Assert.Equal("Folder", ShortcutRules.KindLabel(Locale.En, LaunchTargets.Classify(@"C:\Windows"), TargetHealth.Folder));
        Assert.Equal("File", ShortcutRules.KindLabel(Locale.En, LaunchTargets.Classify(@"C:\x.txt"), TargetHealth.File));
        Assert.Equal("Path", ShortcutRules.KindLabel(Locale.En, LaunchTargets.Classify(@"C:\x.txt"), TargetHealth.Unknown));
        Assert.Equal("Missing", ShortcutRules.KindLabel(Locale.En, LaunchTargets.Classify(@"C:\x.txt"), TargetHealth.Missing));
        Assert.Equal("Not allowed", ShortcutRules.KindLabel(Locale.En, LaunchTargets.Classify("ftp://a"), TargetHealth.Unknown));
        Assert.Equal("App layout", ShortcutRules.LayoutKindLabel(Locale.En));
    }

    [Fact]
    public void S1384_kind_column_follows_the_language()
    {
        Assert.Equal("Webadresse", ShortcutRules.KindLabel(Locale.De, LaunchTargets.Classify("https://a.example/"), TargetHealth.Unknown));
        Assert.Equal("Ordner", ShortcutRules.KindLabel(Locale.De, LaunchTargets.Classify(@"C:\Windows"), TargetHealth.Folder));
        Assert.Equal("Fehlt", ShortcutRules.KindLabel(Locale.De, LaunchTargets.Classify(@"C:\x.txt"), TargetHealth.Missing));
        Assert.Equal("App-Layout", ShortcutRules.LayoutKindLabel(Locale.De));
    }

    // ---- search ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("GitHub", "https://github.com/", "git", true)]
    [InlineData("GitHub", "https://github.com/", "HUB", true)]
    [InlineData("Work", @"C:\Projects\alpha", "alpha", true)]
    [InlineData("Work", @"C:\Projects\alpha", "beta", false)]
    [InlineData("Work", @"C:\Projects\alpha", "  ", true)]
    [InlineData("Work", @"C:\Projects\alpha", null, true)]
    public void S1385_search_matches_name_or_target_ignoring_case(string name, string target, string? query, bool expected)
        => Assert.Equal(expected, ShortcutRules.Matches(name, target, query));

    // ---- item menu and moves -----------------------------------------------------------------------

    private static List<ShortcutCommand> Commands(IReadOnlyList<ShortcutMenuEntry> menu)
        => menu.Where(e => e.Command is not null).Select(e => e.Command!.Value).ToList();

    [Fact]
    public void S1387_a_web_item_offers_copy_url_and_a_file_its_location()
    {
        var web = Commands(ShortcutRules.Menu(LaunchTargets.Classify("https://a.example/"), 1));
        var file = Commands(ShortcutRules.Menu(LaunchTargets.Classify(@"C:\a.txt"), 1));
        var shell = Commands(ShortcutRules.Menu(LaunchTargets.Classify("shell:Desktop"), 1));

        Assert.Equal(ShortcutCommand.Open, web[0]);
        Assert.Contains(ShortcutCommand.CopyUrl, web);
        Assert.DoesNotContain(ShortcutCommand.CopyPath, web);
        Assert.Contains(ShortcutCommand.OpenFileLocation, file);
        Assert.Contains(ShortcutCommand.CopyPath, file);
        Assert.Contains(ShortcutCommand.CopyPath, shell);
        Assert.DoesNotContain(ShortcutCommand.OpenFileLocation, shell);
        Assert.Equal([ShortcutCommand.Rename, ShortcutCommand.Remove], web.TakeLast(2));
    }

    [Fact]
    public void S1387_the_menu_has_no_moves_and_open_is_off_for_a_refused_target()
    {
        var middle = ShortcutRules.Menu(LaunchTargets.Classify("https://a.example/"), 1);
        var refused = ShortcutRules.Menu(LaunchTargets.Classify("javascript:x"), 1);

        // [S-3802] The order is dragged, or moved with Alt+Shift+Up / Down: no menu has arrows.
        Assert.Equal([ShortcutCommand.Open, ShortcutCommand.CopyUrl, ShortcutCommand.Rename, ShortcutCommand.Remove], Commands(middle));
        Assert.True(refused.Single(e => e.Command == ShortcutCommand.Open).Disabled);
        Assert.False(refused.Single(e => e.Command == ShortcutCommand.Remove).Disabled);
    }

    [Fact]
    public void S4864_the_recycle_bin_item_offers_empty_only_while_it_can_be_emptied()
    {
        var bin = LaunchTargets.Classify("shell:RecycleBinFolder");
        var full = ShortcutRules.Menu(bin, 1, layout: null, canEmptyBin: true);
        var empty = ShortcutRules.Menu(bin, 1, layout: null, canEmptyBin: false);
        var desktop = Commands(ShortcutRules.Menu(LaunchTargets.Classify("shell:Desktop"), 1, layout: null, canEmptyBin: true));
        var many = Commands(ShortcutRules.Menu(bin, 2, layout: null, canEmptyBin: true));

        Assert.Equal(
            [ShortcutCommand.Open, ShortcutCommand.EmptyRecycleBin, ShortcutCommand.CopyPath, ShortcutCommand.Rename, ShortcutCommand.Remove],
            Commands(full));
        Assert.False(full.Single(e => e.Command == ShortcutCommand.EmptyRecycleBin).Disabled);
        Assert.True(empty.Single(e => e.Command == ShortcutCommand.EmptyRecycleBin).Disabled);
        Assert.DoesNotContain(ShortcutCommand.EmptyRecycleBin, desktop);
        Assert.DoesNotContain(ShortcutCommand.EmptyRecycleBin, many);
    }

    [Fact]
    public void S1387_a_multi_selection_gets_open_all_and_no_rename()
    {
        var menu = ShortcutRules.Menu(LaunchTargets.Classify("https://a.example/"), 3);

        Assert.Equal([ShortcutCommand.OpenAll, ShortcutCommand.Rename, ShortcutCommand.Remove], Commands(menu));
        Assert.True(menu.Single(e => e.Command == ShortcutCommand.Rename).Disabled);
    }

    // ---- Windows-location switches -----------------------------------------------------------------

    [Fact]
    public void S1388_switching_a_location_on_adds_it_once_and_off_removes_every_copy()
    {
        var items = new List<ShortcutItem> { ShortcutItem.Create("https://a.example/") };

        Assert.True(ShortcutRules.SetLocation(items, "Downloads", on: true));
        Assert.False(ShortcutRules.SetLocation(items, "Downloads", on: true));
        Assert.True(ShortcutRules.HasLocation(items, "Downloads"));

        items.Add(ShortcutItem.Create("SHELL:downloads", "My downloads"));

        Assert.True(ShortcutRules.SetLocation(items, "Downloads", on: false));
        Assert.False(ShortcutRules.HasLocation(items, "Downloads"));
        Assert.Single(items);
        Assert.False(ShortcutRules.SetLocation(items, "Downloads", on: false));
    }

    // ---- Recycle Bin badge -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "1")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    public void S1389_badge_counts_items_and_caps_at_99(long items, string? expected)
        => Assert.Equal(expected, ShortcutRules.BadgeText(new RecycleBinState(items, items * 1024)));

    [Fact]
    public void S1389_unknown_state_has_no_badge_and_the_plain_name()
    {
        Assert.Null(ShortcutRules.BadgeText(null));
        Assert.Equal("Recycle Bin", RecycleBinState.Tooltip(Locale.En, "Recycle Bin", null));
        Assert.Equal("Recycle Bin — empty", RecycleBinState.Tooltip(Locale.En, "Recycle Bin", new RecycleBinState(0, 0)));
        Assert.StartsWith("Recycle Bin — 1 item (", RecycleBinState.Tooltip(Locale.En, "Recycle Bin", new RecycleBinState(1, 2048)), StringComparison.Ordinal);
        Assert.StartsWith("Recycle Bin — 3 items (", RecycleBinState.Tooltip(Locale.En, "Recycle Bin", new RecycleBinState(3, 2048)), StringComparison.Ordinal);
    }

    [Fact]
    public void S1389_bin_tooltip_follows_the_language()
    {
        Assert.Equal("Papierkorb — leer", RecycleBinState.Tooltip(Locale.De, "Papierkorb", new RecycleBinState(0, 0)));
        Assert.StartsWith("Papierkorb — 1 Element (", RecycleBinState.Tooltip(Locale.De, "Papierkorb", new RecycleBinState(1, 2048)), StringComparison.Ordinal);
        Assert.StartsWith("Papierkorb — 3 Elemente (", RecycleBinState.Tooltip(Locale.De, "Papierkorb", new RecycleBinState(3, 2048)), StringComparison.Ordinal);
        Assert.StartsWith("Kosz — 5 elementów (", RecycleBinState.Tooltip(Locale.Pl, "Kosz", new RecycleBinState(5, 2048)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("shell:RecycleBinFolder", true)]
    [InlineData("SHELL:recyclebinfolder", true)]
    [InlineData("shell:MyComputerFolder", false)]
    [InlineData(@"C:\$Recycle.Bin", false)]
    public void S1389_only_the_bin_is_the_bin(string target, bool expected)
        => Assert.Equal(expected, ShortcutRules.IsRecycleBin(target));

    // ---- the grid ladder ---------------------------------------------------------------------------

    /// <summary>The size tiers' slots (card less the ~44 header), less the grid inset (8 / 4+8).</summary>
    public static TheoryData<string, double, double> Tiers => new()
    {
        { "XS", 120 - 16, 76 - 12 },
        { "S", 200 - 16, 106 - 12 },
        { "M", 320 - 16, 196 - 12 },
        { "Wide", 480 - 16, 116 - 12 },
        { "Tall", 200 - 16, 356 - 12 },
        { "L", 560 - 16, 376 - 12 },
        { "XL", 800 - 16, 516 - 12 },
    };

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S1382_every_tier_shows_at_least_four_items_or_all_of_them(string tier, double width, double height)
    {
        foreach (var count in new[] { 1, 2, 5, 12 })
        {
            var plan = ShortcutLayout.Grid(width, height, count, ShortcutItemSize.Medium, iconOnly: false, stretch: false, detail: false);
            var shown = Math.Min(count, plan.Columns * plan.Rows);

            Assert.True(shown >= Math.Min(count, ShortcutLayout.KeepVisible), $"{tier}: {count} items, {plan}");
            Assert.True(plan.Columns * plan.TileWidth + (plan.Columns - 1) * ShortcutLayout.Gap <= width + 0.5, $"{tier} overflows its width");
            Assert.True(plan.TileHeight <= height + 0.5 || plan.Rows == 1, $"{tier} tile taller than the box");
        }
    }

    [Fact]
    public void S1382_xs_sheds_names_before_hiding_items()
    {
        var plan = ShortcutLayout.Grid(104, 64, 5, ShortcutItemSize.Medium, false, false, false);

        Assert.False(plan.Labels);
        Assert.True(plan.Columns * plan.Rows >= 4);
    }

    [Fact]
    public void S1382_names_stay_when_there_are_few_items()
    {
        var plan = ShortcutLayout.Grid(104, 64, 1, ShortcutItemSize.Medium, false, false, false);

        Assert.True(plan.Labels);
        Assert.Equal(1, plan.Columns);
    }

    [Fact]
    public void S1382_a_wide_strip_is_one_row_of_many_named_tiles()
    {
        var plan = ShortcutLayout.Grid(464, 104, 12, ShortcutItemSize.Medium, false, false, false);

        Assert.True(plan.Labels);
        Assert.Equal(1, plan.Rows);
        Assert.True(plan.Columns >= 5);
    }

    [Fact]
    public void S1382_icon_only_never_shows_names()
    {
        var plan = ShortcutLayout.Grid(784, 504, 5, ShortcutItemSize.Large, iconOnly: true, stretch: false, detail: true);

        Assert.False(plan.Labels);
        Assert.False(plan.Detail);
        Assert.Equal(ShortcutLayout.TileIcon(ShortcutItemSize.Large), plan.Icon);
    }

    [Fact]
    public void S1382_large_tiers_step_the_tiles_up_and_xl_adds_the_detail_line()
    {
        var l = new WidgetArea(560, 376);
        var xl = new WidgetArea(800, 516);
        var m = new WidgetArea(320, 196);

        Assert.Equal(ShortcutItemSize.Large, ShortcutLayout.GridSize(ShortcutItemSize.Medium, l));
        Assert.Equal(ShortcutItemSize.Large, ShortcutLayout.GridSize(ShortcutItemSize.Large, xl));
        Assert.Equal(ShortcutItemSize.Medium, ShortcutLayout.GridSize(ShortcutItemSize.Medium, m));
        Assert.False(ShortcutLayout.GridDetail(l));
        Assert.True(ShortcutLayout.GridDetail(xl));

        var plan = ShortcutLayout.Grid(784, 504, 12, ShortcutItemSize.Large, false, false, detail: true);
        Assert.True(plan.Detail);
        Assert.Equal(48, plan.Icon);
    }

    [Fact]
    public void S1382_stretch_shares_the_height_only_when_every_row_fits()
    {
        var fits = ShortcutLayout.Grid(304, 184, 4, ShortcutItemSize.Medium, false, stretch: true, false);
        var plain = ShortcutLayout.Grid(304, 184, 4, ShortcutItemSize.Medium, false, stretch: false, false);
        var overflows = ShortcutLayout.Grid(304, 184, 40, ShortcutItemSize.Medium, false, stretch: true, false);

        Assert.True(fits.TileHeight > plain.TileHeight);
        Assert.Equal(plain.TileHeight, overflows.TileHeight);

        var rows = (int)Math.Ceiling(4 / (double)fits.Columns);
        Assert.InRange(rows * fits.TileHeight + (rows - 1) * ShortcutLayout.Gap, 183.5, 184.5);
    }

    [Fact]
    public void S1382_a_grid_that_fits_keeps_its_columns_from_the_top_left()
    {
        // Two items on an XL tile: the plan keeps every column at its shared width (the items take
        // the first two from the left, the rest stays empty), never a narrowed, centred group.
        var few = ShortcutLayout.Grid(784, 504, 2, ShortcutItemSize.Large, false, false, true);

        Assert.True(few.Columns > 2);
        Assert.InRange(few.Columns * few.TileWidth + (few.Columns - 1) * ShortcutLayout.Gap, 783.5, 784.5);
    }

    [Fact]
    public void S1382_columns_share_out_the_width()
    {
        var plan = ShortcutLayout.Grid(304, 184, 6, ShortcutItemSize.Medium, false, false, false);

        Assert.InRange(plan.Columns * plan.TileWidth + (plan.Columns - 1) * ShortcutLayout.Gap, 303.5, 304.5);
    }

    // ---- the list / details ladder -----------------------------------------------------------------

    [Fact]
    public void S1383_rows_step_down_on_short_tiles_and_gain_a_second_line_on_large_ones()
    {
        var xs = ShortcutLayout.Rows(new WidgetArea(120, 76), ShortcutItemSize.Medium, details: false);
        var m = ShortcutLayout.Rows(new WidgetArea(320, 196), ShortcutItemSize.Medium, details: false);
        var l = ShortcutLayout.Rows(new WidgetArea(560, 376), ShortcutItemSize.Medium, details: false);
        var xl = ShortcutLayout.Rows(new WidgetArea(800, 516), ShortcutItemSize.Medium, details: false);

        Assert.Equal(32, xs.RowHeight);
        Assert.Equal(40, m.RowHeight);
        Assert.False(m.SecondLine);
        Assert.True(l.SecondLine);
        Assert.Equal(1, l.Columns);
        Assert.Equal(1, m.Columns);
        Assert.Equal(2, xl.Columns);
        Assert.Equal(2, ShortcutLayout.Rows(new WidgetArea(480, 116), ShortcutItemSize.Medium, details: false).Columns);
    }

    [Fact]
    public void S1384_details_adds_target_then_kind_then_a_header_as_it_grows()
    {
        var m = ShortcutLayout.Rows(new WidgetArea(320, 196), ShortcutItemSize.Medium, details: true);
        var wide = ShortcutLayout.Rows(new WidgetArea(480, 116), ShortcutItemSize.Medium, details: true);
        var l = ShortcutLayout.Rows(new WidgetArea(560, 376), ShortcutItemSize.Medium, details: true);
        var xl = ShortcutLayout.Rows(new WidgetArea(800, 516), ShortcutItemSize.Medium, details: true);

        Assert.False(m.TargetColumn);
        Assert.True(wide.TargetColumn);
        Assert.False(wide.ColumnHeader);
        Assert.True(l.TargetColumn);
        Assert.True(l.ColumnHeader);
        Assert.False(l.KindColumn);
        Assert.True(xl.KindColumn);
        Assert.Equal(1, xl.Columns);
        Assert.False(xl.SecondLine);
    }
}
