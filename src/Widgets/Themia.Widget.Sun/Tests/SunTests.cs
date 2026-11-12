using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Sun.Tests;

/// <summary>
/// [S-11110] [S-11111] [S-6045] [S-6046] The sun graphic: its sizes, its styles' repair and the day bar.
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
        var times = new Themia.Core.Astronomy.SunTimes(new DateTimeOffset(2026, 6, 21, 6, 0, 0, TimeSpan.FromHours(2)), new DateTimeOffset(2026, 6, 21, 21, 0, 0, TimeSpan.FromHours(2)), PolarState.None);

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
        var none = new Themia.Core.Astronomy.SunTimes(null, null, PolarState.None);
        Assert.Equal(11 / 23d, SunDayBar.Of(noon, none, Zurich).Now, 6);

        var day = SunDayBar.Of(noon, new Themia.Core.Astronomy.SunTimes(null, null, PolarState.Day), Zurich);
        Assert.Equal((0d, 1d), day.Daylight);
        Assert.True(day.NowLit);

        var night = SunDayBar.Of(noon, new Themia.Core.Astronomy.SunTimes(null, null, PolarState.Night), Zurich);
        Assert.Equal((0d, 0d), night.Daylight);
        Assert.False(night.NowLit);

        // A sunrise that is not on this day has no tick.
        Assert.Null(SunDayBar.Share(noon.AddDays(-2), noon.AddHours(-12), noon.AddHours(11)));
    }

    [Fact]
    public void S11111_the_arc_is_200_to_480_wide_in_the_inset_its_natural_height_half_its_content_s_width()
    {
        var sizing = SunLayout.Sizing(new SunConfig());

        Assert.Equal(new Themia.Sdk.SizeDip(280, 24 + 128), sizing.Natural);
        Assert.Equal((200.0, 480.0), (sizing.Min.Width, sizing.Max.Width));
        Assert.Equal((24 + 88.0, 24 + 228.0), (sizing.Min.Height, sizing.Max.Height));
        Assert.Empty(sizing.Problems());
    }

    [Fact]
    public void S11111_the_day_bar_is_one_dot_tall_in_the_inset_at_one_height()
    {
        var sizing = SunLayout.Sizing(new SunConfig { Style = SunStyle.DayBar });

        Assert.True(sizing.FixedHeight);
        Assert.Equal(24 + SunLayout.Dot, sizing.Natural.Height);
        Assert.Equal((200.0, 280.0, 480.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S11110_the_sun_starts_headerless_and_its_styles_are_the_arc_and_the_day_bar()
    {
        Assert.True(new SunModule().Descriptor.Traits.HasFlag(Themia.Sdk.WidgetTraits.StartsHeaderless));
        Assert.Equal([SunStyle.Arc, SunStyle.DayBar], Enum.GetValues<SunStyle>());
    }
}
