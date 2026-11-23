using Themia.Core.Office;
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
        var tall = MonthLayouts.Plan(910, 600, 36, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(MonthFace.Chips, tall.Face);
        Assert.InRange(tall.ChipLanes, 1, 3);
        Assert.True(tall.NumberLine > FamilyType.Caption.LineHeight);

        // [S-14101] A wide, short month whose cells hold one chip line keeps its dots; a month with titles lists them.
        var wide = MonthLayouts.Plan(900, 300, 36, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(MonthFace.Dots, wide.Face);
        Assert.Equal(MonthFace.Chips, MonthLayouts.Plan(910, 300, 36, Grid, anchorRow: 0, mostItems: 3, MonthChipRoom.Titles).Face);

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
    [InlineData(700, 60 + (6 * 40), MonthFace.Dots)]    // a week of 40: the number and one chip line
    [InlineData(700, 60 + (6 * 50), MonthFace.Dots)]    // 50: one and a half
    [InlineData(910, 60 + (6 * 60), MonthFace.Chips)]   // 60: two chip lines under the number, columns of 130
    [InlineData(420, 60 + (6 * 80), MonthFace.Dots)]    // columns of 60: a title of three letters
    [InlineData(520, 60 + (6 * 80), MonthFace.Dots)]    // columns of 74: five or six (WR-9's 520 × 600)
    [InlineData(600, 60 + (6 * 80), MonthFace.Dots)]    // columns of 86: "Brunch w…"
    [InlineData(700, 60 + (6 * 80), MonthFace.Dots)]    // [S-16448] columns of 100: "09:00 Stand…"
    [InlineData(910, 60 + (6 * 80), MonthFace.Chips)]   // columns of 130: its start and a title of words
    public void S14101_S15792_a_dots_month_keeps_its_dots_until_a_cell_holds_two_readable_chip_lines(double width, double height, MonthFace face)
    {
        var month = MonthLayouts.Plan(width, height, 40, Grid, anchorRow: 0, mostItems: 3);
        Assert.Equal(face, month.Face);
        Assert.True(face == MonthFace.Dots ? month.ChipLanes == 0 : month.ChipLanes >= MonthChipRoom.Dots.Lanes);
    }

    [Fact]
    public void S14101_a_month_with_titles_shows_chips_from_one_line_in_its_least_column()
    {
        var titles = MonthLayouts.Plan(7 * 130, 60 + (6 * 40), 40, Grid, anchorRow: 0, mostItems: 3, MonthChipRoom.Titles);
        Assert.Equal((MonthFace.Chips, 1), (titles.Face, titles.ChipLanes));
        Assert.Equal(MonthFace.Dots, MonthLayouts.Plan(7 * 60, 60 + (6 * 40), 40, Grid, anchorRow: 0, mostItems: 3, MonthChipRoom.Titles).Face); // [S-15792]
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
                var top = FamilySpace.Inset + LineRules.Band(Grid);
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

    [Fact]
    public void S15794_a_larger_card_never_draws_smaller_numbers()
    {
        // A dots month 300 wide from its least up (its dots stand in the cell's foot padding), and a month with titles with
        // chips from 700 × 510 up (the number takes what its least chip line leaves): the day's number never shrinks as the
        // card grows within a face.
        foreach (var (chips, widths, heights) in new[]
        {
            (MonthChipRoom.Dots, new[] { 300.0 }, Enumerable.Range(0, 30).Select(i => 210.0 + (i * 10)).ToArray()),
            (MonthChipRoom.Titles, Enumerable.Range(0, 11).Select(i => 700.0 + (i * 20)).ToArray(), new[] { 510.0 * 392 / 700 }),
        })
        {
            var last = 0.0;
            foreach (var width in widths)
            {
                foreach (var height in heights)
                {
                    var scale = chips.Equals(MonthChipRoom.Titles) ? width / 392 : 1;
                    var month = MonthLayouts.Plan(width, height * scale, 12, Grid, anchorRow: 2, mostItems: 3, chips);
                    Assert.True(month.NumberLine >= last - 1e-6, $"{width} x {height * scale}: {month.NumberLine} after {last}");
                    last = month.NumberLine;
                }
            }
        }
    }

    /// <summary>
    /// [S-16442] A day's dots keep its cell's padding under them where the room under the figures holds them (WR-3: they stood
    /// on the cell's foot, the last week's on the card's border), and sink into it only as far as the figures need, never
    /// past it.
    /// </summary>
    [Theory]
    [InlineData(88, 40)]
    [InlineData(48, 32)]
    [InlineData(25, 17)]
    [InlineData(24, 16)]
    [InlineData(20, 16)]
    public void S16442_the_dots_keep_the_cells_padding_under_them_and_clear_the_figures(double row, double line)
    {
        var face = row - (2 * FamilySpace.S1);
        var sink = MonthLayouts.DotSink(row, line);
        var under = LineInk.Of(new TypeStep(line * FamilyType.Caption.FontSize / FamilyType.Caption.LineHeight, line, false)).Under + Math.Max(0, (face - line) / 2);

        Assert.InRange(sink, 0, FamilySpace.S1);
        // The dot and its gap fit under the figures, the sink only what the room is short of (no sink where it holds them).
        Assert.True(under + sink >= CalendarFit.Dot + MonthLayouts.DotGap - 1e-9 || sink == FamilySpace.S1);
        Assert.Equal(0, MonthLayouts.DotSink(88, 40));
        Assert.True(MonthLayouts.DotSink(25, 17) < FamilySpace.S1);
    }

    [Fact]
    public void S15798_the_months_least_holds_a_readable_number_in_every_cell()
    {
        var sizing = CalendarSizing.MonthSizing(new Themia.Core.Model.CalendarWidgetConfig { MonthEvents = Themia.Core.Model.MonthEvents.Dots });
        Assert.Equal((160.0, 210.0), (sizing.Min.Width, sizing.Min.Height));

        // At its least every week's number is a Caption line, its two figures across the column, under the period's line.
        var least = MonthLayouts.Plan(sizing.Min.Width, sizing.Min.Height, FamilySpace.Inset + LineRules.Band(Grid), Grid, anchorRow: 2, mostItems: 2);
        Assert.Equal(FamilyType.Caption.LineHeight, least.NumberLine);
        Assert.True(MonthLayouts.NumberFits(least.LeastColumn) >= FamilyType.Caption.LineHeight);
    }

    /// <summary>[S-16449] A day's dots stand whole in its cell's padding: as many a line as fit, the "more" one among them.</summary>
    [Theory]
    [InlineData(160.0 / 7, 2)]
    [InlineData(32, 3)]
    [InlineData(100, 4)]
    [InlineData(10, 1)]
    public void S16449_a_days_dots_stand_whole_in_its_cell(double column, int slots)
    {
        Assert.Equal(slots, MonthLayouts.DotSlots(column));
        Assert.True(slots == 1 || (slots * CalendarFit.Dot) + ((slots - 1) * FamilySpace.S1) <= column - (2 * FamilySpace.S1) + LayoutMath.Tolerance);
    }

    /// <summary>[S-16448] Chips only where they read (time and a title of words), dots narrower: a 100 column keeps its dots.</summary>
    [Fact]
    public void S16448_a_month_shows_chips_only_where_they_read()
    {
        Assert.Equal(MonthFace.Dots, MonthLayouts.Plan(7 * 100, 60 + (6 * 40), 40, Grid, anchorRow: 0, mostItems: 3, MonthChipRoom.Titles).Face);
        Assert.Equal(MonthFace.Chips, MonthLayouts.Plan(7 * 130, 60 + (6 * 40), 40, Grid, anchorRow: 0, mostItems: 3, MonthChipRoom.Titles).Face);
    }
}
