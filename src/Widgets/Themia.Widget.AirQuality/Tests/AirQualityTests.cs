using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;
using static Themia.Core.Tests.OnlineReplies;

namespace Themia.Widget.AirQuality.Tests;

/// <summary>
/// [S-5103]..[S-5107] [S-6170] The air quality widget: the index rules and their roles, both styles'
/// sizes and the scale's repair.
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

    [Fact]
    public void S5103_repair_puts_an_unknown_scale_back_on_auto()
    {
        var config = new AirQualityConfig { Scale = (AirQualityScale)9 };

        Assert.Single(config.Repair());
        Assert.Equal(AirQualityScale.Auto, config.Scale);
    }

    [Fact]
    public void S5104_the_index_is_primary_while_good_then_caution_then_critical_and_never_green()
    {
        Assert.Equal(ColourRole.TextPrimary, AirQualityRules.ValueRole(AirTone.Good));
        Assert.Equal(ColourRole.Caution, AirQualityRules.ValueRole(AirTone.Caution));
        Assert.Equal(ColourRole.Critical, AirQualityRules.ValueRole(AirTone.Critical));
        Assert.Equal(ColourRole.Accent, AirQualityRules.ArcRole(AirTone.Good));
        Assert.Equal(ColourRole.Caution, AirQualityRules.ArcRole(AirTone.Caution));
        Assert.Equal(ColourRole.Critical, AirQualityRules.ArcRole(AirTone.Critical));
    }

    [Fact]
    public void S11131_the_number_is_the_28_readout_and_its_band_at_one_height_160_to_320_wide()
    {
        var sizing = AirLayout.Sizing(new AirQualityConfig());

        Assert.Equal(36 + 36 + 16 + 12, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight);
        Assert.Equal((160.0, 200.0, 320.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
        Assert.True(ReadoutRules.Width(FamilyType.Big28, AirLayout.Worst) <= 160 - 24);
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
    public void S11131_the_gauge_is_one_size_its_160_ring_and_its_band_in_the_inset()
    {
        var sizing = AirLayout.Sizing(new AirQualityConfig { Style = AirQualityStyle.Gauge });

        Assert.True(sizing.FixedHeight && sizing.FixedWidth);
        Assert.Equal(24 + 160, sizing.Natural.Width);
        Assert.Equal(36 + Math.Ceiling(ChartRules.GaugeHeight(160) + 16) + 12, sizing.Natural.Height);
        Assert.True(ReadoutRules.Width(FamilyType.Big28, AirLayout.Worst with { LineUnder = false }) <= 160 - 48, "the widest index fits the bowl");
    }
}
