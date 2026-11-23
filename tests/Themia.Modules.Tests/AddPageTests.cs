using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-13610]..[S-13617] [S-11701]..[S-11709] The add page: its rail, its sections, family stacks and tiles, the
/// search, the layout and the keys. Here, not in Core's tests, because the page is built from every widget of
/// the build and every ready-made group, and only this host has them all.
/// </summary>
public sealed class AddPageTests
{
    private static readonly WidgetId[] Every = [.. ModuleCatalog.Widgets.Select(w => w.Id)];

    private static AddPage Page => AddPage.Of(Every, ReadyMadeGroups.Offered());

    private static IEnumerable<AddTile> AllTiles(AddPage page, bool withStart)
        => page.Rail.Where(t => withStart || t.Id != AddFolderId.Start).SelectMany(t => page.Sections(t.Id)).SelectMany(s => s.Tiles);

    private static AddFamily Processor(AddPage page)
        => page.Sections(AddFolderId.System).Single(s => s.Family?.Id == AddFolderId.Processor).Family!;

    // ---- [S-13610] the rail ---------------------------------------------------------------------

    [Fact]
    public void S13610_the_rail_is_start_then_every_subject_in_browses_order_and_no_more_when_every_kind_has_a_home()
    {
        var page = Page;

        AddFolderId[] subjects = [AddFolderId.Time, AddFolderId.CalendarMail, AddFolderId.Weather, AddFolderId.Markets, AddFolderId.Media,
            AddFolderId.Notes, AddFolderId.Files, AddFolderId.System, AddFolderId.Devices, AddFolderId.QuickSettings];
        Assert.Equal([AddFolderId.Start, .. subjects], page.Rail.Select(t => t.Id));
        Assert.All(page.Rail, item => Assert.False(string.IsNullOrWhiteSpace(item.Title.Text(Locale.En))));
        Assert.All(page.Rail, item => Assert.False(string.IsNullOrEmpty(item.Glyph)));
        Assert.Equal("Start", page.Rail[0].Title.Text(Locale.En));
    }

    [Fact]
    public void S13610_each_subjects_count_is_the_widgets_at_home_in_it_and_start_counts_none()
    {
        var page = Page;

        Assert.Equal(0, page.Rail[0].Count);
        foreach (var item in page.Rail.Skip(1))
        {
            Assert.Equal(page.Homes.Count(h => h.Path[0].Id == item.Id), item.Count);
            Assert.Equal(page.Sections(item.Id).SelectMany(s => s.Tiles).OfType<AddWidgetTile>().Count(), item.Count);
        }

        // Every kind counted once ([S-16681]: a kind with presets once per preset): the counts add up to the catalogue.
        Assert.Equal(ModuleCatalog.Picks.Count(), page.Rail.Sum(i => i.Count));
    }

    [Fact]
    public void S13610_a_subjects_glyph_is_its_first_widgets()
    {
        var system = Page.Rail.Single(i => i.Id == AddFolderId.System);
        var shelf = AddTree.Browse.Rows.OfType<AddShelf>().Single(s => s.Id == AddFolderId.System);
        Assert.Equal(shelf.Icon.Glyph, system.Glyph);
    }

    [Fact]
    public void S13610_a_kind_the_tree_does_not_know_still_stands_in_more_the_last_item()
    {
        WidgetId[] unknown = [new("notBuiltA"), new("notBuiltB")];
        var page = AddPage.Of([.. Every, .. unknown], ReadyMadeGroups.Offered());

        Assert.Equal(AddFolderId.More, page.Rail[^1].Id);
        Assert.Equal(2, page.Rail[^1].Count);
        var more = Assert.Single(page.Sections(AddFolderId.More));
        Assert.Equal(unknown, more.Tiles.Cast<AddWidgetTile>().Select(t => t.Kind));
        Assert.Equal([AddFolderId.More], page.Homes.Single(h => h.Kind == unknown[0]).Path.Select(s => s.Id));
    }

    // ---- [S-11701] [S-13611] sections -----------------------------------------------------------

    [Fact]
    public void S11701_no_section_is_empty_and_every_section_key_is_its_own()
    {
        var page = Page;
        var sections = page.Rail.SelectMany(t => page.Sections(t.Id)).ToList();

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
    public void S13611_a_subject_is_its_ready_made_groups_its_own_widgets_then_each_family_as_a_stack()
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

        // [S-13612] Each family section carries its family, its tiles and name the section's; no other section has one.
        Assert.All(system.Where(s => s.Kind == AddSectionKind.Family), s =>
        {
            var family = Assert.IsType<AddFamily>(s.Family);
            Assert.Equal(s.Tiles, family.Tiles);
            Assert.True(family.Tiles.Count >= 2);
            Assert.Equal(s.Heading.Text(Locale.En), family.Title.Text(Locale.En));
        });
        var page = Page;
        Assert.All(page.Rail.SelectMany(i => page.Sections(i.Id)).Where(s => s.Kind != AddSectionKind.Family), s => Assert.Null(s.Family));
    }

    [Fact]
    public void S13611_a_family_left_with_one_widget_is_that_widget_among_its_subjects_own()
    {
        WidgetId[] gone = [new("cpuHistory"), new("cpuCores"), new("topProcesses")];
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

        // Each group stands in the subject it is about.
        foreach (var item in page.Rail.Where(t => t.Id != AddFolderId.Start))
        {
            foreach (var group in page.Sections(item.Id).SelectMany(s => s.Tiles).OfType<AddGroupTile>())
            {
                Assert.Equal(item.Id, AddTree.SubjectOf(group.Group.Home));
            }
        }

        // [S-13612] A kind in a family stands in its stack alone, never among its subject's own widgets too.
        var stacked = page.Rail.SelectMany(i => page.Sections(i.Id)).Where(s => s.Family is not null).SelectMany(s => s.Tiles).ToHashSet();
        Assert.DoesNotContain(page.Rail.SelectMany(i => page.Sections(i.Id)).Where(s => s.Kind == AddSectionKind.Widgets).SelectMany(s => s.Tiles), stacked.Contains);
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
    public void S13613_a_subjects_sections_are_made_once_when_first_asked_for()
    {
        var page = Page;
        var first = page.Sections(AddFolderId.Weather);
        Assert.Same(first, page.Sections(AddFolderId.Weather));
        Assert.Empty(page.Sections(AddFolderId.Popular));
    }

    [Fact]
    public void S13613_hundreds_of_kinds_split_into_families_stand_as_a_stack_each_every_one_counted()
    {
        // A subject of 20 own widgets and 30 families of 12 one-row parts: 380 kinds.
        WidgetId Kind(string name) => new(name);
        var own = Enumerable.Range(0, 20).Select(i => (AddNode)new AddLeaf(Kind($"own{i}"))).ToList();
        AddFolderId[] ids = [AddFolderId.Processor, AddFolderId.Graphics, AddFolderId.NetworkReading];
        var families = Enumerable.Range(0, 30).Select(f => (AddNode)new AddShelf(ids[f % ids.Length], LocalText.Fixed($"Family {f}"), Kind($"f{f}p0"),
            [.. Enumerable.Range(0, 12).Select(p => (AddNode)new AddLeaf(Kind($"f{f}p{p}")))])).ToList();
        var subject = new AddShelf(AddFolderId.Weather, LocalText.Fixed("Weather"), Kind("own0"), [.. own, .. families]);
        var browse = new AddShelf(AddFolderId.Browse, LocalText.Fixed("Browse"), Kind("own0"), [subject]);
        var popular = new AddShelf(AddFolderId.Popular, LocalText.Fixed("Popular"), Kind("own0"), [new AddLeaf(Kind("own0"), Again: true)]);
        WidgetId[] offered = [.. own.Cast<AddLeaf>().Select(l => l.Kind), .. families.Cast<AddShelf>().SelectMany(f => f.Rows.Cast<AddLeaf>().Select(l => l.Kind))];

        var page = AddPage.Of(popular, browse, offered, [], null, null);

        Assert.Equal(380, page.Rail.Single(i => i.Id == AddFolderId.Weather).Count);
        Assert.Equal(380, page.Homes.Count);
        var sections = page.Sections(AddFolderId.Weather);
        Assert.Equal(1 + 30, sections.Count);
        Assert.Equal(20, sections[0].Tiles.Count);
        Assert.All(sections.Skip(1), s => Assert.Equal(12, s.Family!.Tiles.Count));

        // Closed, the whole subject draws its own widgets and one row per family: 20 + 30 × 3 at three across.
        var drawn = sections[0].Tiles.Count + sections.Skip(1).Sum(s => AddPageLayout.Stack(s.Tiles.Count, 3, open: false).Stops);
        Assert.Equal(20 + (30 * 3), drawn);
    }

    // ---- [S-13612] [S-13614] family stacks --------------------------------------------------------

    [Fact]
    public void S13612_a_collapsed_stack_shows_its_first_row_ending_in_plus_n()
    {
        // Five widgets, three across: two tiles and "+3".
        Assert.Equal(new AddStack(2, 3), AddPageLayout.Stack(5, 3, open: false));
        Assert.Equal(3, AddPageLayout.Stack(5, 3, open: false).Stops);

        // Eleven one-row facts, four across: three and "+8"; opened, all eleven and no "+N".
        Assert.Equal(new AddStack(3, 8), AddPageLayout.Stack(11, 4, open: false));
        Assert.Equal(new AddStack(11, 0), AddPageLayout.Stack(11, 4, open: true));
        Assert.Equal(11, AddPageLayout.Stack(11, 4, open: true).Stops);

        // A family that fits one row shows whole, open or not.
        Assert.Equal(new AddStack(3, 0), AddPageLayout.Stack(3, 3, open: false));
        Assert.Equal(new AddStack(2, 0), AddPageLayout.Stack(2, 5, open: false));

        // One across: its first tile and "+N" under it.
        Assert.Equal(new AddStack(1, 4), AddPageLayout.Stack(5, 1, open: false));
        Assert.Equal(new AddStack(1, 1), AddPageLayout.Stack(2, 1, open: false));
    }

    [Fact]
    public void S13612_a_stack_shows_and_hides_every_tile_of_its_family_once()
    {
        foreach (var columns in new[] { 1, 2, 3, 5 })
        {
            foreach (var count in new[] { 2, 3, 5, 11, 40 })
            {
                var closed = AddPageLayout.Stack(count, columns, open: false);
                Assert.Equal(count, closed.Shown + closed.Hidden);
                Assert.True(closed.Stops <= Math.Max(columns, 2), $"{count} in {columns} across takes more than its first row");
                Assert.True(closed.Shown >= 1);
            }
        }
    }

    [Fact]
    public void S13614_a_familys_preview_is_its_first_widgets_picture_under_its_own_key()
    {
        var page = Page;
        var processor = Processor(page);

        Assert.Equal("family:Processor", processor.Key);
        Assert.Equal(new AddWidgetTile(new WidgetId("cpu")), processor.Picture);
        Assert.Equal(processor.Tiles[0], processor.Picture);

        // Its key is no tile's: the preview tells a family from its first widget.
        Assert.DoesNotContain(AllTiles(page, withStart: true), t => t.Key == processor.Key);
    }

    // ---- [S-13615] the search ---------------------------------------------------------------------

    [Fact]
    public void S13615_every_kind_and_every_group_is_found_by_its_own_name()
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
    public void S13615_a_search_lists_groups_then_widgets_under_a_heading_each_from_every_subject()
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
    public void S13615_a_search_is_flat_a_familys_widgets_each_on_their_own()
    {
        var page = Page;
        var processor = Processor(page);

        // The family's name finds every one of its widgets, none stacked, none behind a "+N".
        var found = page.Find(processor.Title.Text(Locale.En));
        Assert.All(found, s => Assert.Null(s.Family));
        Assert.DoesNotContain(found, s => s.Kind == AddSectionKind.Family);
        var widgets = found.Single(s => s.Kind == AddSectionKind.Widgets).Tiles;
        Assert.All(processor.Tiles, t => Assert.Contains(t, widgets));

        // A family's later widget by its own name, as a tile of its own.
        Assert.Contains(new AddWidgetTile(new WidgetId("topProcesses")), page.Find("top processes").SelectMany(s => s.Tiles));
    }

    [Fact]
    public void S13615_a_groups_subject_finds_it_as_a_widgets_does()
    {
        var found = Page.Find("music").SelectMany(s => s.Tiles).ToList();
        Assert.Contains(new AddGroupTile(ReadyMadeGroups.NowPlaying), found);
    }

    [Fact]
    public void S13615_spaces_search_nothing_and_no_match_is_no_section()
    {
        Assert.False(AddPage.Searching("   "));
        Assert.Empty(Page.Find("   "));
        Assert.Empty(Page.Find("zzqxv"));
    }

    // ---- [S-13601] [S-13616] the layout -----------------------------------------------------------

    [Fact]
    public void S13601_tiles_of_at_least_200_fill_the_list_4_apart()
    {
        Assert.Equal(1, AddPageLayout.Columns(150));
        Assert.Equal(1, AddPageLayout.Columns(403));
        Assert.Equal(2, AddPageLayout.Columns(404));
        Assert.Equal(5, AddPageLayout.Columns(1016));
        Assert.Equal(4, AddPageLayout.Columns(1015));

        var columns = AddPageLayout.Columns(1048);
        var tile = AddPageLayout.TileWidth(1048, columns);
        Assert.True(tile >= 200);
        Assert.True((columns * tile) + ((columns - 1) * 4) <= 1048);
        Assert.True((columns * tile) + ((columns - 1) * 4) > 1048 - columns);
    }

    [Fact]
    public void S13616_the_rail_the_list_and_the_preview_share_the_width_the_preview_dropping_first()
    {
        // A 1920 display's bar less the flyout's gutters: the rail, two tiles across, the 360 pane.
        Assert.Equal((240.0, 596.0, 360.0), AddPageLayout.Split(1228));
        Assert.Equal(2, AddPageLayout.Columns(AddPageLayout.Split(1228).List));

        // Just room for the rail, two tiles and the pane; less and the pane goes, the list taking its room.
        Assert.Equal((240.0, 404.0, 360.0), AddPageLayout.Split(240 + 16 + 404 + 16 + 360));
        Assert.Equal((240.0, 779.0, 0.0), AddPageLayout.Split(240 + 16 + 779));
        Assert.False(AddPageLayout.Compact(240 + 16 + 404));

        // Narrower still: the rail its glyphs alone, the list the rest.
        Assert.Equal((48.0, 403.0 + 192, 0.0), AddPageLayout.Split(240 + 16 + 403));
        Assert.True(AddPageLayout.Compact(240 + 16 + 403));
        Assert.Equal((48.0, 0.0, 0.0), AddPageLayout.Split(-5));
    }

    [Fact]
    public void S13602_a_picture_is_scaled_down_to_its_box_and_never_up()
    {
        Assert.Equal(1, AddPageLayout.Fit(100, 50, 184, 104));
        Assert.Equal(0.5, AddPageLayout.Fit(368, 100, 184, 104));
        Assert.Equal(104.0 / 400, AddPageLayout.Fit(300, 400, 184, 104), 6);
    }

    [Fact]
    public void S11707_the_page_hangs_flush_under_the_bar_as_wide_with_no_beak_and_at_most_70_percent_tall()
    {
        var bar = new DipRect(400, 8, 1100, 48);
        var area = new DipRect(0, 0, 1920, 1032);

        var low = FlyoutPlacement.Under(bar, area, 300);
        Assert.Equal((400, 68, 1100, 300), (low.X, low.Y, low.Width, low.Height));
        Assert.True(double.IsNaN(low.Tip));

        var tall = FlyoutPlacement.Under(bar, area, 5000);
        Assert.Equal(Math.Floor(1032 * 0.7), tall.Height);
    }

    // ---- [S-13617] the keys -----------------------------------------------------------------------

    [Fact]
    public void S13617_down_from_the_box_goes_to_the_first_stop_and_up_from_the_first_row_back()
    {
        int[] counts = [3, 5];
        Assert.Equal(new AddKeyStep(new AddCursor(0, 0), true), AddPageKeys.Move(counts, 4, null, WidgetKey.Down));
        Assert.False(AddPageKeys.Move(counts, 4, null, WidgetKey.Left).Handled);
        Assert.Equal(new AddKeyStep(null, true), AddPageKeys.Move(counts, 4, new AddCursor(0, 2), WidgetKey.Up));
    }

    [Fact]
    public void S13617_the_arrows_walk_the_grid_and_cross_sections()
    {
        int[] counts = [3, 6];
        const int across = 4;

        // Right at a section's end goes on to the next; Left inside a row back one.
        Assert.Equal(new AddCursor(1, 0), AddPageKeys.Move(counts, across, new AddCursor(0, 2), WidgetKey.Right).Cursor);
        Assert.Equal(new AddCursor(1, 1), AddPageKeys.Move(counts, across, new AddCursor(1, 2), WidgetKey.Left).Cursor);

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
    public void S13617_left_at_a_rows_start_goes_back_to_the_rail()
    {
        int[] counts = [3, 6];
        var back = AddPageKeys.Move(counts, 4, new AddCursor(1, 4), WidgetKey.Left);
        Assert.True(back.Handled);
        Assert.True(back.ToRail);
        Assert.Null(back.Cursor);
        Assert.True(AddPageKeys.Move(counts, 4, new AddCursor(1, 0), WidgetKey.Left).ToRail);
        Assert.False(AddPageKeys.Move(counts, 4, new AddCursor(1, 1), WidgetKey.Left).ToRail);
    }

    [Fact]
    public void S13617_a_familys_head_is_a_run_of_one_over_its_stack()
    {
        // Widgets (3), then a family: its head, then its stack of two tiles and "+N".
        int[] counts = [3, 1, 3];
        const int across = 3;

        Assert.Equal(new AddCursor(1, 0), AddPageKeys.Move(counts, across, new AddCursor(0, 2), WidgetKey.Down).Cursor);
        Assert.Equal(new AddCursor(2, 0), AddPageKeys.Move(counts, across, new AddCursor(1, 0), WidgetKey.Down).Cursor);
        Assert.Equal(new AddCursor(1, 0), AddPageKeys.Move(counts, across, new AddCursor(2, 2), WidgetKey.Up).Cursor);
        Assert.Equal(new AddCursor(0, 0), AddPageKeys.Move(counts, across, new AddCursor(1, 0), WidgetKey.Up).Cursor);
        Assert.Equal(new AddCursor(2, 0), AddPageKeys.Move(counts, across, new AddCursor(1, 0), WidgetKey.Right).Cursor);
        Assert.True(AddPageKeys.Move(counts, across, new AddCursor(1, 0), WidgetKey.Left).ToRail);
    }

    [Fact]
    public void S13617_the_rail_walks_up_and_down_up_from_the_first_to_the_box_and_right_into_the_content()
    {
        Assert.Equal(new AddRailStep(3, AddRailMove.Item), AddPageKeys.Rail(11, 2, WidgetKey.Down));
        Assert.Equal(new AddRailStep(1, AddRailMove.Item), AddPageKeys.Rail(11, 2, WidgetKey.Up));
        Assert.Equal(new AddRailStep(0, AddRailMove.Box), AddPageKeys.Rail(11, 0, WidgetKey.Up));
        Assert.Equal(new AddRailStep(10, AddRailMove.Item), AddPageKeys.Rail(11, 10, WidgetKey.Down));
        Assert.Equal(new AddRailStep(0, AddRailMove.Item), AddPageKeys.Rail(11, 6, WidgetKey.Home));
        Assert.Equal(new AddRailStep(10, AddRailMove.Item), AddPageKeys.Rail(11, 6, WidgetKey.End));
        Assert.Equal(new AddRailStep(6, AddRailMove.Content), AddPageKeys.Rail(11, 6, WidgetKey.Right));
        Assert.Equal(new AddRailStep(6, AddRailMove.Content), AddPageKeys.Rail(11, 6, WidgetKey.Enter));
        Assert.Equal(AddRailMove.None, AddPageKeys.Rail(11, 6, WidgetKey.Left).Move);
        Assert.Equal(AddRailMove.None, AddPageKeys.Rail(0, 0, WidgetKey.Down).Move);
    }

    [Fact]
    public void S13617_ctrl_tab_steps_the_rail_round()
    {
        Assert.Equal(1, AddPageKeys.Tab(11, 0, back: false));
        Assert.Equal(0, AddPageKeys.Tab(11, 10, back: false));
        Assert.Equal(10, AddPageKeys.Tab(11, 0, back: true));
    }

    // ---- [S-11709] the desktop icon kind ----------------------------------------------------------

    [Fact]
    public void S11709_the_desktop_icon_kind_has_its_tile_and_its_subject_lists_the_unplaced_items_too()
    {
        var itemKind = new WidgetId("desktopItem");
        DesktopItem[] unplaced = [new("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin", DesktopItemKind.ShellItem), new("a.txt", "a", DesktopItemKind.File)];

        var page = AddPage.Of(Every, ReadyMadeGroups.Offered(), itemKind, unplaced);
        var files = page.Sections(AddFolderId.Files);

        // The tile (it makes an own icon): among the subject's widgets, once, and at home there.
        var widgets = files.Single(s => s.Kind == AddSectionKind.Widgets);
        Assert.Single(widgets.Tiles, t => t.Equals(new AddWidgetTile(itemKind)));
        Assert.Single(AllTiles(page, withStart: false), t => t.Equals(new AddWidgetTile(itemKind)));
        Assert.Equal([AddFolderId.Files], page.Homes.Single(h => h.Kind == itemKind).Path.Select(s => s.Id));
        Assert.Equal("Shortcut", ModuleCatalog.Of(itemKind).AddLabel!.Value.Text(Locale.En));
        Assert.Equal("Desktop icon", ModuleCatalog.Of(itemKind).Label.Text(Locale.En));

        // And the view's unplaced desktop items, in the same subject, as given.
        var desktop = files.Single(s => s.Kind == AddSectionKind.Desktop);
        Assert.Equal(unplaced, desktop.Tiles.Cast<AddItemTile>().Select(t => t.Item));
        Assert.True(files.ToList().IndexOf(widgets) < files.ToList().IndexOf(desktop));
        Assert.DoesNotContain(page.Rail.Where(t => t.Id != AddFolderId.Files).SelectMany(t => page.Sections(t.Id)), s => s.Kind == AddSectionKind.Desktop);

        // Nothing unplaced (or none given, or no item kind): no section, the tile stays.
        foreach (var bare in new[] { AddPage.Of(Every, ReadyMadeGroups.Offered(), itemKind, []), AddPage.Of(Every, ReadyMadeGroups.Offered(), itemKind), AddPage.Of(Every, ReadyMadeGroups.Offered(), null, unplaced), Page })
        {
            Assert.DoesNotContain(bare.Sections(AddFolderId.Files), s => s.Kind == AddSectionKind.Desktop);
            Assert.Contains(new AddWidgetTile(itemKind), bare.Sections(AddFolderId.Files).Single(s => s.Kind == AddSectionKind.Widgets).Tiles);
        }
    }

    [Fact]
    public void S11709_the_desktop_icon_kind_is_among_popular()
    {
        var itemKind = new WidgetId("desktopItem");
        var popular = Page.Sections(AddFolderId.Start).Single(s => s.Kind == AddSectionKind.Popular);

        Assert.Contains(new AddWidgetTile(itemKind), popular.Tiles);
        Assert.Equal(10, ModuleCatalog.Of(itemKind).Add!.Popular);
    }
}
