using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1887] The folder widget builds what is in view and one viewport more, never the whole folder.</summary>
public class FolderRealizeTests
{
    [Fact]
    public void S1887_a_small_tile_builds_two_viewports_of_rows_not_the_folder()
    {
        // 140 items, 28 DIP rows, 84 DIPs in view (three rows): three in view, three ahead.
        Assert.Equal(6, FolderRealize.Target(140, 0, 0, 84, 28, 1, -1));
    }

    [Fact]
    public void S1887_a_folder_shorter_than_the_view_is_built_whole()
    {
        Assert.Equal(5, FolderRealize.Target(5, 0, 0, 600, 28, 1, -1));
        Assert.Equal(0, FolderRealize.Target(0, 0, 0, 600, 28, 1, -1));
    }

    [Fact]
    public void S1887_scrolling_builds_down_to_a_viewport_below_the_bottom()
    {
        // Scrolled to 280 (row 10), 84 in view: rows through 280 + 168 = 448 -> 16 rows.
        Assert.Equal(16, FolderRealize.Target(140, 6, 280, 84, 28, 1, -1));
    }

    [Fact]
    public void S1887_nothing_built_is_taken_back()
    {
        Assert.Equal(60, FolderRealize.Target(140, 60, 0, 84, 28, 1, -1));
    }

    [Fact]
    public void S1887_the_keyboard_cursor_row_is_always_built()
    {
        // End: the cursor on the last item builds everything up to it.
        Assert.Equal(140, FolderRealize.Target(140, 6, 0, 84, 28, 1, 139));
        Assert.Equal(51, FolderRealize.Target(140, 6, 0, 84, 28, 1, 50));
    }

    [Fact]
    public void S1887_tiles_build_whole_rows()
    {
        // Four to a row, 96 DIP cells, 200 in view: 400 DIPs -> 5 rows -> 20 tiles.
        Assert.Equal(20, FolderRealize.Target(140, 0, 0, 200, 96, 4, -1));

        // The cursor on tile 21 (row 5) builds that row whole.
        Assert.Equal(24, FolderRealize.Target(140, 0, 0, 200, 96, 4, 21));
    }

    [Fact]
    public void S1887_an_unmeasured_list_assumes_a_screenful()
    {
        var target = FolderRealize.Target(500, 0, 0, 0, 28, 1, -1);
        Assert.Equal(FolderRealize.UnmeasuredRows * 2, target);
        Assert.Equal(target, FolderRealize.Target(500, 0, double.NaN, double.NaN, 28, 1, -1));
    }

    [Fact]
    public void S1887_the_spacer_stands_for_exactly_the_rows_not_built()
    {
        Assert.Equal((140 - 6) * 28.0, FolderRealize.SpacerHeight(140, 6, 28, 1));
        Assert.Equal(0, FolderRealize.SpacerHeight(140, 140, 28, 1));

        // 10 tiles of 4 to a row is 3 rows built (the last part-filled); 140 is 35 rows.
        Assert.Equal((35 - 3) * 96.0, FolderRealize.SpacerHeight(140, 10, 96, 4));
    }

    [Fact]
    public void S1887_the_built_part_and_the_spacer_always_add_up_to_the_whole_list()
    {
        foreach (var columns in new[] { 1, 3, 4 })
        {
            for (var realized = 0; realized <= 30; realized++)
            {
                var builtRows = (realized + columns - 1) / columns;
                var allRows = (30 + columns - 1) / columns;
                Assert.Equal(allRows * 20.0, (builtRows * 20.0) + FolderRealize.SpacerHeight(30, realized, 20, columns));
            }
        }
    }
}
