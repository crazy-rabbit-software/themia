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
    public void S4605_a_face_too_narrow_for_the_tenth_on_the_floor_step_never_shows_it()
    {
        // 112 wide: 88 of face; "88:88.8" on 28 is 95, "88:88" 72 (XS's 96 just holds the tenth).
        Assert.True(StopwatchFace.Plan(new WidgetArea(120, 84), hours: false).Tenths);
        var xs = StopwatchFace.Plan(new WidgetArea(112, 84), hours: false);
        Assert.False(xs.Tenths);
        Assert.Equal(FamilyType.Big28, xs.Face.Step);
        Assert.Equal("88:88", StopwatchFace.Widest(hours: false, tenths: false));
        Assert.True(ReadoutRules.Width(xs.Face.Step, new ReadoutText(StopwatchFace.Widest(false, xs.Tenths))) <= xs.FaceWidth);

        // S and larger keep it.
        Assert.True(StopwatchFace.Plan(new WidgetArea(200, 114), hours: false).Tenths);
        Assert.True(StopwatchFace.Plan(new WidgetArea(800, 524), hours: true).Tenths);
    }

    [Fact]
    public void S4605_below_an_expanded_width_the_lap_line_alone_says_where_the_laps_are()
    {
        foreach (var area in new[] { new WidgetArea(120, 84), new WidgetArea(200, 114), new WidgetArea(320, 204), new WidgetArea(200, 364) })
        {
            var plan = StopwatchFace.Plan(area, hours: false);
            Assert.Equal(0, plan.LapColumn);
            Assert.Equal(0, plan.LapRows);
            Assert.Equal(area.Width - (2 * FamilySpace.Inset), plan.FaceWidth);
        }
    }

    [Fact]
    public void S4605_from_an_expanded_width_the_laps_take_two_fifths_in_whole_rows()
    {
        // Wide: 480 × 124 — one line a lap, four whole rows in the 112 left over the bottom inset.
        var wide = StopwatchFace.Plan(new WidgetArea(480, 124), hours: false);
        Assert.Equal(192, wide.LapColumn);
        Assert.Equal(480 - 192 - FamilySpace.Inset, wide.FaceWidth);
        Assert.Equal(1, wide.LapLines);
        Assert.Equal(4, wide.LapRows);
        Assert.True(wide.Face.Controls);

        // XL: two lines a lap (its total under it), twelve rows of 40 in 512.
        var xl = StopwatchFace.Plan(new WidgetArea(800, 524), hours: true);
        Assert.Equal(320, xl.LapColumn);
        Assert.Equal(2, xl.LapLines);
        Assert.Equal(12, xl.LapRows);
        Assert.Equal(FamilyType.Hero96, xl.Face.Step);
    }
}
