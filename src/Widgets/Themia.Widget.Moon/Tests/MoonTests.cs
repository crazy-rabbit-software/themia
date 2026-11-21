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
    public void S11121_the_phase_month_is_one_size_seven_discs_across_five_rows_down_its_name_over_them()
    {
        var sizing = MoonLayout.Sizing(new MoonConfig { Style = MoonStyle.PhaseMonth });

        Assert.True(sizing.FixedHeight && sizing.FixedWidth);
        // [S-15673] The grid its discs' ink: six pitches and a disc across, four and a disc down.
        Assert.Equal(new SizeDip(24 + (6 * 28) + 20, 12 + 16 + 8 + (4 * 28) + 20 + 12), sizing.Natural);
        Assert.True(MoonLayout.Columns * MoonLayout.Rows >= 31);
    }

    [Fact]
    public void S15673_the_month_grids_box_is_its_ink_the_discs_and_todays_ring_where_it_stands_on_an_edge()
    {
        // Thirty days, today inside: the discs from every edge, five lines.
        Assert.Equal(new MoonGridBox((6 * 28) + 20, (4 * 28) + 20, 0, 0), MoonLayout.GridOf(30, 9));

        // Today in the last column and the first line: its ring 4 past the disc on the right and the top.
        Assert.Equal(new MoonGridBox((6 * 28) + 20 + 4, 4 + (4 * 28) + 20, 0, 4), MoonLayout.GridOf(30, 6));

        // Today the 29th of thirty: the last line's, in its first column; the ring on the left and the foot.
        Assert.Equal(new MoonGridBox(4 + (6 * 28) + 20, (4 * 28) + 20 + 4, 4, 0), MoonLayout.GridOf(30, 28));

        // A February of 28: four lines; no today: no ring; the right edge is the full lines', a short last line's today none.
        Assert.Equal(new MoonGridBox((6 * 28) + 20, (3 * 28) + 20, 0, 0), MoonLayout.GridOf(28, -1));
        Assert.Equal((6 * 28) + 20, MoonLayout.GridOf(31, 29).Width);
        Assert.Equal(MoonLayout.Ring, (MoonLayout.Pitch - MoonLayout.Disc) / 2);
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
