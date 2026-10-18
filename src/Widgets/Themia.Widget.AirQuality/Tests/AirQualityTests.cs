using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;
using static Themia.Core.Tests.OnlineReplies;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Widget.AirQuality.Tests;

/// <summary>
/// [S-5103]..[S-5109] [S-6170] The air quality widget: the index rules, the ladder of both styles and
/// the scale's repair. Moved with the widget from Core's OnlineWidgetsTests and WeatherStylesTests,
/// unchanged in what they assert.
/// </summary>
public sealed class AirQualityTests
{
    [Theory]
    [InlineData(47.37, 8.54, AirQualityScale.European)]   // Zürich
    [InlineData(64.15, -21.94, AirQualityScale.European)] // Reykjavík
    [InlineData(40.71, -74.0, AirQualityScale.Us)]        // New York
    [InlineData(35.68, 139.69, AirQualityScale.Us)]       // Tokyo
    [InlineData(-33.87, 151.21, AirQualityScale.Us)]      // Sydney
    public void S5103_auto_picks_the_european_index_in_europe(double lat, double lon, AirQualityScale expected)
        => Assert.Equal(expected, AirQualityRules.Resolve(AirQualityScale.Auto, lat, lon));

    [Fact]
    public void S5103_a_chosen_scale_wins_and_auto_without_a_place_is_us()
    {
        Assert.Equal(AirQualityScale.Us, AirQualityRules.Resolve(AirQualityScale.Us, 47.37, 8.54));
        Assert.Equal(AirQualityScale.European, AirQualityRules.Resolve(AirQualityScale.European, 40.7, -74));
        Assert.Equal(AirQualityScale.Us, AirQualityRules.Resolve(AirQualityScale.Auto, null, null));
    }

    [Theory]
    [InlineData(AirQualityScale.European, 0, AirBand.Best)]
    [InlineData(AirQualityScale.European, 20, AirBand.Best)]
    [InlineData(AirQualityScale.European, 20.5, AirBand.Second)]
    [InlineData(AirQualityScale.European, 55, AirBand.Third)]
    [InlineData(AirQualityScale.European, 100, AirBand.Fifth)]
    [InlineData(AirQualityScale.European, 140, AirBand.Worst)]
    [InlineData(AirQualityScale.Us, 50, AirBand.Best)]
    [InlineData(AirQualityScale.Us, 51, AirBand.Second)]
    [InlineData(AirQualityScale.Us, 120, AirBand.Third)]
    [InlineData(AirQualityScale.Us, 300, AirBand.Fifth)]
    [InlineData(AirQualityScale.Us, 420, AirBand.Worst)]
    public void S5104_bands_follow_each_agencys_breakpoints(AirQualityScale scale, double value, AirBand expected)
        => Assert.Equal(expected, AirQualityRules.Band(scale, value));

    [Fact]
    public void S5104_tones_are_two_good_two_caution_two_critical()
    {
        Assert.Equal(AirTone.Good, AirQualityRules.Tone(AirBand.Second));
        Assert.Equal(AirTone.Caution, AirQualityRules.Tone(AirBand.Third));
        Assert.Equal(AirTone.Caution, AirQualityRules.Tone(AirBand.Fourth));
        Assert.Equal(AirTone.Critical, AirQualityRules.Tone(AirBand.Fifth));
    }

    [Theory]
    [InlineData(0, UvLevel.Low)]
    [InlineData(2.9, UvLevel.Low)]
    [InlineData(3, UvLevel.Moderate)]
    [InlineData(6, UvLevel.High)]
    [InlineData(8, UvLevel.VeryHigh)]
    [InlineData(11, UvLevel.Extreme)]
    public void S5105_uv_follows_the_who_categories(double index, UvLevel expected)
        => Assert.Equal(expected, AirQualityRules.Uv(index));

    [Theory]
    [InlineData(PollenType.Birch, 0.5, PollenLevel.None)]
    [InlineData(PollenType.Birch, 50, PollenLevel.Moderate)]
    [InlineData(PollenType.Birch, 100, PollenLevel.High)]
    [InlineData(PollenType.Grass, 50, PollenLevel.High)]
    [InlineData(PollenType.Ragweed, 6, PollenLevel.Moderate)]
    [InlineData(PollenType.Ragweed, 20, PollenLevel.High)]
    public void S5106_pollen_levels_per_type(PollenType type, double grains, PollenLevel expected)
        => Assert.Equal(expected, AirQualityRules.Level(type, grains));

    [Fact]
    public void S5106_the_worst_pollen_comes_first()
    {
        var air = AirQualityWire.Parse(AirJson(GoodAir)).Value!;
        var pollen = AirQualityRules.Pollen(air);

        // Grass 12 and ragweed 6 are both Moderate; the larger count breaks the tie. Mugwort 0.2 is none.
        Assert.Equal([(PollenType.Grass, PollenLevel.Moderate), (PollenType.Ragweed, PollenLevel.Moderate)], pollen);
    }

    [Theory]
    [InlineData(AirQualityScale.European, 0, 0)]
    [InlineData(AirQualityScale.European, 10, 1.0 / 12)]
    [InlineData(AirQualityScale.European, 30, 1.5 / 6)]
    [InlineData(AirQualityScale.Us, 125, 2.5 / 6)]
    [InlineData(AirQualityScale.Us, 9999, 1)]
    public void S5109_the_strip_places_the_index_inside_its_band(AirQualityScale scale, double value, double expected)
        => Assert.Equal(expected, AirQualityRules.ScalePosition(scale, value), 6);

    [Fact]
    public void S5109_the_strip_is_the_rung_after_every_reading()
    {
        var floor = FluentType.Subtitle.LineHeight + FluentType.Body.LineHeight;

        Assert.False(AirLayout.Air(new WidgetArea(320, 240), floor, 7).Strip);
        Assert.True(AirLayout.Air(new WidgetArea(560, 420), floor, 7).Strip);
        Assert.True(AirLayout.Air(new WidgetArea(800, 560), floor, 7).Strip);
    }

    [Fact]
    public void S5103_repair_puts_an_unknown_scale_back_on_auto()
    {
        var config = new AirQualityConfig { Scale = (AirQualityScale)9 };

        Assert.Single(config.Repair());
        Assert.Equal(AirQualityScale.Auto, config.Scale);
    }

    [Fact]
    public void S5107_the_air_ladder_shows_more_as_it_grows()
    {
        var floor = FluentType.Subtitle.LineHeight + FluentType.Body.LineHeight;

        var small = AirLayout.Air(new WidgetArea(160, 100), floor, 5);
        var medium = AirLayout.Air(new WidgetArea(320, 260), floor, 5);
        var tall = AirLayout.Air(new WidgetArea(200, 460), floor, 5);
        var strip = AirLayout.Air(new WidgetArea(720, 140), floor, 5);

        Assert.Equal(AirFacts.None, small.Facts);
        Assert.Equal(AirFacts.Cells, medium.Facts);
        Assert.True(medium.Count >= 3);
        Assert.Equal(AirFacts.Lines, tall.Facts);
        Assert.Equal(5, tall.Count);
        Assert.Equal(AirFacts.Side, strip.Facts);
        Assert.True(strip.Count >= 4);
        Assert.True(medium.HeroRoom >= floor);
    }

    [Theory]
    [InlineData(AirQualityScale.European, 0, 0.0)]
    [InlineData(AirQualityScale.European, 20, 1 / 6.0)]
    [InlineData(AirQualityScale.European, 50, 2.5 / 6)]
    [InlineData(AirQualityScale.Us, 100, 2 / 6.0)]
    [InlineData(AirQualityScale.Us, 300, 5 / 6.0)]
    [InlineData(AirQualityScale.Us, 500, 1.0)]
    public void S6170_the_gauge_fills_to_the_index_s_place_on_six_equal_bands(AirQualityScale scale, double index, double fraction)
        => Assert.Equal(fraction, AirQualityRules.ScalePosition(scale, index), 6);

    [Fact]
    public void S6170_the_gauge_takes_the_tile_and_the_readings_come_beside_on_a_strip_and_under_on_a_column()
    {
        var small = AirLayout.AirGauge(Xs, 7);
        var medium = AirLayout.AirGauge(M, 7);
        var tall = AirLayout.AirGauge(Tall, 7);
        var strip = AirLayout.AirGauge(new WidgetArea(720, 140), 7);

        Assert.Equal(AirFacts.None, small.Facts);
        Assert.NotNull(GaugeLayout.Fit(small.GaugeWidth, small.GaugeHeight));
        Assert.True(medium.Band);
        Assert.Equal(AirFacts.None, medium.Facts);
        Assert.Equal(AirFacts.Lines, tall.Facts);
        Assert.True(tall.Count > 0);
        Assert.True(GaugeLayout.Fit(tall.GaugeWidth, tall.GaugeHeight)!.Value.Width >= Tall.Width - (2 * OnlineLayouts.Inset) - 4, "the gauge keeps the column's width");
        Assert.Equal(AirFacts.Side, strip.Facts);
        Assert.True(strip.SideWidth > 0 && strip.GaugeWidth + strip.SideWidth <= 720 - (2 * OnlineLayouts.Inset));
    }
}
