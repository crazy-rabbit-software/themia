using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Calendar.Tests;

/// <summary>
/// [S-11544] [S-12832] The month tiles the card (owner, 2026-11-19: "the calendar does not match the fucking design rules
/// i told you"; "and the items dont reach from edge to edge"): columns edge to edge in whole cells, weeks of whole cells
/// from under the weekday line, the left-over cells at the foot, and a cell filled by its content.
/// </summary>
public sealed class MonthLayoutTests
{
    private static readonly ItemGrid Grid = ItemGrid.Default();

    [Theory]
    [InlineData(340, 340)]
    [InlineData(520, 400)]
    [InlineData(420, 600)]
    [InlineData(900, 300)]
    [InlineData(333, 247)]
    public void S11544_the_days_tile_the_card_in_whole_cells_edge_to_edge(double width, double height)
    {
        var top = 36.0;
        var month = MonthLayouts.Plan(width, height, top, Grid, anchorRow: 2, mostItems: 2);

        // Across: from the left edge to the right, whole cells shared to a cell, the last taking the part cell.
        Assert.Equal(width, month.Columns.Sum(), 6);
        Assert.All(month.Columns.Take(6), c => Assert.True(Grid.IsWhole(c, 1e-6), $"{c}"));
        Assert.True(month.Columns.Take(6).Max() - month.Columns.Take(6).Min() <= Grid.Pitch + 1e-6);

        // Down: the weekday line ends on a grid line, the weeks whole cells and alike, the rest at the foot.
        Assert.Equal(0, (top + month.HeaderHeight) % Grid.Pitch, 6);
        Assert.True(Grid.IsWhole(month.RowHeight, 1e-6));
        var left = height - (top + month.HeaderHeight) - (month.Rows * month.RowHeight);
        Assert.InRange(left, -1e-6, month.Rows * Grid.Pitch);
    }

    [Fact]
    public void S11544_a_short_card_shows_the_weeks_it_holds_the_anchors_among_them()
    {
        var month = MonthLayouts.Plan(300, 120, 36, Grid, anchorRow: 4, mostItems: 0);

        Assert.InRange(month.Rows, 1, 5);
        Assert.InRange(4, month.FirstRow, month.FirstRow + month.Rows - 1);
        Assert.True(month.RowHeight >= MonthLayouts.LeastRow);
    }

    [Fact]
    public void S11544_a_cell_with_room_shows_its_days_events_as_chips_and_its_number_grows()
    {
        var tall = MonthLayouts.Plan(420, 600, 36, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(MonthFace.Chips, tall.Face);
        Assert.InRange(tall.ChipLanes, 1, 3);
        Assert.True(tall.NumberLine > FamilyType.Caption.LineHeight);

        // A wide, short month still lists its events: one line each.
        var wide = MonthLayouts.Plan(900, 300, 36, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(MonthFace.Chips, wide.Face);

        // Nothing on: the number alone, as large as the cell lets it.
        var bare = MonthLayouts.Plan(420, 600, 36, Grid, anchorRow: 0, mostItems: 0);
        Assert.Equal(MonthFace.Number, bare.Face);
        Assert.True(bare.NumberLine >= tall.NumberLine);
        Assert.True(bare.NumberLine <= bare.LeastColumn / 2 + 1e-6);
    }

    [Fact]
    public void S11544_a_narrow_column_shows_dots_not_titles()
    {
        var narrow = MonthLayouts.Plan(260, 400, 36, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(MonthFace.Dots, narrow.Face);
        Assert.Equal(0, narrow.ChipLanes);
    }
}
