using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11700]..[S-11709] The add page: its tabs, its sections and tiles, the search, the layout and the
/// keys. Here, not in Core's tests, because the page is built from every widget of the build and every
/// ready-made group, and only this host has them all.
/// </summary>
public sealed class AddPageTests
{
    private static readonly WidgetId[] Every = [.. ModuleCatalog.Widgets.Select(w => w.Id)];

    private static AddPage Page => AddPage.Of(Every, ReadyMadeGroups.Offered());

    private static IEnumerable<AddTile> AllTiles(AddPage page, bool withStart)
        => page.Tabs.Where(t => withStart || t.Id != AddFolderId.Start).SelectMany(t => page.Sections(t.Id)).SelectMany(s => s.Tiles);

    [Fact]
    public void S11700_start_first_then_every_subject_in_browses_order_and_no_more_when_every_kind_has_a_home()
    {
        var page = Page;

        AddFolderId[] subjects = [AddFolderId.Time, AddFolderId.CalendarMail, AddFolderId.Weather, AddFolderId.Markets, AddFolderId.Media,
            AddFolderId.Notes, AddFolderId.Files, AddFolderId.System, AddFolderId.Devices, AddFolderId.QuickSettings];
        Assert.Equal([AddFolderId.Start, .. subjects], page.Tabs.Select(t => t.Id));
        Assert.All(page.Tabs, tab => Assert.False(string.IsNullOrWhiteSpace(tab.Title.Text(Locale.En))));
        Assert.Equal("Start", page.Tabs[0].Title.Text(Locale.En));
    }

    [Fact]
    public void S11700_a_kind_the_tree_does_not_know_still_stands_in_more_the_last_tab()
    {
        WidgetId[] unknown = [new("notBuiltA"), new("notBuiltB")];
        var page = AddPage.Of([.. Every, .. unknown], ReadyMadeGroups.Offered());

        Assert.Equal(AddFolderId.More, page.Tabs[^1].Id);
        var more = Assert.Single(page.Sections(AddFolderId.More));
        Assert.Equal(unknown, more.Tiles.Cast<AddWidgetTile>().Select(t => t.Kind));
        Assert.Equal([AddFolderId.More], page.Homes.Single(h => h.Kind == unknown[0]).Path.Select(s => s.Id));
    }

    [Fact]
    public void S11701_no_section_is_empty_and_every_section_key_is_its_own()
    {
        var page = Page;
        var sections = page.Tabs.SelectMany(t => page.Sections(t.Id)).ToList();

        Assert.All(sections, s => Assert.NotEmpty(s.Tiles));
        Assert.All(sections, s => Assert.False(string.IsNullOrWhiteSpace(s.Heading.Text(Locale.En))));
        Assert.Equal(sections.Count, sections.Select(s => s.Key).Distinct().Count());
    }

    [Fact]
    public void S11702_a_tiles_key_names_what_it_places()
    {
        Assert.Equal("kind:clock", new AddWidgetTile(new WidgetId("clock")).Key);
        Assert.Equal("group:myDay", new AddGroupTile(ReadyMadeGroups.MyDay).Key);
        Assert.Equal("item:a.txt", new AddItemTile(new DesktopItem("a.txt", "a", DesktopItemKind.File)).Key);
    }

    [Fact]
    public void S11703_start_is_the_starter_groups_in_their_order_then_popular()
    {
        var start = Page.Sections(AddFolderId.Start);

        Assert.Equal([AddSectionKind.ReadyMade, AddSectionKind.Popular], start.Select(s => s.Kind));
        Assert.Equal(["myDay", "thisPc", "weatherStation", "nowPlaying", "myFiles", "screenTime", "inboxAndTasks"],
            start[0].Tiles.Cast<AddGroupTile>().Select(t => t.Group.Id));
        Assert.Equal(AddTree.Popular.Rows.OfType<AddLeaf>().Select(l => l.Kind), start[1].Tiles.Cast<AddWidgetTile>().Select(t => t.Kind));
    }

    [Fact]
    public void S11703_the_starters_are_data_each_its_own_place()
    {
        var starters = ReadyMadeGroups.All.Where(g => g.Starter is not null).Select(g => g.Starter!.Value).ToList();
        Assert.Equal(Enumerable.Range(1, starters.Count), starters.Order());
    }

    [Fact]
    public void S11704_a_subject_is_its_ready_made_groups_its_own_widgets_then_each_family_under_its_name()
    {
        var system = Page.Sections(AddFolderId.System);

        Assert.Equal(AddSectionKind.ReadyMade, system[0].Kind);
        Assert.Equal(AddSectionKind.Widgets, system[1].Kind);
        Assert.All(system.Skip(2), s => Assert.Equal(AddSectionKind.Family, s.Kind));

        // A family's groups are its subject's: CPU (Processor) and This PC (System) both stand in System.
        var groups = system[0].Tiles.Cast<AddGroupTile>().Select(t => t.Group.Id).ToList();
        Assert.Contains("thisPc", groups);
        Assert.Contains("cpu", groups);

        // The processor family under its reading's name, its meter first.
        var processor = system.Single(s => s.Key == $"{AddFolderId.System}/family:{AddFolderId.Processor}");
        Assert.Equal(new WidgetId("cpu"), Assert.IsType<AddWidgetTile>(processor.Tiles[0]).Kind);
        Assert.Equal(ModuleCatalog.Of(new WidgetId("cpu")).Label.Text(Locale.En), processor.Heading.Text(Locale.En));
    }

    [Fact]
    public void S11704_a_family_left_with_one_widget_is_that_widget_among_its_subjects_own()
    {
        WidgetId[] gone = [new("cpuHistory"), new("cpuCores"), new("cpuFacts"), new("topProcesses")];
        var page = AddPage.Of([.. Every.Except(gone)], ReadyMadeGroups.Offered());

        var system = page.Sections(AddFolderId.System);
        Assert.DoesNotContain(system, s => s.Key.EndsWith($"family:{AddFolderId.Processor}", StringComparison.Ordinal));
        Assert.Contains(new AddWidgetTile(new WidgetId("cpu")), system.Single(s => s.Kind == AddSectionKind.Widgets).Tiles);
        Assert.Equal([AddFolderId.System], page.Homes.Single(h => h.Kind == new WidgetId("cpu")).Path.Select(s => s.Id));
    }

    [Fact]
    public void S11705_every_kind_and_every_ready_made_group_has_exactly_one_home()
    {
        var page = Page;
        var tiles = AllTiles(page, withStart: false).ToList();

        var kinds = tiles.OfType<AddWidgetTile>().Select(t => t.Kind).ToList();
        Assert.Equal(Every.Order(), kinds.Order());
        Assert.Equal(Every.Order(), page.Homes.Select(h => h.Kind).Order());

        var groups = tiles.OfType<AddGroupTile>().Select(t => t.Group.Id).ToList();
        Assert.Equal(ReadyMadeGroups.Offered().Select(g => g.Id).Order(), groups.Order());

        // Each group stands in the tab of the subject it is about.
        foreach (var tab in page.Tabs.Where(t => t.Id != AddFolderId.Start))
        {
            foreach (var group in page.Sections(tab.Id).SelectMany(s => s.Tiles).OfType<AddGroupTile>())
            {
                Assert.Equal(tab.Id, AddTree.SubjectOf(group.Group.Home));
            }
        }
    }

    [Fact]
    public void S11705_a_family_is_in_its_subject_and_a_subject_is_its_own()
    {
        Assert.Equal(AddFolderId.System, AddTree.SubjectOf(AddFolderId.Processor));
        Assert.Equal(AddFolderId.Devices, AddTree.SubjectOf(AddFolderId.PowerButtons));
        Assert.Equal(AddFolderId.Weather, AddTree.SubjectOf(AddFolderId.Weather));
        Assert.Null(AddTree.SubjectOf(AddFolderId.Popular));
        Assert.Null(AddTree.SubjectOf(AddFolderId.Start));
    }

    [Fact]
    public void S11706_every_kind_and_every_group_is_found_by_its_own_name()
    {
        var page = Page;
        foreach (var home in page.Homes)
        {
            var name = ModuleCatalog.Of(home.Kind).Label.Text(Locale.En);
            var found = page.Find(name).SelectMany(s => s.Tiles).ToList();
            Assert.True(found.Contains(new AddWidgetTile(home.Kind)), $"searching \"{name}\" does not find {home.Kind}");
        }

        foreach (var group in page.Groups)
        {
            var name = group.Name.Local().Text(Locale.En);
            Assert.True(page.Find(name).SelectMany(s => s.Tiles).Contains(new AddGroupTile(group)), $"searching \"{name}\" does not find the group {group.Id}");
        }
    }

    [Fact]
    public void S11706_a_search_lists_groups_then_widgets_under_a_heading_each_from_every_subject()
    {
        var found = Page.Find("cpu");

        Assert.Equal([AddSectionKind.ReadyMade, AddSectionKind.Widgets], found.Select(s => s.Kind));
        Assert.All(found[0].Tiles, t => Assert.IsType<AddGroupTile>(t));
        Assert.All(found[1].Tiles, t => Assert.IsType<AddWidgetTile>(t));
        Assert.Equal(new WidgetId("cpu"), Assert.IsType<AddWidgetTile>(found[1].Tiles[0]).Kind);

        // A subject's name finds its widgets ([S-3513]): "system" the meters.
        Assert.Contains(new AddWidgetTile(new WidgetId("memory")), Page.Find("system").SelectMany(s => s.Tiles));
    }

    [Fact]
    public void S11706_a_groups_subject_finds_it_as_a_widgets_does()
    {
        var found = Page.Find("music").SelectMany(s => s.Tiles).ToList();
        Assert.Contains(new AddGroupTile(ReadyMadeGroups.NowPlaying), found);
    }

    [Fact]
    public void S11706_spaces_search_nothing_and_no_match_is_no_section()
    {
        Assert.False(AddPage.Searching("   "));
        Assert.Empty(Page.Find("   "));
        Assert.Empty(Page.Find("zzqxv"));
    }

    [Fact]
    public void S11707_tiles_of_at_least_200_fill_the_width_8_apart()
    {
        Assert.Equal(1, AddPageLayout.Columns(150));
        Assert.Equal(1, AddPageLayout.Columns(407));
        Assert.Equal(2, AddPageLayout.Columns(408));
        Assert.Equal(5, AddPageLayout.Columns(1048));

        var columns = AddPageLayout.Columns(1048);
        var tile = AddPageLayout.TileWidth(1048, columns);
        Assert.True(tile >= 200);
        Assert.True((columns * tile) + ((columns - 1) * 8) <= 1048);
        Assert.True((columns * tile) + ((columns - 1) * 8) > 1048 - columns);
    }

    [Fact]
    public void S11707_a_picture_is_scaled_down_to_its_box_and_never_up()
    {
        Assert.Equal(1, AddPageLayout.Fit(100, 50, 184, 104));
        Assert.Equal(0.5, AddPageLayout.Fit(368, 100, 184, 104));
        Assert.Equal(104.0 / 400, AddPageLayout.Fit(300, 400, 184, 104), 6);
    }

    [Fact]
    public void S11707_the_page_hangs_flush_under_the_bar_as_wide_with_no_beak_and_at_most_70_percent_tall()
    {
        var bar = new PixelRect(400, 8, 1100, 48);
        var area = new PixelRect(0, 0, 1920, 1032);

        var low = FlyoutPlacement.Under(bar, area, 300);
        Assert.Equal((400, 68, 1100, 300), (low.X, low.Y, low.Width, low.Height));
        Assert.True(double.IsNaN(low.Tip));

        var tall = FlyoutPlacement.Under(bar, area, 5000);
        Assert.Equal(Math.Floor(1032 * 0.7), tall.Height);
    }

    [Fact]
    public void S11708_down_from_the_box_goes_to_the_first_tile_and_up_from_the_first_row_back()
    {
        int[] counts = [3, 5];
        Assert.Equal(new AddKeyStep(new AddCursor(0, 0), true), AddPageKeys.Move(counts, 4, null, WidgetKey.Down));
        Assert.False(AddPageKeys.Move(counts, 4, null, WidgetKey.Left).Handled);
        Assert.Equal(new AddKeyStep(null, true), AddPageKeys.Move(counts, 4, new AddCursor(0, 2), WidgetKey.Up));
    }

    [Fact]
    public void S11708_the_arrows_walk_the_grid_and_cross_sections()
    {
        int[] counts = [3, 6];
        const int across = 4;

        // Right at a section's end goes on to the next; Left back.
        Assert.Equal(new AddCursor(1, 0), AddPageKeys.Move(counts, across, new AddCursor(0, 2), WidgetKey.Right).Cursor);
        Assert.Equal(new AddCursor(0, 2), AddPageKeys.Move(counts, across, new AddCursor(1, 0), WidgetKey.Left).Cursor);

        // Down from a section's last row: the same column of the next's first row, held to its tiles.
        Assert.Equal(new AddCursor(1, 2), AddPageKeys.Move(counts, across, new AddCursor(0, 2), WidgetKey.Down).Cursor);
        Assert.Equal(new AddCursor(1, 5), AddPageKeys.Move(counts, across, new AddCursor(1, 1), WidgetKey.Down).Cursor);

        // A row with nothing under that column: Down goes to the last tile.
        Assert.Equal(new AddCursor(1, 5), AddPageKeys.Move(counts, across, new AddCursor(1, 3), WidgetKey.Down).Cursor);

        // Up from a section's first row: the same column of the last row above.
        Assert.Equal(new AddCursor(0, 1), AddPageKeys.Move(counts, across, new AddCursor(1, 1), WidgetKey.Up).Cursor);
        Assert.Equal(new AddCursor(0, 2), AddPageKeys.Move(counts, across, new AddCursor(1, 3), WidgetKey.Up).Cursor);

        Assert.Equal(new AddCursor(0, 0), AddPageKeys.Move(counts, across, new AddCursor(1, 4), WidgetKey.Home).Cursor);
        Assert.Equal(new AddCursor(1, 5), AddPageKeys.Move(counts, across, new AddCursor(0, 0), WidgetKey.End).Cursor);

        // The last tile stays put going on.
        Assert.Equal(new AddCursor(1, 5), AddPageKeys.Move(counts, across, new AddCursor(1, 5), WidgetKey.Right).Cursor);
    }

    [Fact]
    public void S11708_ctrl_tab_steps_the_tabs_round()
    {
        Assert.Equal(1, AddPageKeys.Tab(11, 0, back: false));
        Assert.Equal(0, AddPageKeys.Tab(11, 10, back: false));
        Assert.Equal(10, AddPageKeys.Tab(11, 0, back: true));
    }

    [Fact]
    public void S11709_the_item_kind_is_never_a_tile_and_its_subject_lists_the_unplaced_items()
    {
        var itemKind = new WidgetId("desktopItem");
        DesktopItem[] unplaced = [new("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin", DesktopItemKind.ShellItem), new("a.txt", "a", DesktopItemKind.File)];

        var page = AddPage.Of(Every, ReadyMadeGroups.Offered(), itemKind, unplaced);
        Assert.DoesNotContain(new AddWidgetTile(itemKind), AllTiles(page, withStart: true));
        Assert.DoesNotContain(page.Homes, h => h.Kind == itemKind);

        var files = page.Sections(AddFolderId.Files);
        var desktop = files.Single(s => s.Kind == AddSectionKind.Desktop);
        Assert.Equal(unplaced, desktop.Tiles.Cast<AddItemTile>().Select(t => t.Item));

        // Nothing unplaced: no section.
        Assert.DoesNotContain(AddPage.Of(Every, ReadyMadeGroups.Offered(), itemKind, []).Sections(AddFolderId.Files), s => s.Kind == AddSectionKind.Desktop);
    }
}
