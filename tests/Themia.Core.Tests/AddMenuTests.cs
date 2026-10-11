using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The edit bar's add menu ([S-3502..S-3505]): the newspaper flow of its sections through columns,
/// how many columns a screen gets, what a search leaves and in which order, and the arrow keys.
/// </summary>
public sealed class AddMenuTests
{
    /// <summary>Today's picker sections: Time 5, Accounts 4, System 20, Power 7, Information 4, Media 4, Launch 2, Desk 4, Trackers 1.</summary>
    private static readonly int[] Catalogue = [5, 4, 20, 7, 4, 4, 2, 4, 1];

    private static void AssertNoHeadingLast(AddMenuGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            Assert.False(column[^1].IsHeading, "a heading ended a column");
        }
    }

    [Fact]
    public void S3502_sections_flow_down_a_column_and_on_into_the_next()
    {
        var grid = AddMenuLayout.Flow([3, 2], 4);

        Assert.Equal(2, grid.Columns.Count);
        Assert.Equal([new(0, -1), new(0, 0), new(0, 1), new(0, 2)], grid.Columns[0]);
        Assert.Equal([new(1, -1), new(1, 0), new(1, 1)], grid.Columns[1]);
    }

    [Fact]
    public void S3502_a_long_section_carries_on_at_the_top_of_the_next_column()
    {
        var grid = AddMenuLayout.Flow([6], 4);

        Assert.Equal(2, grid.Columns.Count);
        Assert.Equal(new AddMenuLine(0, 3), grid.Columns[1][0]);
        Assert.Equal(6, grid.EntryCount);
    }

    [Fact]
    public void S3502_a_heading_is_never_the_last_line_of_a_column()
    {
        // Section 0 fills three of four lines; section 1's heading would be fourth, alone.
        var grid = AddMenuLayout.Flow([2, 2], 4);

        Assert.Equal(3, grid.Columns[0].Count);
        Assert.True(grid.Columns[1][0].IsHeading);
        AssertNoHeadingLast(grid);

        for (var rows = 2; rows <= 30; rows++)
        {
            AssertNoHeadingLast(AddMenuLayout.Flow(Catalogue, rows));
        }
    }

    [Fact]
    public void S3502_an_empty_section_is_left_out_heading_and_all()
    {
        var grid = AddMenuLayout.Flow([0, 2, 0], 10);

        Assert.Single(grid.Columns);
        Assert.Equal([new(1, -1), new(1, 0), new(1, 1)], grid.Columns[0]);
    }

    [Fact]
    public void S3502_nothing_to_show_is_no_columns()
    {
        var grid = AddMenuLayout.Flow([], 10);

        Assert.Empty(grid.Columns);
        Assert.Equal(0, grid.EntryCount);
    }

    [Fact]
    public void S3502_rows_for_columns_balances_them_as_evenly_as_the_headings_allow()
    {
        // 51 entries and 9 headings: 60 lines, 15 to a column across four.
        Assert.Equal(15, AddMenuLayout.RowsFor(Catalogue, 4));
        Assert.Equal(4, AddMenuLayout.Flow(Catalogue, 15).Columns.Count);

        // [2, 2] in two columns: 3 lines each, nothing pushed.
        Assert.Equal(3, AddMenuLayout.RowsFor([2, 2], 2));

        // One column holds everything.
        Assert.Equal(60, AddMenuLayout.RowsFor(Catalogue, 1));
    }

    [Fact]
    public void S3502_rows_for_never_needs_more_columns_than_asked()
    {
        for (var columns = 1; columns <= 8; columns++)
        {
            var rows = AddMenuLayout.RowsFor(Catalogue, columns);
            Assert.True(AddMenuLayout.Flow(Catalogue, rows).Columns.Count <= columns);
        }
    }

    [Fact]
    public void S3503_a_1080p_work_area_takes_the_fewest_columns_that_fit_its_height()
    {
        // 1920 x 1032 DIPs at 32-DIP lines: 70 % of the height is 22 lines, so three columns of 20.
        var (columns, rows) = AddMenuLayout.Fit(Catalogue, 1920, 1032, 216, 16, 32);

        Assert.Equal(3, columns);
        Assert.Equal(20, rows);
    }

    [Fact]
    public void S3503_a_short_screen_takes_more_columns()
    {
        var (columns, rows) = AddMenuLayout.Fit(Catalogue, 1920, 600, 216, 16, 32);

        Assert.True(columns > 3);
        Assert.True(rows * 32 <= 600 * AddMenuLayout.MostHeightShare);
    }

    [Fact]
    public void S3503_never_more_than_six_columns_nor_wider_than_three_quarters_of_the_screen()
    {
        var (wide, _) = AddMenuLayout.Fit(Catalogue, 10000, 100, 216, 16, 32);
        Assert.True(wide <= AddMenuLayout.MostColumns);

        // 800 wide: 600 usable holds two 216-DIP columns and their gap, and no third.
        var (narrow, rows) = AddMenuLayout.Fit(Catalogue, 800, 100, 216, 16, 32);
        Assert.Equal(2, narrow);
        Assert.Equal(AddMenuLayout.RowsFor(Catalogue, 2), rows);
    }

    [Fact]
    public void S3503_a_screen_narrower_than_a_column_still_gets_one()
    {
        var (columns, _) = AddMenuLayout.Fit([3], 100, 1000, 216, 16, 32);

        Assert.Equal(1, columns);
    }

    private static IndexedWidget Index(string label, string? keyword = null)
        => WidgetSearch.Index(new WidgetSearchDoc(label, [label], keyword is null ? null : [keyword]));

    private static readonly (IndexedWidget, string, string)[] Items =
    [
        (Index("Clock"), "clock", "Time"),
        (Index("Countdown"), "countdown", "Time"),
        (Index("CPU", "processor"), "cpu", "System"),
        (Index("Network"), "network", "System"),
        (Index("Network details"), "netdetails", "System"),
        (Index("Notes"), "notes", "Desk"),
    ];

    [Fact]
    public void S3504_nothing_typed_lists_everything_in_the_order_given()
    {
        var sections = AddMenuLayout.Filter(Items, "  ");

        Assert.Equal(["Time", "System", "Desk"], sections.Select(s => s.Section));
        Assert.Equal(["cpu", "network", "netdetails"], sections[1].Items);
    }

    [Fact]
    public void S3504_a_query_keeps_only_the_matches_in_their_sections()
    {
        var sections = AddMenuLayout.Filter(Items, "network");

        var section = Assert.Single(sections);
        Assert.Equal("System", section.Section);
        Assert.Equal(["network", "netdetails"], section.Items);
    }

    [Fact]
    public void S3504_the_section_with_the_best_match_comes_first_so_the_first_entry_is_the_best()
    {
        // Desk comes after Time and System in the catalogue, but holds the one match.
        var sections = AddMenuLayout.Filter(Items, "notes");

        Assert.Equal("Desk", sections[0].Section);
        Assert.Equal("notes", sections[0].Items[0]);
    }

    [Fact]
    public void S3504_no_match_is_no_sections()
    {
        Assert.Empty(AddMenuLayout.Filter(Items, "zzzzqqq"));
    }

    // Three columns of up to four lines: [A: a0 a1 a2] [B: b0 b1 b2] [C: c0].
    private static AddMenuGrid Grid() => AddMenuLayout.Flow([3, 3, 1], 4);

    [Fact]
    public void S3505_down_from_the_search_box_goes_to_the_first_entry_and_up_comes_back()
    {
        var grid = Grid();

        Assert.Equal(0, AddMenuKeys.Move(grid, -1, WidgetKey.Down));
        Assert.Equal(-1, AddMenuKeys.Move(grid, 0, WidgetKey.Up));
        Assert.Equal(-1, AddMenuKeys.Move(grid, -1, WidgetKey.Up));
    }

    [Fact]
    public void S3505_down_at_a_columns_foot_runs_on_into_the_next_and_stops_at_the_last()
    {
        var grid = Grid();

        // Entry 2 is a2, the foot of column 0; entry 3 is b0, under column 1's heading.
        Assert.Equal((0, 3), grid.SpotOf(2));
        Assert.Equal(3, AddMenuKeys.Move(grid, 2, WidgetKey.Down));
        Assert.Equal((1, 1), grid.SpotOf(3));
        Assert.Equal(6, AddMenuKeys.Move(grid, 6, WidgetKey.Down));
        Assert.Equal(2, AddMenuKeys.Move(grid, 3, WidgetKey.Up));
    }

    [Fact]
    public void S3505_left_and_right_go_to_the_nearest_row_of_the_column_beside()
    {
        var grid = Grid();

        // a1 (row 2) -> b1 (row 2) -> c0 (row 1, the only entry) and back to b0 (row 1).
        Assert.Equal(4, AddMenuKeys.Move(grid, 1, WidgetKey.Right));
        Assert.Equal(6, AddMenuKeys.Move(grid, 4, WidgetKey.Right));
        Assert.Equal(3, AddMenuKeys.Move(grid, 6, WidgetKey.Left));

        // a0 sits on row 1, b0 on row 1 too.
        Assert.Equal(3, AddMenuKeys.Move(grid, 0, WidgetKey.Right));
    }

    [Fact]
    public void S3505_left_and_right_stay_at_the_outer_columns()
    {
        var grid = Grid();

        Assert.Equal(0, AddMenuKeys.Move(grid, 0, WidgetKey.Left));
        Assert.Equal(6, AddMenuKeys.Move(grid, 6, WidgetKey.Right));
    }

    [Fact]
    public void S3505_on_the_search_box_the_caret_keys_are_the_texts_own()
    {
        var grid = Grid();

        Assert.Null(AddMenuKeys.Move(grid, -1, WidgetKey.Left));
        Assert.Null(AddMenuKeys.Move(grid, -1, WidgetKey.Right));
        Assert.Null(AddMenuKeys.Move(grid, -1, WidgetKey.Home));
        Assert.Null(AddMenuKeys.Move(grid, -1, WidgetKey.End));
        Assert.Null(AddMenuKeys.Move(grid, 2, WidgetKey.Enter));
    }

    [Fact]
    public void S3505_home_and_end_go_to_the_first_and_last_entry()
    {
        var grid = Grid();

        Assert.Equal(0, AddMenuKeys.Move(grid, 4, WidgetKey.Home));
        Assert.Equal(6, AddMenuKeys.Move(grid, 1, WidgetKey.End));
    }

    [Fact]
    public void S3505_an_empty_menu_keeps_the_highlight_on_the_box()
    {
        var grid = AddMenuLayout.Flow([], 4);

        Assert.Equal(-1, AddMenuKeys.Move(grid, -1, WidgetKey.Down));
        Assert.Null(AddMenuKeys.Move(grid, -1, WidgetKey.Right));
    }

    [Fact]
    public void S3505_a_highlight_past_a_shrunk_menu_is_pulled_back_in()
    {
        var grid = AddMenuLayout.Flow([2], 4);

        Assert.Equal(1, AddMenuKeys.Move(grid, 9, WidgetKey.Down));
    }
}
