using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.AudioVisualizer.Tests;

/// <summary>The audio visualizer, split out of Music ([S-3200], [S-3205]), in six styles ([S-3216]).</summary>
public class AudioVisualizerTests
{

    private static readonly AudioVisualizerStyle[] Linear =
        [AudioVisualizerStyle.Bars, AudioVisualizerStyle.Mirrored, AudioVisualizerStyle.Wave, AudioVisualizerStyle.Led];

    private static WidgetArea Tier(string name)
    {
        var tier = WidgetTiers.All.Single(t => t.Name == name);
        return new WidgetArea(tier.Width, tier.Height);
    }

    public static TheoryData<string> Tiers() => [.. WidgetTiers.All.Select(t => t.Name)];

    [Fact]
    public void S3200_the_config_round_trips_under_its_own_kind()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new AudioVisualizerConfig(), StateJson.WidgetConfig);
        Assert.Contains("\"audioVisualizer\"", json, StringComparison.Ordinal);

        var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
        Assert.IsType<AudioVisualizerConfig>(back);
        Assert.Equal(new WidgetId("audioVisualizer"), back!.Kind);
        Assert.Empty(back.Repair());
        Assert.IsType<AudioVisualizerConfig>(back.Clone());
    }

    [Theory]
    [InlineData(AudioVisualizerStyle.Bars, "bars")]
    [InlineData(AudioVisualizerStyle.Mirrored, "mirrored")]
    [InlineData(AudioVisualizerStyle.Wave, "wave")]
    [InlineData(AudioVisualizerStyle.Led, "led")]
    [InlineData(AudioVisualizerStyle.Radial, "radial")]
    [InlineData(AudioVisualizerStyle.Waveform, "waveform")]
    public void S3216_every_style_round_trips_under_the_one_kind_and_stores_only_its_name(AudioVisualizerStyle style, string wire)
    {
        var config = new AudioVisualizerConfig { Style = style };

        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);
        var back = Assert.IsType<AudioVisualizerConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));

        Assert.Equal(style, back.Style);
        Assert.Equal(style, Assert.IsType<AudioVisualizerConfig>(back.Clone()).Style);
        Assert.Empty(back.Repair());

        // The discriminator and the style, nothing else.
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["kind", "style"], document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(wire, document.RootElement.GetProperty("style").GetString());
    }

    [Fact]
    public void S3216_the_six_styles_in_menu_order_bars_first()
    {
        Assert.Equal(["bars", "mirrored", "wave", "led", "radial", "waveform"], WidgetStyles.For(new WidgetId("audioVisualizer")).Select(s => s.Name));
        Assert.Equal(AudioVisualizerStyle.Bars, ((AudioVisualizerConfig)WidgetConfig.CreateDefault(new WidgetId("audioVisualizer"))).Style);
    }

    [Fact]
    public void S3200_a_saved_visualizer_reads_back_and_an_unknown_style_is_the_bars()
    {
        Assert.IsType<AudioVisualizerConfig>(ConfigJson.Read("AudioVisualizer", """{"kind":"audioVisualizer"}"""));

        var config = Assert.IsType<AudioVisualizerConfig>(ConfigJson.Read("AudioVisualizer", """{"kind":"audioVisualizer","style":"lava"}"""));
        Assert.Equal(AudioVisualizerStyle.Bars, config.Style);
    }

    [Fact]
    public void S3216_the_visualizer_starts_without_a_header()
        => Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.Create(new WidgetId("audioVisualizer"), new FracRect(0, 0, 0.1, 0.1)).Header);

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
    public void S3216_led_columns_are_fewer_than_bars_so_a_cell_is_never_a_speck(string tier, int columns)
    {
        var plan = VisualizerLayout.Plan(Tier(tier), AudioVisualizerStyle.Led);
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
        Assert.False(VisualizerLayout.Plan(Tier("Tall"), AudioVisualizerStyle.Radial).Horizontal);
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
        foreach (var style in new[] { AudioVisualizerStyle.Bars, AudioVisualizerStyle.Mirrored, AudioVisualizerStyle.Wave, AudioVisualizerStyle.Radial })
        {
            var plan = VisualizerLayout.Plan(Tier(tier), style);
            Assert.Equal(VisualizerLayout.Baseline, plan.Floor * plan.Length, 6);
        }

        // The LED columns idle on their first cell.
        var dots = VisualizerLayout.Plan(Tier(tier), AudioVisualizerStyle.Led);
        Assert.Equal(1, VisualizerLayout.LitCells(0, dots.Cells));
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3216_led_cells_fill_each_column_exactly_and_stay_about_square(string tier)
    {
        var plan = VisualizerLayout.Plan(Tier(tier), AudioVisualizerStyle.Led);

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
    public void S3216_a_level_lights_its_share_of_the_cells_never_none_never_more(float level, int cells, int lit)
        => Assert.Equal(lit, VisualizerLayout.LitCells(level, cells));

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3216_the_ring_is_centred_and_a_full_spoke_reaches_the_nearer_edge(string tier)
    {
        var area = Tier(tier);
        var plan = VisualizerLayout.Plan(area, AudioVisualizerStyle.Radial);

        Assert.Equal(area.Width / 2, plan.CenterX, 6);
        Assert.Equal(area.Height / 2, plan.CenterY, 6);
        Assert.Equal(Math.Min(area.Width, area.Height) / 2, plan.Radius + plan.Length, 6);
        Assert.True(plan.Thickness >= VisualizerLayout.MinSpoke, $"{tier}: {plan.Thickness}");
        Assert.Contains(plan.Bars, VisualizerLayout.Counts);
    }

    [Fact]
    public void S3216_spokes_run_from_the_top_round_to_the_bottom_on_each_side_mirrored()
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
    public void S3216_the_wave_passes_through_every_band_and_its_controls_stay_in_the_box()
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
    public void S3216_a_flat_wave_is_a_straight_line_and_a_short_one_writes_nothing()
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
        foreach (var style in Enum.GetValues<AudioVisualizerStyle>())
        {
            var unmeasured = VisualizerLayout.Plan(default, style);
            Assert.Equal(VisualizerLayout.Counts[^1], unmeasured.Bars);
            Assert.Equal(1, unmeasured.Floor);

            // [S-3213] The waveform's count is points along its run, never fewer than its minimum.
            var tiny = VisualizerLayout.Plan(new WidgetArea(20, 20), style);
            Assert.Equal(style == AudioVisualizerStyle.Waveform ? VisualizerLayout.MinScopePoints : VisualizerLayout.Counts[^1], tiny.Bars);
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

    [Fact]
    public void S3212_the_waveform_is_a_visualizer_style_that_never_has_a_header()
    {
        Assert.Contains(WidgetStyles.For(new WidgetId("audioVisualizer")), s => s.Value == (int)AudioVisualizerStyle.Waveform && s.Name == "waveform");
        Assert.False(WidgetInstance.CanHaveHeader(new WidgetId("audioVisualizer")));
        Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.DefaultHeader(new WidgetId("audioVisualizer")));
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3213_the_line_runs_the_long_side_with_a_point_every_few_dips(string tier)
    {
        var area = Tier(tier);
        var plan = VisualizerLayout.Plan(area, AudioVisualizerStyle.Waveform);
        var along = plan.Horizontal ? area.Height : area.Width;
        var across = plan.Horizontal ? area.Width : area.Height;

        Assert.Equal(area.Shape == AreaShape.Tall, plan.Horizontal);
        Assert.Equal(along, plan.Run);
        Assert.Equal(across, plan.Length);
        Assert.InRange(plan.Bars, VisualizerLayout.MinScopePoints, Oscilloscope.Points);
        Assert.True(plan.Bars == Oscilloscope.Points || plan.Run / (plan.Bars - 1) >= VisualizerLayout.ScopeStep - 0.01, tier);
        Assert.InRange(plan.Thickness, 2, 4);
    }

    [Fact]
    public void S3213_a_larger_tile_draws_more_points_and_a_thicker_line_never_past_the_trace()
    {
        var xs = VisualizerLayout.Plan(Tier("XS"), AudioVisualizerStyle.Waveform);
        var xl = VisualizerLayout.Plan(Tier("XL"), AudioVisualizerStyle.Waveform);
        Assert.True(xl.Bars > xs.Bars);
        Assert.True(xl.Thickness > xs.Thickness);
        Assert.Equal(Oscilloscope.Points, xl.Bars);
    }
}
