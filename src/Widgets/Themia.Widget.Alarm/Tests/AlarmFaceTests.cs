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
    public void S11301_S2449_retired_the_natural_card_is_the_one_readout_face_and_it_scales_down_to_its_floor()
    {
        var locale = Themia.Core.Localization.Locale.En;
        var sizing = AlarmFace.Sizing(new Themia.Core.Model.AlarmConfig(), locale);

        Assert.True(sizing.Natural.Height == sizing.Max.Height && !sizing.FixedWidth);

        // The inset, the 68 line and its Body line under, 8, the Controls row, the inset, the slack.
        Assert.Equal(12 + 92 + 20 + 8 + 28 + 12 + 2, sizing.Natural.Height);

        // [S-12833] Down as one until its Body text (the line under, the buttons) is at 9; the insets do not scale.
        Assert.Equal(Math.Ceiling((sizing.Natural.Height - 26) * 9 / 14) + 26, sizing.Min.Height);
        Assert.Equal(Math.Ceiling((sizing.Natural.Width - 26) * 9 / 14) + 26, sizing.Min.Width);
        Assert.Null(typeof(Themia.Core.Model.AlarmConfig).GetProperty("Size"));
        Assert.Equal(FamilyType.Hero68, AlarmFace.Sized(new Themia.Core.Model.AlarmConfig(), locale).Step);
    }

    [Fact]
    public void S11301_the_meridiem_is_the_languages_widest_and_none_on_a_24_hour_clock()
    {
        Assert.Equal(" AM", AlarmFace.Unit(Themia.Core.Localization.Locale.En));
        Assert.Equal(string.Empty, AlarmFace.Unit(Themia.Core.Localization.Locale.De));
    }

    [Fact]
    public void S4629_the_face_is_sized_on_every_figure_an_8_so_an_earlier_time_does_not_shrink_it()
        => Assert.Equal(new ReadoutText("88:88", " AM", LineUnder: true, OneReadout: true), AlarmFace.Worst(" AM"));
}
