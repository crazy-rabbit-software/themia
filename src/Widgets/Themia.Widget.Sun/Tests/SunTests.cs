using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Sun.Tests;

/// <summary>
/// [S-6045] [S-6046] The Sun widget: its styles' repair and the day bar. Moved with the widget from
/// Core's SkyWidgetsTests and TimeStylesTests, unchanged in what they assert.
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
}
