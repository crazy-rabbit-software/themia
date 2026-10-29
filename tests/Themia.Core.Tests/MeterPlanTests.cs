using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1762] [S-6300] The meters on the family's blocks: the reading a Readout at the top on the step its
/// widest case takes, the look's part 8 under it. Areas are the seven tiers less the header, as the card
/// hands them to a view.
/// </summary>
public sealed class MeterPlanTests
{
    private static readonly ReadoutText Percent = ReadoutRules.Split("100%");

    private static WidgetArea Area(string tier)
    {
        var t = WidgetTiers.All.Single(x => x.Name == tier);
        return new WidgetArea(t.Width, t.Height - FamilySpace.ContentTop);
    }

    [Theory]
    [InlineData("XS", 28)]
    [InlineData("S", 28)]
    [InlineData("M", 40)]
    [InlineData("Wide", 40)]
    [InlineData("Tall", 40)]
    [InlineData("L", 68)]
    [InlineData("XL", 68)]
    public void S1762_the_reading_climbs_28_40_68_and_stops(string tier, double size)
        => Assert.Equal(size, FamilyType.BaseOf(MeterPlan.Reading(Area(tier), Percent)));

    [Theory]
    [InlineData("XS")]
    [InlineData("M")]
    [InlineData("L")]
    [InlineData("XL")]
    public void S1824_the_history_runs_from_8_under_the_reading_to_the_cards_foot(string tier)
    {
        var area = Area(tier);
        var step = MeterPlan.Reading(area, Percent);

        // Edge to edge and to the foot: no inset under it.
        Assert.Equal(area.Height - step.LineHeight - FamilySpace.S2, MeterPlan.Band(area, step, graphOn: true));
        Assert.Equal(0, MeterPlan.Band(area, step, graphOn: false));
    }

    [Fact]
    public void S1762_a_larger_tile_is_a_longer_chart_not_a_bigger_number()
    {
        var l = Area("L");
        var xl = Area("XL");
        Assert.Equal(MeterPlan.Reading(l, Percent), MeterPlan.Reading(xl, Percent));
        Assert.True(MeterPlan.Band(xl, MeterPlan.Reading(xl, Percent), true) > MeterPlan.Band(l, MeterPlan.Reading(l, Percent), true));
    }

    [Fact]
    public void S1762_a_band_shorter_than_its_floor_is_not_drawn()
    {
        var area = new WidgetArea(200, 60);
        var step = MeterPlan.Reading(area, Percent);
        Assert.Equal(0, MeterPlan.Band(area, step, true));
    }

    [Theory]
    [InlineData("XS", 88)]
    [InlineData("M", 288)]
    [InlineData("Wide", 198)]
    [InlineData("L", 528)]
    public void S6302_the_gauge_is_as_wide_as_the_content_while_its_half_fits_the_height(string tier, double diameter)
    {
        var area = Area(tier);
        Assert.Equal(diameter, MeterPlan.Gauge(area));
        Assert.True(ChartRules.GaugeHeight(diameter) <= area.Height - FamilySpace.Inset);
    }

    [Fact]
    public void S6302_a_gauge_under_its_floor_leaves_the_reading_alone()
        => Assert.Equal(0, MeterPlan.Gauge(new WidgetArea(60, 40)));

    [Fact]
    public void S6302_the_reading_in_the_bowl_is_on_the_readout_steps_and_capped_by_the_area()
    {
        var l = Area("L");
        Assert.Equal(68, FamilyType.BaseOf(MeterPlan.GaugeReading(l, MeterPlan.Gauge(l), Percent)));
        var xs = Area("XS");
        Assert.Equal(28, FamilyType.BaseOf(MeterPlan.GaugeReading(xs, MeterPlan.Gauge(xs), Percent)));
    }

    [Fact]
    public void S6303_columns_need_a_track_and_a_gap_each_and_a_parts_height()
    {
        var m = Area("M");
        var step = MeterPlan.Reading(m, Percent);
        Assert.True(MeterPlan.Columns(m, step, 16));
        Assert.False(MeterPlan.Columns(m, step, 100));
        Assert.False(MeterPlan.Columns(m, step, 0));
        Assert.False(MeterPlan.Columns(new WidgetArea(200, 50), FamilyType.Big28, 4));
    }

    [Theory]
    [InlineData("XS", 0)]
    [InlineData("S", 1)]
    [InlineData("M", 3)]
    [InlineData("L", 4)]
    public void S6304_the_items_rows_are_whole_32_rows_under_the_reading(string tier, int rows)
    {
        var area = Area(tier);
        Assert.Equal(rows, MeterPlan.Rows(area, MeterPlan.Reading(area, Percent), 4));
    }

    [Fact]
    public void S6305_the_rings_take_the_grid_with_the_largest_ring_and_hold_every_item()
    {
        var area = Area("L");
        var step = MeterPlan.Reading(area, Percent);
        var grid = MeterPlan.Rings(area, step, 16);
        Assert.True(grid.Shown);
        Assert.True(grid.Columns * grid.Rows >= 16);
        Assert.True((grid.Columns * grid.CellWidth) + ((grid.Columns - 1) * FamilySpace.S2) <= area.Width - (2 * FamilySpace.Inset));
        Assert.True((grid.Rows * grid.CellHeight) + ((grid.Rows - 1) * FamilySpace.S2) <= MeterPlan.PartHeight(area, step) + 0.01);

        // A ring smaller than an icon is no grid.
        Assert.False(MeterPlan.Rings(Area("S"), MeterPlan.Reading(Area("S"), Percent), 24).Shown);
    }

    [Fact]
    public void S6317_a_core_graph_is_an_icons_height_of_graph_and_its_figure_where_there_is_room()
    {
        var area = Area("L");
        var step = MeterPlan.Reading(area, Percent);
        var grid = MeterPlan.Graphs(area, step, 16);
        Assert.True(grid.Shown);
        Assert.True(MeterPlan.GraphFigure(grid.CellHeight));
        Assert.True(grid.CellWidth >= MeterPlan.GraphCellWidthFloor);
        Assert.True(grid.Columns * grid.Rows >= 16);
        Assert.False(MeterPlan.Graphs(Area("XS"), MeterPlan.Reading(Area("XS"), Percent), 16).Shown);

        // At M the cells are graphs alone: too short for the figure over them.
        var m = MeterPlan.Graphs(Area("M"), MeterPlan.Reading(Area("M"), Percent), 16);
        Assert.True(m.Shown);
        Assert.True(m.CellHeight >= MeterPlan.GraphCellFloor);
        Assert.False(MeterPlan.GraphFigure(m.CellHeight));
    }

    [Theory]
    [InlineData("M", false)]
    [InlineData("Tall", false)]
    [InlineData("Wide", true)]
    [InlineData("XL", true)]
    public void S6264_two_readings_side_by_side_on_an_expanded_width_else_one_over_the_other(string tier, bool side)
    {
        var (beside, step) = MeterPlan.Numbers(Area(tier), ReadoutRules.Split("1023 KB/s") with { Lead = true });
        Assert.Equal(side, beside);
        Assert.True(FamilyType.BaseOf(step) <= ReadoutRules.Ceiling(Area(tier)));
    }

    [Fact]
    public void S6262_the_battery_is_twice_as_wide_as_tall_its_fill_4_inside_its_outline()
    {
        var area = Area("M");
        var picture = MeterPlan.Battery(area, MeterPlan.Reading(area, Percent))!.Value;
        Assert.Equal(2 * picture.Height, picture.BodyWidth);
        Assert.Equal(picture.BodyWidth - (2 * (FamilyShape.Stroke + FamilySpace.S1)), picture.InnerWidth);
        Assert.Equal(picture.InnerWidth * 0.64, picture.FillWidth(0.64), 6);
        Assert.Equal(0, picture.FillWidth(double.NaN));
        Assert.Contains(picture.Bolt, FamilyShape.Icons);
        Assert.Null(MeterPlan.Battery(new WidgetArea(120, 50), FamilyType.Big28));
    }

    [Fact]
    public void S6263_the_signal_bars_rise_left_to_right_and_stand_on_one_line()
    {
        var area = Area("L");
        var bars = MeterPlan.Signal(area, MeterPlan.Reading(area, Percent));
        Assert.Equal(PingSignal.BarCount, bars.Count);
        Assert.All(bars, b => Assert.Equal(bars[0].Width, b.Width));
        for (var i = 1; i < bars.Count; i++)
        {
            Assert.True(bars[i].Height > bars[i - 1].Height);
        }

        Assert.Equal(Math.Floor(MeterPlan.PartHeight(area, MeterPlan.Reading(area, Percent))), bars[^1].Height);
    }

    [Fact]
    public void S6266_the_ring_is_the_contents_short_side_and_its_reading_fits_its_hole()
    {
        var area = Area("Tall");
        var diameter = MeterPlan.Ring(area);
        Assert.Equal(area.Width - (2 * FamilySpace.Inset), diameter);
        var step = MeterPlan.RingReading(area, diameter, Percent);
        Assert.True(ReadoutRules.Width(step, Percent) <= diameter);
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
