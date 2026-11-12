using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6300] [S-11003]..[S-11005] Where a meter atom's parts stand in the box it is given: the items'
/// grids, the battery's picture, the signal bars, the pressure's role, the reading's split.
/// </summary>
public sealed class MeterPlanTests
{
    [Fact]
    public void S6305_the_rings_take_the_grid_with_the_largest_ring_and_hold_every_item()
    {
        var grid = MeterPlan.Rings(336, 300, 16);
        Assert.True(grid.Shown);
        Assert.True(grid.Columns * grid.Rows >= 16);
        Assert.True((grid.Columns * grid.CellWidth) + ((grid.Columns - 1) * FamilySpace.S2) <= 336);
        Assert.True((grid.Rows * grid.CellHeight) + ((grid.Rows - 1) * FamilySpace.S2) <= 300);

        // A ring smaller than an icon is no grid.
        Assert.False(MeterPlan.Rings(72, 40, 24).Shown);
    }

    [Fact]
    public void S6317_a_core_graph_is_at_least_an_icons_height_and_32_wide()
    {
        var grid = MeterPlan.Graphs(256, 176, 16);
        Assert.True(grid.Shown);
        Assert.True(grid.CellWidth >= MeterPlan.GraphCellWidthFloor);
        Assert.True(grid.CellHeight >= MeterPlan.GraphCellFloor);
        Assert.True(grid.Columns * grid.Rows >= 16);
        Assert.False(MeterPlan.Graphs(96, 40, 16).Shown);
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
