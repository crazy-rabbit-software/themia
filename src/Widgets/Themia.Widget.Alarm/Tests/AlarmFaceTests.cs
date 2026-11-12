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
    public void S11301_the_card_follows_the_size_setting_one_height_a_small_width_range()
    {
        var locale = Themia.Core.Localization.Locale.En;
        Themia.Sdk.WidgetSizing At(ReadoutSize size) => AlarmFace.Sizing(new Themia.Core.Model.AlarmConfig { Size = size }, locale);

        var (small, medium, large) = (At(ReadoutSize.Small), At(ReadoutSize.Medium), At(ReadoutSize.Large));
        Assert.True(small.Natural.Height < medium.Natural.Height && medium.Natural.Height < large.Natural.Height);
        Assert.True(small.Natural.Width < medium.Natural.Width && medium.Natural.Width < large.Natural.Width);
        Assert.True(medium.FixedHeight && !medium.FixedWidth);
        Assert.Equal(medium.Min, medium.Natural);

        // The inset, the 68 line and its Body line under, 8, the Controls row, the inset, the slack.
        Assert.Equal(12 + 92 + 20 + 8 + 28 + 12 + 2, medium.Natural.Height);
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
