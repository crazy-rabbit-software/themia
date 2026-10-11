using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The arithmetic between the mixer's scalar and the tile. [S-220] [S-221]
/// <para>
/// The cases that matter are the two ends: a scalar that is nearly zero must not read as silence,
/// and the round trip through the slider must land back on the same number or the widget and the
/// volume flyout disagree by a step forever.
/// </para>
/// </summary>
public class VolumeRulesTests
{
    [Fact]
    public void S220_A_level_that_is_only_just_audible_does_not_read_as_silence()
    {
        // 0.2% would round to 0, and "0%" beside audible sound is the widget calling the machine
        // a liar.
        Assert.Equal(1, VolumeRules.Percent(0.002));
        Assert.Equal(1, VolumeRules.Percent(0.0001));
    }

    [Fact]
    public void S220_Silence_reads_as_silence()
    {
        Assert.Equal(0, VolumeRules.Percent(0));
        Assert.Equal(0, VolumeRules.Percent(-1));
        Assert.Equal(0, VolumeRules.Percent(double.NaN));
    }

    [Fact]
    public void S220_A_level_above_the_top_is_clamped_rather_than_reported()
    {
        Assert.Equal(100, VolumeRules.Percent(1));
        Assert.Equal(100, VolumeRules.Percent(1.4));
    }

    [Fact]
    public void S220_Half_a_percent_rounds_the_way_windows_rounds_it()
    {
        // Away from zero, as the volume flyout does. 0.125 is exact in binary, so this is testing
        // the rounding mode rather than the representation of a decimal literal.
        Assert.Equal(13, VolumeRules.Percent(0.125));
        Assert.Equal(35, VolumeRules.Percent(0.346));
        Assert.Equal(34, VolumeRules.Percent(0.344));
    }

    [Fact]
    public void S221_Every_whole_step_survives_the_round_trip_through_the_slider()
    {
        // A slider dropped on n must read back as n. Anything else and dragging the tile once
        // leaves it permanently one step away from the system mixer.
        for (var percent = 0; percent <= 100; percent++)
        {
            Assert.Equal(percent, VolumeRules.Percent(VolumeRules.Scalar(percent)));
        }
    }

    [Fact]
    public void S222_Mute_outranks_the_level_underneath_it()
    {
        Assert.Equal(VolumeLevel.Muted, VolumeRules.Level(0.8, muted: true));
    }

    [Fact]
    public void S222_A_level_turned_all_the_way_down_is_not_the_same_state_as_muted()
    {
        Assert.Equal(VolumeLevel.Silent, VolumeRules.Level(0, muted: false));
    }

    [Theory]
    [InlineData(0.01, VolumeLevel.Low)]
    [InlineData(0.33, VolumeLevel.Low)]
    [InlineData(0.34, VolumeLevel.Medium)]
    [InlineData(0.66, VolumeLevel.Medium)]
    [InlineData(0.67, VolumeLevel.High)]
    [InlineData(1.0, VolumeLevel.High)]
    public void S222_The_speaker_glyph_steps_at_a_third_and_two_thirds(double scalar, VolumeLevel expected)
        => Assert.Equal(expected, VolumeRules.Level(scalar, muted: false));

    [Theory]
    [InlineData(120, 76)]   // XS
    [InlineData(200, 106)]  // S
    [InlineData(320, 196)]  // M
    [InlineData(480, 116)]  // Wide
    [InlineData(560, 376)]  // L
    [InlineData(800, 516)]  // XL
    public void S2731_the_slider_lies_along_every_tile_that_is_not_a_column(double width, double height)
        => Assert.Equal(VolumeAxis.Horizontal, VolumeRules.Axis(new WidgetArea(width, height)));

    [Fact]
    public void S2731_a_tall_column_stands_the_slider_up()
        => Assert.Equal(VolumeAxis.Vertical, VolumeRules.Axis(new WidgetArea(200, 356)));

    [Fact]
    public void S2731_an_unmeasured_tile_starts_horizontal()
        => Assert.Equal(VolumeAxis.Horizontal, VolumeRules.Axis(default));
}
