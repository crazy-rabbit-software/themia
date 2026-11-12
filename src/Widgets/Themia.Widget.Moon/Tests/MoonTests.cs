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

        Assert.Equal(36 + 36 + 16 + 12, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight);
        Assert.Equal((140.0, 160.0, 320.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S11121_the_phase_month_is_one_size_seven_discs_across_five_rows_down_its_name_over_them()
    {
        var sizing = MoonLayout.Sizing(new MoonConfig { Style = MoonStyle.PhaseMonth });

        Assert.True(sizing.FixedHeight && sizing.FixedWidth);
        Assert.Equal(new SizeDip(24 + (7 * 28), 36 + 16 + 8 + (5 * 28) + 12), sizing.Natural);
        Assert.True(MoonLayout.Columns * MoonLayout.Rows >= 31);
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
