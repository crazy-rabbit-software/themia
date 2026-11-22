using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Moon.Tests;

/// <summary>
/// [S-11121] [S-6051] The moon reading: its sizes and the month of phases.
/// </summary>
public sealed class MoonTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public void S11121_the_disc_is_the_28_readout_and_its_line_under_at_one_height_140_to_320_wide()
    {
        var sizing = MoonLayout.Sizing(new MoonConfig());

        Assert.Equal(12 + 36 + 16 + 12, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight);
        Assert.Equal((140.0, 160.0, 320.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S11121_S15805_the_phase_month_is_seven_discs_across_five_rows_down_its_name_over_them_down_to_12_discs()
    {
        var sizing = MoonLayout.Sizing(new MoonConfig { Style = MoonStyle.PhaseMonth });

        // [S-15805] WR-14: down to where a cell's disc is the glyph floor, 12, its name kept over the cells.
        var room = Math.Ceiling(ScaleFloor.Glyph * MoonLayout.Pitch / MoonLayout.Disc);
        Assert.Equal(new SizeDip(7 * room, 12 + 16 + 8 + (5 * room)), sizing.Min);
        Assert.True(MoonLayout.Cells(sizing.Min.Width, sizing.Min.Height - 12 - 16 - 8, 31).Disc >= ScaleFloor.Glyph);
        // [S-15673] The grid its discs' ink: six pitches and a disc across, four and a disc down.
        Assert.Equal(new SizeDip(24 + (6 * 28) + 20, 12 + 16 + 8 + (4 * 28) + 20 + 12), sizing.Natural);
        Assert.True(MoonLayout.Columns * MoonLayout.Rows >= 31);
    }

    [Theory]
    [InlineData(212, 144, 31)]
    [InlineData(900, 264, 30)]
    [InlineData(330, 94, 28)]
    public void S15805_the_months_day_cells_are_the_cards_grid_a_disc_in_the_middle_of_each(double width, double height, int days)
    {
        // WR-9, WR-2, WR-3: at 900x300 the discs had stood on a fixed 28 pitch, 275 DIP of the card empty either side.
        var cells = MoonLayout.Cells(width, height, days);

        Assert.Equal((days + 6) / 7, cells.Rows);
        Assert.Equal(width, MoonLayout.Columns * cells.CellWidth, 6);
        Assert.Equal(height, cells.Rows * cells.CellHeight, 6);

        // The disc keeps its share of the cell's shorter side (a 20 in a 28), today's ring the whole of it.
        Assert.Equal(Math.Min(cells.CellWidth, cells.CellHeight), cells.Ring, 6);
        Assert.Equal(cells.Ring * MoonLayout.Disc / MoonLayout.Pitch, cells.Disc, 6);
        Assert.True(cells.Disc < cells.Ring);
    }

    [Fact]
    public void S15805_a_new_month_card_draws_its_discs_at_their_own_size()
    {
        // The natural card (212x180) gives the cells 212 x 144 (its name and the gap over them): about the old 28 pitch.
        var sizing = MoonLayout.Sizing(new MoonConfig { Style = MoonStyle.PhaseMonth });
        var cells = MoonLayout.Cells(sizing.Natural.Width, sizing.Natural.Height - 12 - FamilyType.Caption.LineHeight - FamilySpace.S2, 31);
        Assert.InRange(cells.Disc, MoonLayout.Disc - 1, MoonLayout.Disc + 2);
    }
    [Fact]
    public void S6051_the_phase_month_is_every_day_of_this_month_today_marked_once()
    {
        var now = new DateTimeOffset(2026, 2, 10, 20, 0, 0, TimeSpan.FromHours(1));
        var days = MoonMonth.Days(now, Zurich);

        Assert.Equal(28, days.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), days[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 10), days.Single(d => d.Today).Date);

        // A day on, the moon is a 29.53rd of its cycle further, wrapping at the new moon.
        for (var i = 1; i < days.Count; i++)
        {
            var step = (days[i].Phase - days[i - 1].Phase + 1) % 1;
            Assert.Equal(1 / 29.53058867, step, 3);
        }

        Assert.Equal(MoonPhase.At(new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.FromHours(1))).Phase, days[9].Phase, 9);
    }
}
