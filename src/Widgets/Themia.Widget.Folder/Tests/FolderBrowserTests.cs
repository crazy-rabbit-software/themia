using System.Buffers.Binary;
using System.Text;
using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>[S-1320] [S-1323] [S-1326] [S-1328] [S-4121] [S-6411] The folder widget's config, ladder, history and press-and-drag rules. Moved with the widget from Core's FolderBrowserTests, unchanged.</summary>
public sealed class FolderBrowserTests
{
    // ---- config ---------------------------------------------------------------------------------

    [Fact]
    public void S1320_defaults_match_the_original()
    {
        var config = new FolderConfig();

        Assert.Equal(FolderStyle.List, config.Style);
        Assert.Equal(FolderItemSize.Medium, config.ItemSize);
        Assert.Equal(FolderNavigation.Explorer, config.Navigation);
        Assert.Equal(ItemClickMode.Double, config.ClickMode);
        Assert.False(config.ShowExtensions);
        Assert.Equal([FolderColumn.Size, FolderColumn.Modified, FolderColumn.Type], config.Columns);
        Assert.Equal(FolderTileSize.Medium, config.TileSize);
    }

    [Fact]
    public void S1320_repair_drops_unknown_and_repeated_columns_and_says_so()
    {
        var config = new FolderConfig { Style = FolderStyle.Details, Columns = [FolderColumn.Git, (FolderColumn)42, FolderColumn.Git, FolderColumn.Size] };

        var notes = config.Repair();

        Assert.Equal([FolderColumn.Git, FolderColumn.Size], config.Columns);
        Assert.Single(notes);
    }

    [Fact]
    public void S1320_clone_does_not_share_the_column_list()
    {
        var config = new FolderConfig { Style = FolderStyle.Details };
        var copy = (FolderConfig)config.Clone();

        copy.Columns.Add(FolderColumn.Git);

        Assert.DoesNotContain(FolderColumn.Git, config.Columns);
    }

    // ---- selection ------------------------------------------------------------------------------

    private static FolderSelection Selection(int count)
    {
        var selection = new FolderSelection();
        selection.SetOrder([.. Enumerable.Range(0, count).Select(i => $@"C:\Root\{i}")]);
        return selection;
    }

    [Fact]
    public void S1328_an_additive_band_keeps_what_was_selected()
    {
        var s = Selection(5);
        s.Click(0, false, false);

        s.Band([2, 3], before: [@"C:\Root\0"], BandMode.Add);
        Assert.Equal([@"C:\Root\0", @"C:\Root\2", @"C:\Root\3"], s.Selected);

        s.Band([3], before: null, BandMode.Replace);
        Assert.Equal([@"C:\Root\3"], s.Selected);
    }

    // ---- gesture (gesture.test.ts, #58) ---------------------------------------------------------

    [Fact]
    public void S1328_movement_inside_the_dead_zone_stays_a_click()
    {
        var g = new FolderGesture();
        g.BeginPress(10, 10, FolderPress.Item, [@"C:\a"]);

        Assert.Equal(GestureStep.Idle, g.Move(13, 13));
        Assert.Equal((false, false), g.Release());
    }

    [Fact]
    public void S1328_a_press_on_an_item_becomes_a_drag_once_and_suppresses_the_click()
    {
        var g = new FolderGesture();
        g.BeginPress(10, 10, FolderPress.Item, [@"C:\a"]);

        Assert.Equal(GestureStep.StartDrag, g.Move(20, 10));
        Assert.Equal([@"C:\a"], g.DragPaths);
        Assert.Equal(GestureStep.Idle, g.Move(40, 10));
        Assert.Equal((true, false), g.Release());
    }

    [Fact]
    public void S1328_a_band_once_started_stays_a_band()
    {
        var g = new FolderGesture();
        g.BeginPress(0, 0, FolderPress.Background);

        Assert.Equal(GestureStep.Band, g.Move(10, 10));
        Assert.Equal(GestureStep.Band, g.Move(1, 1));
        Assert.Equal(new BandRect(0, 0, 1, 1), g.BandRect);
        Assert.Equal((false, true), g.Release());
    }

    [Fact]
    public void S1328_a_stale_drag_origin_never_leaks_into_the_next_press()
    {
        // #58: an aborted gesture left its origin; the next press must measure from its own.
        var g = new FolderGesture();
        g.BeginPress(0, 0, FolderPress.Item, [@"C:\a"]);
        var stale = g.Generation;

        g.BeginPress(500, 500, FolderPress.Item, [@"C:\b"]);

        Assert.NotEqual(stale, g.Generation);
        Assert.Equal(GestureStep.Idle, g.Move(502, 502));
        Assert.False(g.DragWasActive);
    }

    [Fact]
    public void S1328_release_resets_and_moves_after_it_do_nothing()
    {
        var g = new FolderGesture();
        g.BeginPress(0, 0, FolderPress.Background);
        var generation = g.Generation;

        g.Release();

        Assert.False(g.Pressed);
        Assert.NotEqual(generation, g.Generation);
        Assert.Equal(GestureStep.Idle, g.Move(100, 100));
    }

    [Fact]
    public void S1328_band_hits_overlapping_boxes_only()
    {
        var band = new BandRect(50, 50, 10, 10);

        Assert.True(band.Hits(0, 0, 20, 20));
        Assert.True(band.Hits(45, 45, 100, 100));
        Assert.False(band.Hits(60, 0, 20, 20));
    }

    // ---- history --------------------------------------------------------------------------------

    [Fact]
    public void S1326_back_and_forward_restore_the_scroll_and_a_new_place_clears_forward()
    {
        var h = new FolderHistory(@"C:\Home");

        Assert.True(h.Go(@"C:\Home\A", scroll: 120));
        Assert.True(h.Go(@"C:\Home\A\B", scroll: 40));

        Assert.Equal(new FolderPlace(@"C:\Home\A", 40), h.Back(scroll: 7));
        Assert.True(h.CanGoForward);
        Assert.Equal(new FolderPlace(@"C:\Home\A\B", 7), h.Forward(scroll: 40));

        h.Back(0);
        h.Go(@"C:\Elsewhere", 0);
        Assert.False(h.CanGoForward);
        Assert.False(h.Go(@"c:\elsewhere", 0));
    }

    [Theory]
    [InlineData(@"C:\Users\nate", @"C:\Users")]
    [InlineData(@"C:\Users", @"C:\")]
    [InlineData(@"C:\", null)]
    [InlineData(@"\\server\share", null)]
    [InlineData(@"\\server\share\dir", @"\\server\share")]
    public void S1326_up_goes_to_the_parent_and_stops_at_a_root(string path, string? parent)
        => Assert.Equal(parent, FolderHistory.Parent(path));

    [Fact]
    public void S1323_the_breadcrumb_runs_from_the_root()
    {
        var crumbs = FolderHistory.Crumbs(@"C:\Users\nate");

        Assert.Equal([(@"C:", @"C:\"), ("Users", @"C:\Users"), ("nate", @"C:\Users\nate")], crumbs);
    }

    [Fact]
    public void S1326_history_is_capped()
    {
        var h = new FolderHistory(@"C:\0");

        for (var i = 1; i <= FolderHistory.Depth + 10; i++)
        {
            h.Go($@"C:\{i}", 0);
        }

        var steps = 0;

        while (h.Back(0) is not null)
        {
            steps++;
        }

        Assert.Equal(FolderHistory.Depth, steps);
    }

    // ---- the ladder -----------------------------------------------------------------------------

    [Fact]
    public void S1323_tiles_stretch_to_fill_the_row()
    {
        var plan = FolderLayout.Grid(width: 330, count: 7, FolderTileSize.Small, labelled: true);

        Assert.Equal(4, plan.Columns);
        Assert.Equal(82.5, plan.CellWidth);
        Assert.Equal(2, plan.Rows);
        Assert.Equal(90, plan.CellHeight);
    }

    [Fact]
    public void S4121_tiles_keep_their_preset_height_there_is_no_stretch()
    {
        // A grid that would fit twice over still draws its preset rows and leaves the rest empty.
        var plan = FolderLayout.Grid(320, 8, FolderTileSize.Small, true);
        Assert.Equal(90, plan.CellHeight);
        Assert.Equal(2, plan.Rows);
    }

    [Fact]
    public void S1323_icons_view_packs_tiles_to_the_icon()
    {
        var plan = FolderLayout.Grid(560, 20, FolderTileSize.Small, labelled: false);

        Assert.Equal(56, plan.CellHeight);
        Assert.Equal(10, plan.Columns);
    }

    [Fact]
    public void S1323_a_narrow_box_still_gets_one_column()
        => Assert.Equal(1, FolderLayout.Grid(30, 3, FolderTileSize.Large, true).Columns);

    [Fact]
    public void S1323_list_columns_grow_with_the_width()
    {
        Assert.Empty(FolderLayout.ListColumns(new WidgetArea(150, 100), 100, anyFile: true));
        Assert.Equal([FolderColumn.Modified], FolderLayout.ListColumns(new WidgetArea(300, 200), 250, anyFile: true));
        Assert.Equal([FolderColumn.Size, FolderColumn.Modified], FolderLayout.ListColumns(new WidgetArea(480, 116), 424, anyFile: true));
        Assert.Equal([FolderColumn.Type, FolderColumn.Size, FolderColumn.Modified], FolderLayout.ListColumns(new WidgetArea(800, 500), 744, anyFile: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S6411_the_list_s_rightmost_column_is_one_every_row_fills_at_every_width(bool anyFile)
    {
        // The bug: from 400 DIPs the list put Size last, blank for every folder, so the dates ended
        // a hidden column's width short of the right edge.
        for (var width = 120; width <= 1200; width += 10)
        {
            var area = new WidgetArea(width, 300);
            var columns = FolderLayout.ListColumns(area, width - 56, anyFile);

            if (columns.Count > 0)
            {
                Assert.Equal(FolderColumn.Modified, columns[^1]);
            }

            if (!anyFile)
            {
                Assert.DoesNotContain(FolderColumn.Size, columns);
            }
        }
    }

    [Fact]
    public void S6411_the_last_details_column_ends_at_the_right_edge_whatever_it_is()
    {
        // Type, Git and Cloud are drawn from the left of their slot; as the last column that left
        // the slot's slack as an empty strip at the row's end.
        foreach (var last in FolderColumns.All)
        {
            List<FolderColumn> chosen = [.. FolderColumns.All.Where(c => c != last), last];

            for (var width = 200; width <= 1400; width += 20)
            {
                var shown = FolderColumns.Fit(chosen, width);

                for (var i = 0; i < shown.Count; i++)
                {
                    var end = FolderColumns.AlignsRight(shown[i], last: i == shown.Count - 1);
                    Assert.True(i < shown.Count - 1 || end, $"{shown[i]} last at {width}");
                }
            }
        }

        Assert.False(FolderColumns.AlignsRight(FolderColumn.Type, last: false));
        Assert.True(FolderColumns.AlignsRight(FolderColumn.Size, last: false));
    }

    [Fact]
    public void S6411_a_listing_of_folders_only_reserves_no_size_column()
    {
        var plan = FolderLayout.Plan(new WidgetArea(670, 300), new FolderConfig(), 4, anyFile: false, searchOpen: false, navigate: false, hasGit: false);

        Assert.Equal([FolderColumn.Modified], plan.Columns);
    }

    [Fact]
    public void S1323_chrome_appears_on_big_tiles_only()
    {
        var config = new FolderConfig { Style = FolderStyle.Details };

        var small = FolderLayout.Plan(new WidgetArea(200, 106), config, 20, true, searchOpen: false, navigate: true, hasGit: false);
        Assert.False(small.Breadcrumb);

        var big = FolderLayout.Plan(new WidgetArea(800, 516), config, 20, true, false, true, false);
        Assert.True(big.Breadcrumb);
        Assert.True(big.ColumnHeader);
        Assert.Equal(config.Columns, big.Columns);
    }

    [Fact]
    public void S1323_a_big_tile_has_a_breadcrumb_and_no_column_header_outside_details()
    {
        var plan = FolderLayout.Plan(new WidgetArea(800, 516), new FolderConfig { Style = FolderStyle.Grid }, 6, true, false, true, false);

        Assert.True(plan.Breadcrumb);
        Assert.False(plan.ColumnHeader);
        Assert.Empty(plan.Columns);
        Assert.NotNull(plan.Grid);
    }

    [Fact]
    public void S1323_an_open_search_box_is_shown_before_the_column_header()
    {
        var config = new FolderConfig { Style = FolderStyle.Details };

        var plan = FolderLayout.Plan(new WidgetArea(300, 80), config, 20, true, searchOpen: true, navigate: false, hasGit: false);

        Assert.True(plan.SearchBox);
        Assert.False(plan.ColumnHeader);
    }

    [Fact]
    public void S1323_xl_grid_tiles_are_one_size_larger()
    {
        var config = new FolderConfig { Style = FolderStyle.Grid, TileSize = FolderTileSize.Small };

        var plan = FolderLayout.Plan(new WidgetArea(800, 516), config, 20, true, false, false, false);

        Assert.Equal(48, plan.Grid?.Icon);
    }

    [Fact]
    public void S1326_header_buttons_shed_in_priority_order()
    {
        Assert.Equal(FolderHeaderPlan.None, FolderLayout.Header(150, FolderStyle.List, navigate: true, hasGit: true));

        var narrow = FolderLayout.Header(200, FolderStyle.List, true, true);
        Assert.True(narrow.Up);
        Assert.False(narrow.Back);

        var wide = FolderLayout.Header(800, FolderStyle.List, true, true);
        Assert.Equal(new FolderHeaderPlan(true, true, true, true, true, true, true), wide);

        var details = FolderLayout.Header(800, FolderStyle.Details, navigate: false, hasGit: false);
        Assert.False(details.Sort);
        Assert.False(details.Back);
        Assert.True(details.Search);

        Assert.False(FolderLayout.Header(800, FolderStyle.Grid, false, hasGit: true).Git);
    }
}
