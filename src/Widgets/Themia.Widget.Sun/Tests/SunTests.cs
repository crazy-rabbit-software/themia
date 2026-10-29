using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Sun.Tests;

/// <summary>
/// [S-3102] [S-6045] [S-6046] The Sun widget: its ladder on the family's blocks, its styles' repair and the day bar.
/// </summary>
public sealed class SunTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public void S6045_an_unknown_sun_style_is_repaired_to_the_arc()
    {
        var config = (SunConfig)ConfigJson.Read("Sun", """{ "kind": "sun", "style": "numbers" }""");

        Assert.Equal(SunStyle.Arc, config.Style);
    }

    [Fact]
    public void S6046_the_day_bar_places_sunrise_sunset_and_now_by_their_share_of_the_local_day()
    {
        var now = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.FromHours(2));
        var times = new SunTimes(new DateTimeOffset(2026, 6, 21, 6, 0, 0, TimeSpan.FromHours(2)), new DateTimeOffset(2026, 6, 21, 21, 0, 0, TimeSpan.FromHours(2)), PolarState.None);

        var bar = SunDayBar.Of(now, times, Zurich);
        Assert.Equal(0.25, bar.Rise!.Value, 6);
        Assert.Equal(21 / 24d, bar.Set!.Value, 6);
        Assert.Equal(0.5, bar.Now, 6);
        Assert.True(bar.NowLit);
        Assert.Equal((0.25, 21 / 24d), (Math.Round(bar.Daylight.From, 6), Math.Round(bar.Daylight.To, 6)));
    }

    [Fact]
    public void S6046_a_spring_forward_day_is_23_hours_across_and_the_polar_states_light_all_or_nothing()
    {
        // 29 March 2026: 02:00 is 03:00 in Zurich. 12:00 local is 11 real hours in, of 23.
        var noon = new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.FromHours(2));
        var none = new SunTimes(null, null, PolarState.None);
        Assert.Equal(11 / 23d, SunDayBar.Of(noon, none, Zurich).Now, 6);

        var day = SunDayBar.Of(noon, new SunTimes(null, null, PolarState.Day), Zurich);
        Assert.Equal((0d, 1d), day.Daylight);
        Assert.True(day.NowLit);

        var night = SunDayBar.Of(noon, new SunTimes(null, null, PolarState.Night), Zurich);
        Assert.Equal((0d, 0d), night.Daylight);
        Assert.False(night.NowLit);

        // A sunrise that is not on this day has no tick.
        Assert.Null(SunDayBar.Share(noon.AddDays(-2), noon.AddHours(-12), noon.AddHours(11)));
    }

    /// <summary>The view's area at a tier: the card less the header.</summary>
    private static SunPlan Plan(double width, double height, SunStyle style = SunStyle.Arc, bool events = true)
        => SunLayout.Plan(new WidgetArea(width, height - FamilySpace.ContentTop), style, events);

    [Fact]
    public void S3102_the_arc_is_never_dropped_and_takes_what_the_rows_leave_up_to_half_its_width()
    {
        foreach (var tier in WidgetTiers.All)
        {
            var plan = Plan(tier.Width, tier.Height);
            Assert.True(plan.GraphicHeight >= SunLayout.ArcFloor, tier.Name);
            Assert.True(plan.GraphicHeight <= Math.Max(SunLayout.ArcFloor, Math.Floor(plan.GraphicWidth / 2)), tier.Name);
        }

        // 320 x 240: two rows of two lines under an arc of what is left (180 - 2 x 48 - 8).
        var medium = Plan(320, 240);
        Assert.Equal(288, medium.GraphicWidth);
        Assert.Equal(76, medium.GraphicHeight);
        Assert.Equal((2, 2, 84.0), (medium.Rows, medium.Lines, medium.RowsTop));
    }

    [Fact]
    public void S3102_a_compact_width_is_the_graphic_alone_and_the_times_style_the_rows_alone()
    {
        Assert.Equal(0, Plan(120, 120).Rows);
        Assert.Equal(1, Plan(200, 150).Rows);

        var times = Plan(320, 240, SunStyle.Times);
        Assert.Equal(0, times.GraphicWidth);
        Assert.Equal(0, times.RowsTop);
        Assert.Equal(3, times.Rows);

        // [S-170] With the graphic gone the rows are on whatever the switch says, and on a compact tile too.
        Assert.True(Plan(120, 120, SunStyle.Times, events: false).Rows > 0);
        Assert.Equal(0, Plan(320, 240, SunStyle.Arc, events: false).Rows);
    }

    [Fact]
    public void S3102_an_expanded_width_puts_the_rows_beside_the_arc_but_the_bar_stays_over_them()
    {
        var wide = Plan(480, 160);
        Assert.True(wide.Side);
        Assert.Equal(Math.Floor(480 * SunLayout.ColumnShare), wide.RowsLeft);
        Assert.Equal(0, wide.RowsTop);
        Assert.Equal(3, wide.Rows);

        var bar = Plan(480, 160, SunStyle.DayBar);
        Assert.False(bar.Side);
        Assert.Equal(SunLayout.Dot, bar.GraphicHeight);
        Assert.Equal(SunLayout.Dot + FamilySpace.S2, bar.RowsTop);
        Assert.Equal(2, bar.Rows);
    }

    [Fact]
    public void S3102_the_rows_run_sunrise_sunset_daylight_noon_dawn_dusk()
        => Assert.Equal([SunRow.Sunrise, SunRow.Sunset, SunRow.Daylight, SunRow.Noon, SunRow.Dawn, SunRow.Dusk], SunLayout.Ladder);
}
