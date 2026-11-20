using System.Buffers.Binary;
using System.Text;
using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
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
        Assert.False(g.Release());
    }

    [Fact]
    public void S1328_a_press_on_an_item_becomes_a_drag_once_and_suppresses_the_click()
    {
        var g = new FolderGesture();
        g.BeginPress(10, 10, FolderPress.Item, [@"C:\a"]);

        Assert.Equal(GestureStep.StartDrag, g.Move(20, 10));
        Assert.Equal([@"C:\a"], g.DragPaths);
        Assert.Equal(GestureStep.Idle, g.Move(40, 10));
        Assert.True(g.Release());
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
        g.BeginPress(0, 0, FolderPress.Item, [@"C:\a"]);
        var generation = g.Generation;

        g.Release();

        Assert.False(g.Pressed);
        Assert.NotEqual(generation, g.Generation);
        Assert.Equal(GestureStep.Idle, g.Move(100, 100));
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

    // ---- the layout and the sizing ----------------------------------------------------------------

    [Theory]
    [InlineData(FolderStyle.Grid, 330, true, 4)]
    [InlineData(FolderStyle.Icons, 330, false, 7)]
    [InlineData(FolderStyle.Grid, 560, true, 7)]
    public void S11203_cells_are_the_family_s_32_at_every_width_and_more_room_shows_more_of_them(FolderStyle style, double width, bool named, int columns)
    {
        var plan = FolderLayout.Plan(new WidgetArea(width, 300), new FolderConfig { Style = style }, 20, true, false, false);
        var grid = plan.Grid!.Value;

        Assert.Equal(32, grid.Icon);
        Assert.Equal(named, grid.Named);
        Assert.Equal(columns, grid.Columns);
        Assert.Equal(Themia.Sdk.Kit.RowRules.CellHeight(32, named), grid.Height);
    }

    [Theory]
    [InlineData(108, 2)]
    [InlineData(228, 6)]
    [InlineData(548, 18)]
    public void S1323_rows_are_the_row_block_s_one_line_of_28_and_only_whole_ones_show(double height, int rows)
    {
        var area = new WidgetArea(320, height);
        var plan = FolderLayout.Plan(area, new FolderConfig(), 40, true, false, false);

        Assert.Null(plan.Grid);
        Assert.Equal(28, FolderLayout.RowHeight);
        Assert.Equal(rows, FolderLayout.Rows(FolderLayout.ItemsHeight(area, plan.SearchBox, plan.ColumnHeader, plan.Breadcrumb)));
    }

    [Fact]
    public void S11203_what_stands_over_the_items_is_the_configs_at_every_size()
    {
        var plain = new FolderConfig { Style = FolderStyle.Details };
        foreach (var area in new[] { new WidgetArea(200, 114), new WidgetArea(800, 524) })
        {
            var plan = FolderLayout.Plan(area, plain, 20, true, false, false);
            Assert.False(plan.Breadcrumb);
            Assert.False(plan.SearchBox);
            Assert.True(plan.ColumnHeader);
            Assert.Equal(plain.Columns, plan.Columns);
        }

        var all = new FolderConfig { Style = FolderStyle.Details, SearchBox = true, ShowPath = true, ColumnTitles = false };
        var small = FolderLayout.Plan(new WidgetArea(200, 114), all, 20, true, false, false);
        Assert.True(small.Breadcrumb && small.SearchBox && !small.ColumnHeader);

        Assert.True(FolderLayout.Plan(new WidgetArea(150, 100), new FolderConfig(), 4, true, false, false).Modified);
        Assert.False(FolderLayout.Plan(new WidgetArea(800, 500), new FolderConfig { ShowDate = false }, 4, true, false, false).Modified);
        Assert.Empty(FolderLayout.Plan(new WidgetArea(800, 500), new FolderConfig(), 4, true, false, false).Columns);
        Assert.True(FolderLayout.Plan(new WidgetArea(300, 96), all, 20, true, navigate: false, hasGit: false).SearchBox);
    }

    [Fact]
    public void S11203_a_folder_keeps_a_free_size_in_its_range_and_details_is_as_wide_as_its_columns()
    {
        // [S-12834] Over the items: the card's inset and the tool line's whole-cell band (12 + 28 = 40). [S-14102] The list's
        // rows run to the card's foot: a new one the whole rows of 28 nearest 280 (on whole cells 280: eight rows of 30), its
        // range free from 160 to 640 ([S-11203]), every cell between.
        var list = FolderLayout.Sizing(new FolderConfig());
        Assert.Equal((new SizeDip(220, 160), new SizeDip(300, 276), new SizeDip(800, 640)), (list.Min, list.Natural, list.Max));
        Assert.Equal(40 + (8 * 30), Math.Ceiling(list.Natural.Height / 10) * 10);
        Assert.Equal(40 + (4 * 64) - 12 + 12, FolderLayout.Sizing(new FolderConfig { Style = FolderStyle.Grid }).Natural.Height);
        Assert.Empty(list.Problems());

        var details = new FolderConfig { Style = FolderStyle.Details };
        var least = 24 + 24 + 120 + (72 + 12) + (96 + 12) + (120 + 12);
        Assert.Equal(least, FolderLayout.Sizing(details).Min.Width);
        Assert.Equal(least + 60, FolderLayout.Sizing(details).Natural.Width);

        details.Columns = [.. FolderColumns.All];
        Assert.Empty(FolderLayout.Sizing(details).Problems());
        Assert.True(FolderLayout.Sizing(details).Min.Width <= 800);

        // Everything over the items on: the least height holds them and one row.
        var tall = new FolderConfig { Style = FolderStyle.Details, SearchBox = true, ShowPath = true };
        // [S-12834] The search box is the family's control of 28, the items right under it; the least holds the tool line, the
        // path, the box, the titles and one row of 28 in the insets, never under the folder's least (160).
        Assert.Equal(160, FolderLayout.Sizing(tall).Min.Height);
        Assert.True(FolderLayout.LeastHeight(tall) >= 12 + 28 + 28 + 28 + 28 + 12);
    }

    /// <summary>
    /// [S-14102] (owner, 2026-11-20: "right now all of the widgets have a space on the bottom, this means their size steps are
    /// not in sync with the item heights"; [S-14500]): at every height the list's and the table's rows run from right under
    /// what stands over them (each a band of whole cells) to the card's foot, none left over.
    /// </summary>
    [Fact]
    public void S14102_S14810_the_rows_stand_under_the_bands_at_their_size_their_room_the_cards_foot_at_every_cell()
    {
        var grid = ItemGrid.Default();
        foreach (var config in new[] { new FolderConfig(), new FolderConfig { ShowPath = true, SearchBox = true }, new FolderConfig { Style = FolderStyle.Details, SearchBox = true, ShowPath = true } })
        {
            var header = config.Style == FolderStyle.Details && config.ColumnTitles;
            var least = Math.Ceiling(FolderLayout.Sizing(config).Min.Height / 10) * 10;
            for (var card = least; card <= least + 300; card += 10)
            {
                var area = new WidgetArea(300, card - Themia.Sdk.Kit.FamilySpace.Inset, grid.Pitch);
                var height = FolderLayout.ItemsHeight(area, config.SearchBox, header, config.ShowPath, rows: true);
                var reach = FolderLayout.Reach(height, grid);

                Assert.Equal(grid.Whole(FolderLayout.RowHeight), reach.Pitch, 6);
                Assert.Equal(card, FolderLayout.Over(grid, config.SearchBox, header, config.ShowPath) + reach.Room, 6);
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
            List<FolderColumn> shown = [.. FolderColumns.All.Where(c => c != last), last];
            for (var i = 0; i < shown.Count; i++)
            {
                var end = FolderColumns.AlignsRight(shown[i], last: i == shown.Count - 1);
                Assert.True(i < shown.Count - 1 || end, $"{shown[i]} last");
            }
        }

        Assert.False(FolderColumns.AlignsRight(FolderColumn.Type, last: false));
        Assert.True(FolderColumns.AlignsRight(FolderColumn.Size, last: false));
    }

    [Fact]
    public void S1326_the_header_holds_two_buttons_at_most_back_and_up_or_search_and_sort()
    {
        FolderConfig Of(FolderStyle style) => new() { Style = style };
        Assert.Equal(new FolderHeaderPlan(Back: true, Up: true, Search: false, Sort: false, Git: true), FolderLayout.Header(Of(FolderStyle.List), navigate: true, hasGit: true));
        Assert.Equal(new FolderHeaderPlan(Back: false, Up: false, Search: true, Sort: true, Git: false), FolderLayout.Header(Of(FolderStyle.Grid), navigate: false, hasGit: true));
        Assert.Equal(new FolderHeaderPlan(Back: false, Up: false, Search: true, Sort: false, Git: true), FolderLayout.Header(Of(FolderStyle.Details), navigate: false, hasGit: true));
        Assert.False(FolderLayout.Header(new FolderConfig { SearchBox = true }, navigate: false, hasGit: false).Search);

        foreach (var style in Enum.GetValues<FolderStyle>())
        {
            foreach (var navigate in new[] { true, false })
            {
                Assert.True(FolderLayout.Header(Of(style), navigate, hasGit: true).Buttons <= 2);
            }
        }
    }
}
