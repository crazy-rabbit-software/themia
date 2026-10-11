using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The audio visualizer, split out of Music ([S-3200], [S-3205]).</summary>
public class AudioVisualizerTests
{
    private static WidgetArea Tier(string name)
    {
        var tier = WidgetTiers.All.Single(t => t.Name == name);
        return new WidgetArea(tier.Width, tier.Height);
    }

    [Fact]
    public void S3200_the_config_round_trips_under_its_own_kind()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new AudioVisualizerConfig(), AppStateJsonContext.Default.WidgetConfig);
        Assert.Contains("\"audioVisualizer\"", json, StringComparison.Ordinal);

        var back = JsonSerializer.Deserialize(json, AppStateJsonContext.Default.WidgetConfig);
        Assert.IsType<AudioVisualizerConfig>(back);
        Assert.Equal(WidgetKind.AudioVisualizer, back!.Kind);
        Assert.Empty(back.Repair());
        Assert.IsType<AudioVisualizerConfig>(back.Clone());
    }

    [Fact]
    public void S3200_a_saved_visualizer_reads_back_and_a_wrong_config_is_replaced_by_its_own()
    {
        Assert.IsType<AudioVisualizerConfig>(ConfigJson.Read("AudioVisualizer", """{"kind":"audioVisualizer"}"""));
        Assert.IsType<AudioVisualizerConfig>(ConfigJson.Read("AudioVisualizer", """{"kind":"music"}"""));
        Assert.IsType<AudioVisualizerConfig>(WidgetConfig.CreateDefault(WidgetKind.AudioVisualizer));
    }

    [Fact]
    public void S3200_the_visualizer_starts_without_a_header()
        => Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.Create(WidgetKind.AudioVisualizer, new FracRect(0, 0, 0.1, 0.1)).Header);

    [Fact]
    public void S3200_music_no_longer_has_a_spectrum_switch()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new MusicConfig(), AppStateJsonContext.Default.WidgetConfig);
        Assert.DoesNotContain("spectrum", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("XS", 12)]
    [InlineData("S", 12)]
    [InlineData("M", 24)]
    [InlineData("Wide", 24)]
    [InlineData("Tall", 24)]
    [InlineData("L", 24)]
    [InlineData("XL", 24)]
    public void S3205_the_bar_count_follows_the_run_along_the_long_side(string tier, int bars)
        => Assert.Equal(bars, VisualizerLayout.Plan(Tier(tier)).Bars);

    [Fact]
    public void S3205_every_count_is_a_whole_group_of_the_analysers_bands()
        => Assert.All(VisualizerLayout.Counts, count => Assert.Equal(0, Spectrum.Bands % count));

    [Theory]
    [InlineData("XS")]
    [InlineData("S")]
    [InlineData("M")]
    [InlineData("Wide")]
    [InlineData("Tall")]
    [InlineData("L")]
    [InlineData("XL")]
    public void S3205_the_bars_fill_the_run_exactly_and_are_never_hairlines(string tier)
    {
        var area = Tier(tier);
        var plan = VisualizerLayout.Plan(area);
        var box = area.Deflate(plan.Inset, plan.Inset);
        var along = plan.Horizontal ? box.Height : box.Width;
        var across = plan.Horizontal ? box.Width : box.Height;

        Assert.True(plan.Thickness >= VisualizerLayout.MinThickness, $"{tier}: {plan.Thickness}");
        Assert.Equal(along, (plan.Bars * plan.Thickness) + ((plan.Bars - 1) * plan.Gap), 6);
        Assert.Equal(across, plan.Length, 6);
    }

    [Fact]
    public void S3205_a_tall_column_lays_the_bars_across_and_a_strip_or_tile_stands_them_up()
    {
        Assert.True(VisualizerLayout.Plan(Tier("Tall")).Horizontal);
        Assert.False(VisualizerLayout.Plan(Tier("Wide")).Horizontal);
        Assert.False(VisualizerLayout.Plan(Tier("M")).Horizontal);
        Assert.False(VisualizerLayout.Plan(Tier("XL")).Horizontal);
    }

    [Fact]
    public void S3205_a_larger_tile_draws_the_same_bands_thicker_with_wider_gaps()
    {
        var m = VisualizerLayout.Plan(Tier("M"));
        var xl = VisualizerLayout.Plan(Tier("XL"));

        Assert.Equal(m.Bars, xl.Bars);
        Assert.True(xl.Thickness > m.Thickness);
        Assert.True(xl.Gap > m.Gap);
        Assert.InRange(xl.Gap, 2, 6);
    }

    [Fact]
    public void S3205_xs_keeps_a_smaller_inset_than_the_card()
    {
        Assert.Equal(VisualizerLayout.CompactInset, VisualizerLayout.Plan(Tier("XS")).Inset);
        Assert.Equal(VisualizerLayout.Inset, VisualizerLayout.Plan(Tier("M")).Inset);
    }

    [Fact]
    public void S3206_the_idle_baseline_is_the_same_few_dips_on_every_tile()
    {
        foreach (var tier in WidgetTiers.All)
        {
            var plan = VisualizerLayout.Plan(new WidgetArea(tier.Width, tier.Height));
            Assert.Equal(VisualizerLayout.Baseline, plan.Floor * plan.Length, 6);
        }
    }

    [Fact]
    public void S3205_an_unmeasured_or_degenerate_tile_plans_safely()
    {
        var unmeasured = VisualizerLayout.Plan(default);
        Assert.Equal(VisualizerLayout.Counts[^1], unmeasured.Bars);
        Assert.Equal(1, unmeasured.Floor);

        var tiny = VisualizerLayout.Plan(new WidgetArea(20, 20));
        Assert.Equal(VisualizerLayout.Counts[^1], tiny.Bars);
        Assert.True(tiny.Thickness >= 0);
        Assert.InRange(tiny.Floor, 0, 1);
    }

    [Fact]
    public void S3205_a_bar_is_the_loudest_band_of_its_group()
    {
        var bands = new float[Spectrum.Bands];
        bands[0] = 0.2f;
        bands[1] = 0.9f;
        bands[5] = 0.4f;
        bands[23] = 0.7f;
        var bars = new float[6];

        VisualizerLayout.Group(bands, bars);

        Assert.Equal([0.9f, 0.4f, 0, 0, 0, 0.7f], bars);
    }

    [Fact]
    public void S3205_grouping_clamps_bad_levels_and_reads_missing_bands_as_silence()
    {
        float[] bands = [float.NaN, 3f, -1f, float.PositiveInfinity];
        var bars = new float[4];
        VisualizerLayout.Group(bands, bars);
        Assert.Equal([0f, 1f, 0f, 0f], bars);

        var none = new float[] { 0.5f, 0.5f };
        VisualizerLayout.Group([], none);
        Assert.Equal([0f, 0f], none);
    }

    [Fact]
    public void S3205_all_24_bars_are_the_bands_themselves()
    {
        var bands = Enumerable.Range(0, Spectrum.Bands).Select(i => i / 24f).ToArray();
        var bars = new float[Spectrum.Bands];
        VisualizerLayout.Group(bands, bars);
        Assert.Equal(bands, bars);
    }
}
