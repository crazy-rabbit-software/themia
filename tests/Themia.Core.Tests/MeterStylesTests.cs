using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The CPU, memory and GPU meters' styles ([S-6300]..[S-6322]) and the shared gauge, bar and tile rules.</summary>
public sealed class MeterStylesTests
{
    // ---- the styles themselves -----------------------------------------------------------------

    [Fact]
    public void S6315_memory_offers_graph_bar_and_gauge()
        => Assert.Equal(["graph", "bar", "gauge"], WidgetStyles.For(MemoryConfig.Descriptor.Id).Select(s => s.Name));

    [Fact]
    public void S6320_the_gpu_offers_graph_engines_and_gauge_and_keeps_its_adapter_through_a_clone()
    {
        Assert.Equal(["graph", "engines", "gauge"], WidgetStyles.For(GpuConfig.Descriptor.Id).Select(s => s.Name));

        var gpu = new GpuConfig { Adapter = 1, Style = GpuStyle.Engines };
        var copy = (GpuConfig)gpu.Clone();
        Assert.Equal(GpuStyle.Engines, copy.Style);
        Assert.Equal(1, copy.Adapter);
    }

    // ---- [S-6310] per-core reads ----------------------------------------------------------------

    [Fact]
    public void S6310_the_cores_come_in_processor_order_without_the_totals()
    {
        var loads = CpuCoreRules.Loads(
        [
            ("0,10", 50), ("_Total", 40), ("0,2", 20), ("1,0", 100), ("0,_Total", 41), ("0,0", 0), ("junk", 9),
        ]);

        Assert.Equal([0.0, 0.2, 0.5, 1.0], loads);
    }

    [Fact]
    public void S6310_a_reading_that_is_not_a_number_is_unknown_and_an_overshoot_is_full()
    {
        var loads = CpuCoreRules.Loads([("0,0", double.NaN), ("0,1", 104), ("0,2", -3), ("0,1", 7)]);
        Assert.Equal([null, 1.0, 0.0], loads);
    }

    [Theory]
    [InlineData("0,3", 0, 3)]
    [InlineData("12,63", 12, 63)]
    public void S6310_a_processor_instance_is_group_and_number(string instance, int group, int number)
        => Assert.Equal((group, number), CpuCoreRules.Parse(instance));

    [Theory]
    [InlineData("_Total")]
    [InlineData("0,_Total")]
    [InlineData(",3")]
    [InlineData("0,")]
    [InlineData("a,b")]
    [InlineData("")]
    public void S6310_anything_else_is_not_a_processor(string instance)
        => Assert.Null(CpuCoreRules.Parse(instance));

    // ---- [S-6301] shades -------------------------------------------------------------------------

    [Fact]
    public void S6301_a_load_shade_rises_with_the_load_in_the_usage_colours()
    {
        Assert.Equal(new LoadShade(MeterPressure.Normal, LoadTint.Faintest, true), LoadTint.Of(0));
        Assert.Equal(1, LoadTint.Of(1).Strength, 6);
        Assert.True(LoadTint.Of(0.5).Strength > LoadTint.Of(0.2).Strength);
        Assert.Equal(MeterPressure.Elevated, LoadTint.Of(0.7).Pressure);
        Assert.Equal(MeterPressure.Critical, LoadTint.Of(0.895).Pressure);
        Assert.Equal(1, LoadTint.Of(3).Strength, 6);
    }

    [Fact]
    public void S6301_an_unknown_load_is_drawn_unknown_never_as_idle()
    {
        Assert.False(LoadTint.Of(null).Known);
        Assert.False(LoadTint.Of(double.NaN).Known);
        Assert.True(LoadTint.Of(0).Known);
    }

    // ---- [S-6302] the gauge ----------------------------------------------------------------------

    [Theory]
    [InlineData(288, 180)]
    [InlineData(88, 60)]
    [InlineData(528, 360)]
    [InlineData(448, 100)]
    [InlineData(168, 340)]
    public void S6302_the_gauge_fits_its_box_and_sits_on_its_bottom_edge(double width, double height)
    {
        var gauge = Assert.NotNull(GaugeLayout.Fit(width, height));

        Assert.True(gauge.Width <= width + 1e-9, "wider than the box");
        Assert.True(gauge.Height <= height + 1e-9, "taller than the box");
        Assert.Equal((2 * gauge.Radius) + gauge.Thickness, gauge.Width, 6);
        Assert.Equal(gauge.Radius + gauge.Thickness, gauge.Height, 6);
        Assert.InRange(gauge.Thickness, GaugeLayout.MinThickness, GaugeLayout.MaxThickness);

        // One side of the box is used up: the gauge is as large as the box allows.
        Assert.True(width - gauge.Width < 2 + gauge.Thickness || height - gauge.Height < 2 + gauge.Thickness);
    }

    [Fact]
    public void S6302_a_box_too_small_for_a_gauge_has_none()
    {
        Assert.Null(GaugeLayout.Fit(40, 30));
        Assert.Null(GaugeLayout.Fit(0, 100));
        Assert.Null(GaugeLayout.Fit(double.NaN, 100));
    }

    [Fact]
    public void S6302_the_arc_runs_from_the_left_end_over_the_top_to_the_right_end()
    {
        var gauge = new GaugeGeometry(212, 112, 100, 12);

        var (lx, ly) = gauge.PointAt(0);
        var (tx, ty) = gauge.PointAt(0.5);
        var (rx, ry) = gauge.PointAt(1);

        Assert.Equal(6, lx, 6);
        Assert.Equal(gauge.CenterY, ly, 6);
        Assert.Equal(gauge.CenterX, tx, 6);
        Assert.Equal(6, ty, 6);
        Assert.Equal(206, rx, 6);
        Assert.Equal(gauge.CenterY, ry, 6);

        // Out of range is the end it is nearest, never past it.
        Assert.Equal(gauge.PointAt(1), gauge.PointAt(7));
        Assert.Equal(gauge.PointAt(0), gauge.PointAt(double.NaN));
    }

    [Fact]
    public void S6302_the_reading_in_the_bowl_grows_with_the_gauge_and_fits_its_chord()
    {
        var bound = HeroBound.Of(Formatting.Percent(1));
        var small = GaugeLayout.Reading(GaugeLayout.Fit(120, 70)!.Value, bound, FluentType.Body, FluentType.Display);
        var large = GaugeLayout.Reading(GaugeLayout.Fit(520, 360)!.Value, bound, FluentType.Body, FluentType.Display);

        Assert.True(large.FontSize > small.FontSize);

        var gauge = GaugeLayout.Fit(520, 360)!.Value;
        var chord = 2 * Math.Sqrt((gauge.InnerRadius * gauge.InnerRadius) - (large.FontSize * large.FontSize));
        Assert.True(HeroLayout.ValueWidth(large, bound.Value, bound.Unit) <= chord);
    }

    // ---- [S-6303] [S-6304] [S-6305] bars and tiles -----------------------------------------------

    [Theory]
    [InlineData(288, 8)]
    [InlineData(288, 16)]
    [InlineData(88, 16)]
    [InlineData(88, 64)]
    [InlineData(528, 128)]
    public void S6303_columns_fill_the_width_and_never_overflow_it(double width, int count)
    {
        var plan = BarLayout.Columns(width, count);
        var used = (plan.Bar * count) + (plan.Gap * (count - 1));

        Assert.True(used <= width + 1e-6, $"{used} > {width}");
        Assert.True(width - used < count * 0.01 + 1e-6 || plan.Gap == 0, "the width is not used");
        Assert.True(plan.Bar >= 1);
    }

    [Fact]
    public void S6303_the_gap_shrinks_before_the_bars_get_thin()
    {
        Assert.Equal(4, BarLayout.Columns(288, 8).Gap);
        Assert.Equal(2, BarLayout.Columns(120, 16).Gap);
        Assert.Equal(1, BarLayout.Columns(88, 16).Gap);
        Assert.True(BarLayout.Columns(88, 32).Gap <= 1);
        Assert.Equal(0, BarLayout.Columns(88, 0).Bar);
    }

    [Fact]
    public void S6304_rows_grow_their_type_and_bar_with_the_room_and_all_fit()
    {
        var tight = BarLayout.Rows(60, 2, 12);
        var roomy = BarLayout.Rows(400, 4, 12);

        Assert.Equal(2, tight.Shown);
        Assert.Equal(4, roomy.Shown);
        Assert.True(roomy.Label.FontSize > tight.Label.FontSize);
        Assert.True(roomy.Thickness > tight.Thickness);
        Assert.True(BarLayout.Total(roomy, 4) <= 400);
        Assert.True(BarLayout.Total(tight, 2) <= 60);
    }

    [Fact]
    public void S6304_rows_that_do_not_all_fit_are_shed_from_the_end_at_the_smallest_size()
    {
        var plan = BarLayout.Rows(60, 4, 12);

        Assert.Equal(FluentType.Caption, plan.Label);
        Assert.Equal(BarLayout.Thicknesses[0], plan.Thickness);
        Assert.Equal(2, plan.Shown);
        Assert.Equal(0, BarLayout.Rows(10, 4, 12).Shown);
    }

    [Fact]
    public void S6304_a_bar_is_never_more_than_half_its_label_line()
    {
        var plan = BarLayout.Rows(1000, 1, 12);
        Assert.True(plan.Thickness <= plan.Label.LineHeight / 2);
        Assert.Equal(FluentType.BodyLarge, plan.Label);
    }

    [Fact]
    public void S6304_a_narrow_row_writes_smaller_rather_than_cutting_its_label()
    {
        Assert.Equal(FluentType.Caption, BarLayout.Rows(400, 1, 12, width: 168, lineCharacters: 26).Label);
        Assert.Equal(FluentType.BodyLarge, BarLayout.Rows(400, 1, 12, width: 528, lineCharacters: 26).Label);
    }

    [Theory]
    [InlineData(288, 100, 8)]
    [InlineData(288, 100, 16)]
    [InlineData(88, 30, 16)]
    [InlineData(528, 300, 12)]
    [InlineData(168, 300, 24)]
    [InlineData(88, 40, 128)]
    public void S6305_tiles_hold_every_item_inside_the_box(double width, double height, int count)
    {
        var plan = BarLayout.Tiles(width, height, count);

        Assert.True(plan.Columns * plan.Rows >= count);
        Assert.True((plan.Rows - 1) * plan.Columns < count, "a whole empty row");
        Assert.True((plan.Columns * plan.TileWidth) + ((plan.Columns - 1) * plan.Gap) <= width + 1e-6);
        Assert.True((plan.Rows * plan.TileHeight) + ((plan.Rows - 1) * plan.Gap) <= height + 1e-6);
        Assert.True(plan.TileWidth > 0 && plan.TileHeight > 0);
    }

    [Fact]
    public void S6305_tiles_take_the_grid_with_the_largest_squarest_tile()
    {
        // Eight cores in a strip: one row of eight; in a square box: a grid, not a strip.
        Assert.Equal((8, 1), (BarLayout.Tiles(400, 40, 8).Columns, BarLayout.Tiles(400, 40, 8).Rows));
        var square = BarLayout.Tiles(200, 200, 16);
        Assert.Equal((4, 4), (square.Columns, square.Rows));
        Assert.Equal(4, square.Gap);
    }

    // ---- [S-6322] GPU engines ------------------------------------------------------------------

    private const string Luid = "luid_0x00000000_0x0000d1f2";

    [Theory]
    [InlineData("pid_1_luid_0x00000000_0x0000d1f2_phys_0_eng_0_engtype_3D", GpuEngineKind.ThreeD)]
    [InlineData("pid_1_luid_0x00000000_0x0000d1f2_phys_0_eng_2_engtype_Copy", GpuEngineKind.Copy)]
    [InlineData("pid_1_luid_0x00000000_0x0000d1f2_phys_0_eng_5_engtype_VideoEncode", GpuEngineKind.VideoEncode)]
    [InlineData("pid_1_luid_0x00000000_0x0000d1f2_phys_0_eng_4_engtype_VideoDecode", GpuEngineKind.VideoDecode)]
    [InlineData("pid_1_luid_0x00000000_0x0000d1f2_phys_0_eng_4_engtype_videodecode", GpuEngineKind.VideoDecode)]
    public void S6322_an_engine_instance_names_its_type(string instance, GpuEngineKind kind)
        => Assert.Equal(kind, GpuRules.EngineKindOf(instance));

    [Theory]
    [InlineData("pid_1_luid_0x00000000_0x0000d1f2_phys_0_eng_7_engtype_Compute_0")]
    [InlineData("pid_1_luid_0x00000000_0x0000d1f2_phys_0_eng_8_engtype_VideoProcessing")]
    [InlineData("pid_1_luid_0x00000000_0x0000d1f2_phys_0_eng_8")]
    [InlineData("")]
    public void S6322_other_engine_types_are_not_drawn(string instance)
        => Assert.Null(GpuRules.EngineKindOf(instance));

    [Fact]
    public void S6322_each_type_is_its_busiest_engine_summed_across_processes_on_its_own_adapter()
    {
        var loads = GpuRules.EngineLoads(
        [
            ($"pid_1_{Luid}_phys_0_eng_0_engtype_3D", 30),
            ($"pid_2_{Luid}_phys_0_eng_0_engtype_3D", 25),
            ($"pid_3_{Luid}_phys_0_eng_1_engtype_3D", 40),
            ($"pid_1_{Luid}_phys_0_eng_4_engtype_VideoDecode", 12.5),
            ($"pid_1_{Luid}_phys_0_eng_2_engtype_Copy", 0),
            ($"pid_1_{Luid}_phys_0_eng_7_engtype_Compute_0", 99),
            ("pid_1_luid_0x00000000_0x0000aaaa_phys_0_eng_5_engtype_VideoEncode", 70),
            ($"pid_9_{Luid}_phys_0_eng_0_engtype_3D", double.NaN),
        ], Luid);

        Assert.Equal(
            [new GpuEngineLoad(GpuEngineKind.ThreeD, 55), new GpuEngineLoad(GpuEngineKind.Copy, 0), new GpuEngineLoad(GpuEngineKind.VideoDecode, 12.5)],
            loads);
    }

    [Fact]
    public void S6322_an_engine_never_reads_over_full()
    {
        var loads = GpuRules.EngineLoads([($"pid_1_{Luid}_phys_0_eng_0_engtype_3D", 80), ($"pid_2_{Luid}_phys_0_eng_0_engtype_3D", 80)], Luid);
        Assert.Equal(100, Assert.Single(loads).Percent);
    }
}
