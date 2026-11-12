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
        Assert.Equal(FolderNavigation.Explorer, config.Navigation);
        Assert.Equal(ItemClickMode.Double, config.ClickMode);
        Assert.False(config.ShowExtensions);
        Assert.Equal([FolderColumn.Size, FolderColumn.Modified, FolderColumn.Type], config.Columns);
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

    [Theory]
    [InlineData(FolderStyle.Grid, 330, 32, true, 4)]
    [InlineData(FolderStyle.Icons, 330, 32, false, 7)]
    [InlineData(FolderStyle.Grid, 560, 48, true, 5)]
    public void S1323_cells_are_the_family_s_32_or_48_from_an_expanded_width(FolderStyle style, double width, double icon, bool named, int columns)
    {
        var plan = FolderLayout.Plan(new WidgetArea(width, 300), new FolderConfig { Style = style }, 20, true, false, false, false);
        var grid = plan.Grid!.Value;

        Assert.Equal(icon, grid.Icon);
        Assert.Equal(named, grid.Named);
        Assert.Equal(columns, grid.Columns);
        Assert.Equal(Themia.Sdk.Kit.RowRules.CellHeight(icon, named), grid.Height);
    }

    [Theory]
    [InlineData(84, 2)]    // XS: 72 for the items, two rows
    [InlineData(204, 6)]   // M: 192, six whole rows
    [InlineData(524, 18)]  // XL: 512
    public void S1323_rows_are_the_row_block_s_one_line_of_28_and_only_whole_ones_show(double height, int rows)
    {
        var area = new WidgetArea(320, height);
        var plan = FolderLayout.Plan(area, new FolderConfig(), 40, true, false, false, false);

        Assert.Null(plan.Grid);
        Assert.Equal(28, FolderLayout.RowHeight);
        Assert.Equal(rows, FolderLayout.Rows(FolderLayout.ItemsHeight(area, plan.SearchBox, plan.ColumnHeader, plan.Breadcrumb)));
    }

    [Fact]
    public void S1323_the_list_shows_the_date_modified_from_a_width_of_180_and_no_other_column()
    {
        Assert.False(FolderLayout.Plan(new WidgetArea(150, 100), new FolderConfig(), 4, true, false, false, false).Modified);
        var wide = FolderLayout.Plan(new WidgetArea(800, 500), new FolderConfig(), 4, true, false, false, false);
        Assert.True(wide.Modified);
        Assert.Empty(wide.Columns);
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
    public void S1323_chrome_appears_on_big_tiles_only()
    {
        var config = new FolderConfig { Style = FolderStyle.Details };

        var small = FolderLayout.Plan(new WidgetArea(200, 114), config, 20, true, searchOpen: false, navigate: true, hasGit: false);
        Assert.False(small.Breadcrumb);

        var big = FolderLayout.Plan(new WidgetArea(800, 524), config, 20, true, false, true, false);
        Assert.True(big.Breadcrumb);
        Assert.True(big.ColumnHeader);
        Assert.Equal(config.Columns, big.Columns);
    }

    [Fact]
    public void S1323_a_big_tile_has_a_breadcrumb_and_no_column_header_outside_details()
    {
        var plan = FolderLayout.Plan(new WidgetArea(800, 524), new FolderConfig { Style = FolderStyle.Grid }, 6, true, false, true, false);

        Assert.True(plan.Breadcrumb);
        Assert.False(plan.ColumnHeader);
        Assert.Empty(plan.Columns);
        Assert.NotNull(plan.Grid);
    }

    [Fact]
    public void S1323_an_open_search_box_is_shown_before_the_column_header()
    {
        var config = new FolderConfig { Style = FolderStyle.Details };

        var plan = FolderLayout.Plan(new WidgetArea(300, 96), config, 20, true, searchOpen: true, navigate: false, hasGit: false);

        Assert.True(plan.SearchBox);
        Assert.False(plan.ColumnHeader);
    }

    [Fact]
    public void S1326_the_header_holds_two_buttons_at_most_back_and_up_or_search_and_sort()
    {
        Assert.Equal(new FolderHeaderPlan(Back: true, Up: true, Search: false, Sort: false, Git: true), FolderLayout.Header(FolderStyle.List, navigate: true, hasGit: true));
        Assert.Equal(new FolderHeaderPlan(Back: false, Up: false, Search: true, Sort: true, Git: false), FolderLayout.Header(FolderStyle.Grid, navigate: false, hasGit: true));
        Assert.Equal(new FolderHeaderPlan(Back: false, Up: false, Search: true, Sort: false, Git: true), FolderLayout.Header(FolderStyle.Details, navigate: false, hasGit: true));

        foreach (var style in Enum.GetValues<FolderStyle>())
        {
            foreach (var navigate in new[] { true, false })
            {
                Assert.True(FolderLayout.Header(style, navigate, hasGit: true).Buttons <= 2);
            }
        }
    }
}
