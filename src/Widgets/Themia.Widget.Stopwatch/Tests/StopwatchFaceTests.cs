using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Stopwatch.Tests;

/// <summary>[S-4603] [S-4605] The stopwatch on the timers' face: the widest reading of its hour, the laps' column from an expanded width.</summary>
public sealed class StopwatchFaceTests
{
    [Fact]
    public void S4603_the_time_is_sized_for_the_widest_reading_of_its_hour()
    {
        Assert.Equal("88:88.8", StopwatchFace.Widest(hours: false));
        Assert.Equal("88:88:88.8", StopwatchFace.Widest(hours: true));
    }

    [Fact]
    public void S4605_below_an_expanded_width_the_lap_line_alone_says_where_the_laps_are()
    {
        foreach (var area in new[] { new WidgetArea(120, 76), new WidgetArea(200, 106), new WidgetArea(320, 196), new WidgetArea(200, 356) })
        {
            var plan = StopwatchFace.Plan(area, hours: false);
            Assert.Equal(0, plan.LapColumn);
            Assert.Equal(0, plan.LapRows);
            Assert.Equal(area.Width - 32, plan.FaceWidth);
        }
    }

    [Fact]
    public void S4605_from_an_expanded_width_the_laps_take_two_fifths_in_whole_rows()
    {
        // Wide: 480 × 116 — one line a lap, three whole rows in the 100 left over the bottom inset.
        var wide = StopwatchFace.Plan(new WidgetArea(480, 116), hours: false);
        Assert.Equal(192, wide.LapColumn);
        Assert.Equal(480 - 192 - 16, wide.FaceWidth);
        Assert.Equal(1, wide.LapLines);
        Assert.Equal(3, wide.LapRows);
        Assert.True(wide.Face.Controls);

        // XL: two lines a lap (its total under it), ten rows of 48 in 500.
        var xl = StopwatchFace.Plan(new WidgetArea(800, 516), hours: true);
        Assert.Equal(320, xl.LapColumn);
        Assert.Equal(2, xl.LapLines);
        Assert.Equal(10, xl.LapRows);
        Assert.Equal(FamilyType.Hero96, xl.Face.Step);
    }
}
