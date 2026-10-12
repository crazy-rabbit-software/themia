using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The audio visualizer, split out of Music ([S-3200], [S-3205]), in five designs ([S-3210]).</summary>
public class AudioVisualizerTests
{
    private static readonly WidgetKind[] Kinds =
        [WidgetKind.AudioVisualizer, WidgetKind.VisualizerMirror, WidgetKind.VisualizerWave, WidgetKind.VisualizerDots, WidgetKind.VisualizerRadial, WidgetKind.VisualizerScope];

    private static readonly VisualizerStyle[] Linear =
        [VisualizerStyle.Bars, VisualizerStyle.Mirror, VisualizerStyle.Wave, VisualizerStyle.Dots];

    private static WidgetArea Tier(string name)
    {
        var tier = WidgetTiers.All.Single(t => t.Name == name);
        return new WidgetArea(tier.Width, tier.Height);
    }

    public static TheoryData<string> Tiers() => [.. WidgetTiers.All.Select(t => t.Name)];

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
    public void S3210_every_design_round_trips_as_its_own_kind_and_stores_nothing()
    {
        foreach (var kind in Kinds)
        {
            var config = WidgetConfig.CreateDefault(kind);
            Assert.Equal(kind, config.Kind);

            var json = JsonSerializer.Serialize(config, AppStateJsonContext.Default.WidgetConfig);
            var back = JsonSerializer.Deserialize(json, AppStateJsonContext.Default.WidgetConfig);

            Assert.Equal(kind, back!.Kind);
            Assert.Equal(kind, back.Clone().Kind);
            Assert.Empty(back.Repair());

            // The discriminator and nothing else: the design is the kind, not a field.
            using var document = JsonDocument.Parse(json);
            Assert.Single(document.RootElement.EnumerateObject());
        }
    }

    [Fact]
    public void S3210_each_kind_draws_a_different_design_and_no_other_kind_is_a_visualizer()
    {
        var styles = Kinds.Select(kind => VisualizerLayout.StyleOf(kind)).ToList();
        Assert.Equal(Enum.GetValues<VisualizerStyle>().Cast<VisualizerStyle?>(), styles);
        Assert.Equal(VisualizerStyle.Bars, ((VisualizerConfig)WidgetConfig.CreateDefault(WidgetKind.AudioVisualizer)).Style);

        Assert.All(Enum.GetValues<WidgetKind>().Except(Kinds), kind => Assert.Null(VisualizerLayout.StyleOf(kind)));
    }

    [Fact]
    public void S3200_a_saved_visualizer_reads_back_and_a_wrong_config_is_replaced_by_its_own()
    {
        Assert.IsType<AudioVisualizerConfig>(ConfigJson.Read("AudioVisualizer", """{"kind":"audioVisualizer"}"""));
        Assert.IsType<AudioVisualizerConfig>(ConfigJson.Read("AudioVisualizer", """{"kind":"music"}"""));
        Assert.IsType<VisualizerWaveConfig>(ConfigJson.Read("VisualizerWave", """{"kind":"audioVisualizer"}"""));
    }

    [Fact]
    public void S3210_every_design_starts_without_a_header()
        => Assert.All(Kinds, kind => Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1)).Header));

    [Fact]
    public void S3200_music_no_longer_has_a_spectrum_switch()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new MusicConfig(), AppStateJsonContext.Default.WidgetConfig);
        Assert.DoesNotContain("spectrum", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("XS", 12)]
    [InlineData("S", 24)]
    [InlineData("M", 24)]
    [InlineData("Wide", 24)]
    [InlineData("Tall", 24)]
    [InlineData("L", 24)]
    [InlineData("XL", 24)]
    public void S3205_the_bar_count_follows_the_run_along_the_long_side(string tier, int bars)
        => Assert.Equal(bars, VisualizerLayout.Plan(Tier(tier)).Bars);

    [Theory]
    [InlineData("XS", 8)]
    [InlineData("S", 12)]
    [InlineData("M", 24)]
    public void S3210_led_columns_are_fewer_than_bars_so_a_cell_is_never_a_speck(string tier, int columns)
    {
        var plan = VisualizerLayout.Plan(Tier(tier), VisualizerStyle.Dots);
        Assert.Equal(columns, plan.Bars);
        Assert.True(plan.Thickness >= VisualizerLayout.MinLed);
    }

    [Fact]
    public void S3205_every_count_is_a_whole_group_of_the_analysers_bands()
        => Assert.All(VisualizerLayout.Counts, count => Assert.Equal(0, Spectrum.Bands % count));

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3205_the_linear_designs_fill_the_tile_edge_to_edge_and_are_never_hairlines(string tier)
    {
        var area = Tier(tier);

        foreach (var style in Linear)
        {
            var plan = VisualizerLayout.Plan(area, style);
            var along = plan.Horizontal ? area.Height : area.Width;
            var across = plan.Horizontal ? area.Width : area.Height;

            Assert.True(plan.Thickness >= VisualizerLayout.MinThickness, $"{tier} {style}: {plan.Thickness}");
            Assert.Equal(along, (plan.Bars * plan.Thickness) + ((plan.Bars - 1) * plan.Gap), 6);
            Assert.Equal(across, plan.Length, 6);
        }
    }

    [Fact]
    public void S3205_a_tall_column_lays_the_bars_across_and_a_strip_or_tile_stands_them_up()
    {
        Assert.True(VisualizerLayout.Plan(Tier("Tall")).Horizontal);
        Assert.False(VisualizerLayout.Plan(Tier("Wide")).Horizontal);
        Assert.False(VisualizerLayout.Plan(Tier("M")).Horizontal);
        Assert.False(VisualizerLayout.Plan(Tier("XL")).Horizontal);
        Assert.False(VisualizerLayout.Plan(Tier("Tall"), VisualizerStyle.Radial).Horizontal);
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

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3206_the_idle_baseline_is_the_same_few_dips_on_every_tile(string tier)
    {
        foreach (var style in new[] { VisualizerStyle.Bars, VisualizerStyle.Mirror, VisualizerStyle.Wave, VisualizerStyle.Radial })
        {
            var plan = VisualizerLayout.Plan(Tier(tier), style);
            Assert.Equal(VisualizerLayout.Baseline, plan.Floor * plan.Length, 6);
        }

        // The LED columns idle on their first cell.
        var dots = VisualizerLayout.Plan(Tier(tier), VisualizerStyle.Dots);
        Assert.Equal(1, VisualizerLayout.LitCells(0, dots.Cells));
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3210_led_cells_fill_each_column_exactly_and_stay_about_square(string tier)
    {
        var plan = VisualizerLayout.Plan(Tier(tier), VisualizerStyle.Dots);

        Assert.InRange(plan.Cells, 1, VisualizerLayout.MaxCells);
        Assert.Equal(plan.Length, (plan.Cells * plan.Cell) + ((plan.Cells - 1) * plan.Gap), 6);
        Assert.True(plan.Cell >= plan.Thickness - 1e-6 || plan.Cells == VisualizerLayout.MaxCells, $"{tier}: {plan.Cell} vs {plan.Thickness}");
        Assert.True(plan.Cell < (2 * plan.Thickness) + plan.Gap || plan.Cells == VisualizerLayout.MaxCells, $"{tier}: {plan.Cell} vs {plan.Thickness}");
    }

    [Theory]
    [InlineData(0f, 10, 1)]
    [InlineData(0.05f, 10, 1)]
    [InlineData(0.1f, 10, 1)]
    [InlineData(0.11f, 10, 2)]
    [InlineData(0.5f, 10, 5)]
    [InlineData(1f, 10, 10)]
    [InlineData(3f, 10, 10)]
    [InlineData(float.NaN, 10, 1)]
    [InlineData(0.5f, 0, 0)]
    public void S3210_a_level_lights_its_share_of_the_cells_never_none_never_more(float level, int cells, int lit)
        => Assert.Equal(lit, VisualizerLayout.LitCells(level, cells));

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3210_the_ring_is_centred_and_a_full_spoke_reaches_the_nearer_edge(string tier)
    {
        var area = Tier(tier);
        var plan = VisualizerLayout.Plan(area, VisualizerStyle.Radial);

        Assert.Equal(area.Width / 2, plan.CenterX, 6);
        Assert.Equal(area.Height / 2, plan.CenterY, 6);
        Assert.Equal(Math.Min(area.Width, area.Height) / 2, plan.Radius + plan.Length, 6);
        Assert.True(plan.Thickness >= VisualizerLayout.MinSpoke, $"{tier}: {plan.Thickness}");
        Assert.Contains(plan.Bars, VisualizerLayout.Counts);
    }

    [Fact]
    public void S3210_spokes_run_from_the_top_round_to_the_bottom_on_each_side_mirrored()
    {
        Assert.Equal(7.5, VisualizerLayout.SpokeAngle(0, 12, left: false), 6);
        Assert.Equal(172.5, VisualizerLayout.SpokeAngle(11, 12, left: false), 6);
        Assert.Equal(-7.5, VisualizerLayout.SpokeAngle(0, 12, left: true), 6);

        for (var i = 0; i < 24; i++)
        {
            Assert.InRange(VisualizerLayout.SpokeAngle(i, 24, false), 0, 180);
        }
    }

    [Fact]
    public void S3210_the_wave_passes_through_every_band_and_its_controls_stay_in_the_box()
    {
        double[] heights = [0, 100, 0, 100, 0, 50];
        var controls = new (double X, double Y)[3 * (heights.Length - 1)];

        VisualizerLayout.Wave(heights, 500, 100, controls);

        for (var i = 0; i < heights.Length - 1; i++)
        {
            Assert.Equal((i + 1) * 100, controls[(3 * i) + 2].X, 6);
            Assert.Equal(heights[i + 1], controls[(3 * i) + 2].Y, 6);
            Assert.InRange(controls[3 * i].Y, 0, 100);
            Assert.InRange(controls[(3 * i) + 1].Y, 0, 100);
            Assert.True(controls[3 * i].X < controls[(3 * i) + 1].X);
        }
    }

    [Fact]
    public void S3210_a_flat_wave_is_a_straight_line_and_a_short_one_writes_nothing()
    {
        double[] flat = [3, 3, 3, 3];
        var controls = new (double X, double Y)[9];
        VisualizerLayout.Wave(flat, 300, 100, controls);
        Assert.All(controls, c => Assert.Equal(3, c.Y, 6));

        var untouched = new (double X, double Y)[3];
        VisualizerLayout.Wave([5], 100, 100, untouched);
        Assert.All(untouched, c => Assert.Equal((0d, 0d), c));
    }

    [Fact]
    public void S3205_an_unmeasured_or_degenerate_tile_plans_safely()
    {
        foreach (var style in Enum.GetValues<VisualizerStyle>())
        {
            var unmeasured = VisualizerLayout.Plan(default, style);
            Assert.Equal(VisualizerLayout.Counts[^1], unmeasured.Bars);
            Assert.Equal(1, unmeasured.Floor);

            // [S-3213] The waveform's count is points along its run, never fewer than its minimum.
            var tiny = VisualizerLayout.Plan(new WidgetArea(20, 20), style);
            Assert.Equal(style == VisualizerStyle.Scope ? VisualizerLayout.MinScopePoints : VisualizerLayout.Counts[^1], tiny.Bars);
            Assert.True(tiny.Thickness >= 0);
            Assert.InRange(tiny.Floor, 0, 1);
            Assert.True(tiny.Cell >= 0);
        }
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
