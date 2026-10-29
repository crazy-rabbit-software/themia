using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Alarm.Tests;

/// <summary>[S-4628] [S-4629] The alarm on the timers' face: its time split into the figures and the meridiem, the step and the controls by size.</summary>
public sealed class AlarmFaceTests
{
    [Theory]
    [InlineData("6:45 AM", "6:45", " AM")]
    [InlineData("06:45", "06:45", "")]
    [InlineData("6:45 PM", "6:45", " PM")]
    [InlineData("午前 6:45", "午前 6:45", "")]
    [InlineData("", "", "")]
    public void S4628_the_meridiem_is_the_readouts_unit_and_a_24_hour_time_is_all_value(string time, string value, string unit)
        => Assert.Equal((value, unit), AlarmFace.Split(time));

    [Fact]
    public void S4629_a_tile_too_short_for_the_switch_keeps_the_time_and_a_larger_one_climbs_the_hero_steps()
    {
        // XS: 120 × 120 less the header; the time alone, on the floor step.
        var xs = AlarmFace.Plan(new WidgetArea(120, 76), " AM");
        Assert.False(xs.Controls);
        Assert.Equal(FamilyType.Big28, xs.Step);

        // S: the switch's row fits under the 28 line, its words do not.
        var s = AlarmFace.Plan(new WidgetArea(200, 106), " AM");
        Assert.True(s.Controls);
        Assert.False(s.Under);

        // L and XL: a Hero step with its Title line under, the row 8 under its box.
        var xl = AlarmFace.Plan(new WidgetArea(800, 516), " AM");
        Assert.True(xl.Controls);
        Assert.True(xl.Under);
        Assert.Equal(FamilyType.Hero192, xl.Step);
        Assert.Equal(516 - 16 - 40, xl.Box);
    }

    [Fact]
    public void S4629_the_face_is_sized_on_every_figure_an_8_so_an_earlier_time_does_not_shrink_it()
        => Assert.Equal(new ReadoutText("88:88", " AM", LineUnder: true, OneReadout: true), AlarmFace.Worst(" AM"));
}
