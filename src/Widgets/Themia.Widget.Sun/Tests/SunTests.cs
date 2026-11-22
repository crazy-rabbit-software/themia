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
    public void S11111_S15808_the_arc_is_up_to_480_wide_its_natural_height_half_its_content_s_width_down_to_64_by_40()
    {
        var sizing = SunLayout.Sizing(new SunConfig());

        Assert.Equal(new Themia.Sdk.SizeDip(280, 24 + 128), sizing.Natural);
        // [S-15808] WR-14: a drawing with no text goes down to where its 12 marker still reads, over the grid's 32.
        Assert.Equal(new Themia.Sdk.SizeDip(64, 40), sizing.Min);
        Assert.Equal((480.0, 24 + 228.0), (sizing.Max.Width, sizing.Max.Height));
        Assert.Empty(sizing.Problems());
    }

    [Fact]
    public void S11111_S15808_the_day_bar_is_one_dot_tall_in_the_inset_at_one_height_down_to_64_wide()
    {
        var sizing = SunLayout.Sizing(new SunConfig { Style = SunStyle.DayBar });

        Assert.True(sizing.FixedHeight);
        Assert.Equal(24 + SunLayout.Dot, sizing.Natural.Height);
        Assert.Equal((64.0, 280.0, 480.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Theory]
    [InlineData(256, 12, 1)]          // a new card: its natural size
    [InlineData(176, 12, 1)]          // narrower: never under it (its dot the glyph floor)
    [InlineData(536, 48, 536 / 256d)] // 560x72: the width's share of its natural width stops it
    [InlineData(276, 276, 276 / 256d)] // 300x300: the width stops it, centred in the height
    [InlineData(900, 24, 2)]          // a long low card: its height stops it
    public void S15807_the_day_bar_grows_as_one_with_its_card(double width, double height, double scale)
    {
        // WR-7: at 200x36, 560x72 and 300x300 the thumb and the track had stayed 12 DIP tall.
        Assert.Equal(scale, SunLayout.BarScale(width, height), 6);
        Assert.True(SunLayout.Dot * SunLayout.BarScale(width, height) <= Math.Max(height, SunLayout.Dot) + 1e-9);
    }

    [Fact]
    public void S15807_the_day_bar_s_dot_stands_at_its_share_of_the_track_never_cut_at_its_ends()
    {
        Assert.Equal(128, SunLayout.DotAt(0.5, 256), 9);
        Assert.Equal(SunLayout.Dot / 2, SunLayout.DotAt(0, 256), 9);
        Assert.Equal(256 - (SunLayout.Dot / 2), SunLayout.DotAt(1, 256), 9);
        Assert.Equal(64, SunLayout.DotAt(0.25, 256), 9);
    }
    [Fact]
    public void S11110_the_sun_s_styles_are_the_arc_and_the_day_bar()
    {
        Assert.Equal([SunStyle.Arc, SunStyle.DayBar], Enum.GetValues<SunStyle>());
    }
}
