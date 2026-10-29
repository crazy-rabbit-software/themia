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
