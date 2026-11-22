using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6300] [S-11003]..[S-11005] Where a meter atom's parts stand in the box it is given: the items'
/// grids, the battery's picture, the signal bars, the pressure's role, the reading's split.
/// </summary>
public sealed class MeterPlanTests
{
    [Theory]
    [InlineData(160, 64)]
    [InlineData(165, 64)]
    [InlineData(640, 200)]
    public void S15746_a_cores_column_keeps_one_width_and_the_last_is_cut(double width, double height)
    {
        // Owner, 2026-11-20: "the widget items should actually have a fixed size ... the last item will be cutoff".
        var grid = ItemGrid.Default();
        var reach = MeterPlan.CoreCells(width, height, new SizeDip(MeterPlan.CoreColumn, MeterPlan.CoreColumn), grid, column: true);
        Assert.Equal(MeterPlan.CoreColumn, reach.Across.Pitch);
        Assert.Equal((int)Math.Ceiling(width / MeterPlan.CoreColumn), reach.Columns);
        Assert.Equal(1, reach.Lines);
        Assert.Equal(height, reach.Down.Height(0), 6);
    }

    [Fact]
    public void S15746_rings_and_graphs_keep_their_cell_from_the_cards_top_left_and_are_cut_by_its_edges()
    {
        var grid = ItemGrid.Default();
        foreach (var (w, h) in new[] { (144.0, 144.0), (150.0, 170.0), (360.0, 360.0) })
        {
            var rings = MeterPlan.CoreCells(w, h, new SizeDip(MeterPlan.CoreRing, MeterPlan.CoreRing), grid);
            Assert.Equal((30.0, 30.0), (rings.Across.Pitch, rings.Down.Pitch));
            Assert.Equal(((int)Math.Ceiling(w / 30), (int)Math.Ceiling(h / 30)), (rings.Columns, rings.Lines));
        }

        foreach (var (w, h) in new[] { (280.0, 200.0), (330.0, 130.0), (280.0, 210.0) })
        {
            var graphs = MeterPlan.CoreCells(w, h, MeterPlan.CoreGraph, grid);
            Assert.Equal((60.0, 40.0), (graphs.Across.Pitch, graphs.Down.Pitch));
            Assert.Equal(((int)Math.Ceiling(w / 60), (int)Math.Ceiling(h / 40)), (graphs.Columns, graphs.Lines));
        }
    }

    [Fact]
    public void S6262_the_battery_is_twice_as_wide_as_tall_its_fill_4_inside_its_outline()
    {
        var picture = MeterPlan.Picture(40);
        Assert.Equal(2 * picture.Height, picture.BodyWidth);
        Assert.Equal(picture.BodyWidth - (2 * (FamilyShape.Stroke + FamilySpace.S1)), picture.InnerWidth);
        Assert.Equal(picture.InnerWidth * 0.64, picture.FillWidth(0.64), 6);
        Assert.Equal(0, picture.FillWidth(double.NaN));
        Assert.Contains(picture.Bolt, FamilyShape.Icons);
    }

    [Fact]
    public void S6263_the_signal_bars_rise_left_to_right_and_stand_on_one_line()
    {
        var bars = MeterPlan.SignalBars(200, 32);
        Assert.Equal(PingSignal.BarCount, bars.Count);
        Assert.All(bars, b => Assert.Equal(bars[0].Width, b.Width));
        for (var i = 1; i < bars.Count; i++)
        {
            Assert.True(bars[i].Height > bars[i - 1].Height);
        }

        Assert.Equal(32, bars[^1].Height);
        Assert.Empty(MeterPlan.SignalBars(8, 32));
    }

    [Theory]
    [InlineData(MeterPressure.Normal, ColourRole.Accent)]
    [InlineData(MeterPressure.Healthy, ColourRole.Accent)]
    [InlineData(MeterPressure.Elevated, ColourRole.Caution)]
    [InlineData(MeterPressure.Critical, ColourRole.Critical)]
    public void S1123_a_pressure_is_the_accent_until_elevated_and_never_green(MeterPressure pressure, ColourRole role)
    {
        Assert.Equal(role, MeterPlan.Role(pressure, absent: false));
        Assert.Equal(ColourRole.TextDisabled, MeterPlan.Role(pressure, absent: true));
    }

    [Theory]
    [InlineData("62%", "62", "%")]
    [InlineData("42 %", "42", " %")]
    [InlineData("4,48 GHz", "4,48", " GHz")]
    [InlineData("٤٢٪", "٤٢", "٪")]
    [InlineData("2.4 MB/s", "2.4", " MB/s")]
    [InlineData("--", "--", "")]
    [InlineData("1 234 ms", "1 234 ms", "")]
    [InlineData("%42", "%42", "")]
    public void S3860_a_reading_splits_into_its_figure_and_its_unit_with_the_space(string text, string value, string unit)
    {
        var split = ReadoutRules.Split(text);
        Assert.Equal(value, split.Value);
        Assert.Equal(unit, split.Unit);
    }
}
