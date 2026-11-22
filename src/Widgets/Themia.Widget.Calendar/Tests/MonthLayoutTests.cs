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
    public void S11544_S14500_the_days_tile_the_card_edge_to_edge_and_top_to_foot(double width, double height)
    {
        var top = 36.0;
        var month = MonthLayouts.Plan(width, height, top, Grid, anchorRow: 2, mostItems: 2);

        // Across: from the left edge to the right, the width shared evenly, alike to a pixel.
        Assert.Equal(width, month.Columns.Sum(), 6);
        Assert.True(month.Columns.Max() - month.Columns.Min() <= 1 + 1e-6);

        // Down: the weekday line ends on a grid line, the weeks alike and at least their optimal, sharing the rest to the
        // card's foot: nothing left over.
        Assert.Equal(0, (top + month.HeaderHeight) % Grid.Pitch, 6);
        Assert.True(month.RowHeight >= Grid.Whole(MonthLayouts.LeastRow) - 1e-6);
        Assert.Equal(height, top + month.HeaderHeight + (month.Rows * month.RowHeight), 6);
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
        var tall = MonthLayouts.Plan(560, 600, 36, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(MonthFace.Chips, tall.Face);
        Assert.InRange(tall.ChipLanes, 1, 3);
        Assert.True(tall.NumberLine > FamilyType.Caption.LineHeight);

        // [S-14101] A wide, short month whose cells hold one chip line keeps its dots; a month with titles lists them.
        var wide = MonthLayouts.Plan(900, 300, 36, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(MonthFace.Dots, wide.Face);
        Assert.Equal(MonthFace.Chips, MonthLayouts.Plan(900, 300, 36, Grid, anchorRow: 0, mostItems: 3, MonthChipRoom.Titles).Face);

        // Nothing on: the number alone, as large as the cell lets it.
        var bare = MonthLayouts.Plan(420, 600, 36, Grid, anchorRow: 0, mostItems: 0);
        Assert.Equal(MonthFace.Number, bare.Face);
        Assert.True(bare.NumberLine >= tall.NumberLine);
        Assert.True(bare.NumberLine <= MonthLayouts.NumberFits(bare.LeastColumn) + 1e-6);
    }

    [Fact]
    public void S15782_the_number_grows_with_its_cell_until_its_two_figures_fill_the_column()
    {
        // 520 × 600 with dots and nothing on: columns of 74, weeks of about 95; the number's line was half the column (37).
        var bare = MonthLayouts.Plan(520, 600, 12, Grid, anchorRow: 0, mostItems: 0);
        Assert.Equal(MonthFace.Number, bare.Face);
        Assert.True(bare.NumberLine > bare.LeastColumn / 2);
        Assert.Equal(MonthLayouts.MostNumberLine, bare.NumberLine);

        // Two figures at that line fit across the column inside the cell's 4 each side.
        var size = MonthLayouts.NumberFits(80) * FamilyType.Caption.FontSize / FamilyType.Caption.LineHeight;
        Assert.Equal(80 - (2 * FamilySpace.S1), 2 * MonthLayouts.FigureAdvance * size, 6);
    }

    /// <summary>
    /// [S-14101] [S-15782] (owner, 2026-11-20: "the calendar shows the individual events too early, the items should stay
    /// small for a little longer."; WR-9: "until it clearly holds a readable chip line"): a dots month's cell shows chips
    /// only once it holds two chip lines under its number in a column where a title reads (56, a month with titles' least);
    /// a month with titles, chosen, from one chip line in that column.
    /// </summary>
    [Theory]
    [InlineData(560, 60 + (6 * 40), MonthFace.Dots)]    // a week of 40: the number and one chip line
    [InlineData(560, 60 + (6 * 50), MonthFace.Dots)]    // 50: one and a half
    [InlineData(560, 60 + (6 * 60), MonthFace.Chips)]   // 60: two chip lines under the number, columns of 80
    [InlineData(350, 60 + (6 * 80), MonthFace.Dots)]    // columns of 50: under a title's 56
    [InlineData(420, 60 + (6 * 80), MonthFace.Chips)]   // columns of 60
    [InlineData(520, 60 + (6 * 80), MonthFace.Chips)]   // columns of 74 (WR-9's 520 × 600)
    public void S14101_S15782_a_dots_month_keeps_its_dots_until_a_cell_holds_two_readable_chip_lines(double width, double height, MonthFace face)
    {
        var month = MonthLayouts.Plan(width, height, 40, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(face, month.Face);
        Assert.True(face == MonthFace.Dots ? month.ChipLanes == 0 : month.ChipLanes >= MonthChipRoom.Dots.Lanes);
    }

    [Fact]
    public void S14101_a_month_with_titles_shows_chips_from_one_line_in_its_least_column()
    {
        var titles = MonthLayouts.Plan(7 * 60, 60 + (6 * 40), 40, Grid, anchorRow: 0, mostItems: 3, MonthChipRoom.Titles);
        Assert.Equal((MonthFace.Chips, 1), (titles.Face, titles.ChipLanes));
        Assert.Equal(MonthFace.Dots, MonthLayouts.Plan(7 * 50, 60 + (6 * 40), 40, Grid, anchorRow: 0, mostItems: 3, MonthChipRoom.Titles).Face);
    }

    [Fact]
    public void S14101_S14500_at_every_cell_the_weeks_fill_the_card_from_under_the_weekday_line_to_its_foot()
    {
        foreach (var config in new[] { new Themia.Core.Model.CalendarWidgetConfig(), new Themia.Core.Model.CalendarWidgetConfig { MonthEvents = Themia.Core.Model.MonthEvents.Chips } })
        {
            var sizing = CalendarSizing.MonthSizing(config);
            for (var n = 0; n <= 25; n++)
            {
                var (width, height) = (Math.Ceiling(sizing.Min.Width / 10) * 10 + (n * 10), Math.Ceiling(sizing.Min.Height / 10) * 10 + (n * 10));
                var top = FamilySpace.Inset;
                var month = MonthLayouts.Plan(width, height, top, Grid, anchorRow: 2, mostItems: 2);

                Assert.Equal(CalendarSizing.MonthWeeks, month.Rows);
                Assert.Equal(height, top + month.HeaderHeight + (month.Rows * month.RowHeight), 6);
                Assert.True(month.Columns.Max() - month.Columns.Min() <= 1 + 1e-6);
                Assert.Equal(width, month.Columns.Sum(), 6);
            }
        }
    }

    [Fact]
    public void S11544_a_narrow_column_shows_dots_not_titles()
    {
        var narrow = MonthLayouts.Plan(260, 400, 36, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(MonthFace.Dots, narrow.Face);
        Assert.Equal(0, narrow.ChipLanes);
    }
}
