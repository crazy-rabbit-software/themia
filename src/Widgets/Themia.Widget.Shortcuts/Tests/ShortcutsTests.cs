using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
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
        Assert.Equal(ItemClickMode.Double, config.ClickMode);
        Assert.False(config.GridIconOnly);
        Assert.False(config.ShowFileExtension);
    }

    [Fact]
    public void S1380_repair_brings_unknown_enums_back_and_mends_ids()
    {
        var config = new ShortcutsConfig
        {
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
            GridIconOnly = true,
            ShowFileExtension = true,
            ClickMode = ItemClickMode.Single,
        };

        var copy = Assert.IsType<ShortcutsConfig>(config.Clone());

        Assert.Equal(config.Style, copy.Style);
        Assert.True(copy.GridIconOnly);
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

    // ---- the sizing --------------------------------------------------------------------------------

    private static ShortcutsConfig With(int count, ShortcutsStyle style = ShortcutsStyle.Grid)
    {
        var config = new ShortcutsConfig { Style = style };
        for (var i = 0; i < count; i++)
        {
            config.Items.Add(ShortcutItem.Create($"https://example{i}.com"));
        }

        return config;
    }

    [Fact]
    public void S11202_the_grid_is_its_columns_wide_and_its_shortcuts_rows_tall()
    {
        // Four columns of 32 icons named (64 each, 12 apart, the insets): 12 + 4 x 76; a row 52 + 12.
        var two = ShortcutLayout.Sizing(With(2));
        Assert.Equal(new SizeDip(12 + (4 * 76), 36 + 64), two.Natural);
        Assert.True(two.FixedHeight);
        Assert.Equal(4, two.WidthSteps!.Count);
        Assert.Equal(36 + (3 * 64), ShortcutLayout.Sizing(With(9)).Natural.Height);
        Assert.Equal(36 + (ShortcutLayout.MaxGridRows * 64), ShortcutLayout.Sizing(With(60)).Natural.Height);
        Assert.Equal(36 + 64, ShortcutLayout.Sizing(With(0)).Natural.Height);

        var large = With(9);
        large.IconSize = ShortcutsIconSize.Large;
        large.Columns = 3;
        Assert.Equal(new SizeDip(12 + (3 * 92), 36 + (3 * 80)), ShortcutLayout.Sizing(large).Natural);
    }

    [Fact]
    public void S11202_dragging_the_grids_side_sets_its_columns_on_a_copy_that_keeps_the_ids()
    {
        var config = With(6);
        var next = Assert.IsType<ShortcutsConfig>(ShortcutLayout.SizeSets(config, new SizeDip(12 + (2 * 76), 500)));

        Assert.Equal(2, next.Columns);
        Assert.Equal(4, config.Columns);
        Assert.Equal(config.Items.Select(i => i.Id), next.Items.Select(i => i.Id));
        Assert.Same(config, ShortcutLayout.SizeSets(config, new SizeDip(12 + (4 * 76), 10)));

        var list = With(6, ShortcutsStyle.List);
        Assert.Same(list, ShortcutLayout.SizeSets(list, new SizeDip(200, 200)));
    }

    [Fact]
    public void S11202_a_list_is_one_row_per_shortcut_up_to_twelve_and_its_lines_say_the_target_line()
    {
        Assert.Equal(36 + (5 * 40) + 12, ShortcutLayout.Sizing(With(5, ShortcutsStyle.List)).Natural.Height);
        Assert.Equal(36 + (12 * 40) + 12, ShortcutLayout.Sizing(With(30, ShortcutsStyle.List)).Natural.Height);

        var one = With(5, ShortcutsStyle.List);
        one.Lines = 1;
        Assert.Equal((28.0, 16.0), (ShortcutLayout.Rows(one).RowHeight, ShortcutLayout.Rows(one).Lead));
        Assert.Equal(36 + (5 * 28) + 12, ShortcutLayout.Sizing(one).Natural.Height);
        Assert.True(ShortcutLayout.Sizing(one).FixedHeight);
    }

    [Fact]
    public void S11202_the_details_columns_are_settings_and_its_least_width_holds_them()
    {
        var details = With(3, ShortcutsStyle.Details);
        var plan = ShortcutLayout.Rows(details);
        Assert.True(plan.TargetColumn && !plan.KindColumn && plan.ColumnHeader);
        Assert.Equal(36 + 24 + (3 * 28) + 12, ShortcutLayout.Sizing(details).Natural.Height);

        details.ShowKind = true;
        var both = ShortcutLayout.Sizing(details);
        Assert.Equal(24 + 24 + 120 + 12 + 160 + 12 + 120, both.Min.Width);

        details.ShowTarget = details.ShowKind = false;
        Assert.False(ShortcutLayout.Rows(details).ColumnHeader);
        Assert.Equal(36 + (3 * 28) + 12, ShortcutLayout.Sizing(details).Natural.Height);
    }

    [Fact]
    public void S11202_out_of_range_cells_are_repaired_and_a_copy_keeps_the_settings()
    {
        var config = new ShortcutsConfig { Columns = 30, Lines = 5, IconSize = (ShortcutsIconSize)9, ShowKind = true, ShowTarget = false };
        Assert.NotEmpty(config.Repair());
        Assert.Equal((8, 2, ShortcutsIconSize.Small), (config.Columns, config.Lines, config.IconSize));

        var copy = (ShortcutsConfig)config.Clone();
        Assert.Equal((8, 2, true, false), (copy.Columns, copy.Lines, copy.ShowKind, copy.ShowTarget));
    }

    [Fact]
    public void S11202_the_grid_shares_the_box_out_between_its_columns_never_below_a_cells_least()
    {
        var grid = ShortcutLayout.Grid(With(6), 6, 400);
        Assert.Equal((4, 2), (grid.Columns, grid.Rows));
        Assert.InRange((grid.Columns * grid.Width) + (3 * FamilySpace.S3), 399.5, 400.5);
        Assert.Equal(64, ShortcutLayout.Grid(With(6), 6, 10).Width);
        Assert.False(ShortcutLayout.Grid(new ShortcutsConfig { GridIconOnly = true }, 1, 300).Named);
    }
}
