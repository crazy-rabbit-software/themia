using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11300] [S-6740] The timers' sized face, a ring's hole and the split-flap cards.</summary>
public sealed class TimerFaceTests
{
    private static readonly ReadoutText Timer = new("88:88", LineUnder: true);

    [Fact]
    public void S6717_a_rings_hole_is_three_quarters_wide_and_half_tall()
    {
        Assert.Equal((75d, 50d), ChartRules.Hole(104));
        Assert.Equal((0d, 0d), ChartRules.Hole(2));
    }

    [Fact]
    public void S6740_a_card_is_its_figures_and_12_either_side_as_tall_as_the_steps_line()
    {
        Assert.Equal(Math.Ceiling(HeroFit.Ems("88") * 96) + 24, FlipRules.CardWidth(FamilyType.Hero96, "88"));
        Assert.Equal((2 * FlipRules.CardWidth(FamilyType.Big40, "88")) + 8, FlipRules.RowWidth(FamilyType.Big40, 2, "88"));

        var plan = FlipRules.Plan(new WidgetArea(800, 560), 768, 528, 2, "88", oneReadout: true);
        Assert.Equal(FamilyType.Hero192, plan.Step);
        Assert.Equal(252, plan.CardHeight);
    }

    [Fact]
    public void S6740_cards_take_the_largest_step_on_which_the_row_and_what_stands_beside_it_fit()
    {
        // Two cards on 96 are 2 × (112 + 24) + 8 = 280 wide: a 288 box holds them, not with 40 beside them.
        Assert.Equal(FamilyType.Hero96, FlipRules.Plan(new WidgetArea(320, 204), 288, 180, 2, "88", oneReadout: true).Step);
        Assert.Equal(FamilyType.Hero68, FlipRules.Plan(new WidgetArea(320, 204), 288, 180, 2, "88", oneReadout: true, beside: _ => 40).Step);

        // A data widget's cards stop at the area's ceiling (28 on a regular tile); a box too short for the line under sheds it.
        Assert.Equal(FamilyType.Big28, FlipRules.Plan(new WidgetArea(320, 204), 288, 180, 2, "88", oneReadout: false).Step);
        Assert.False(FlipRules.Plan(new WidgetArea(120, 84), 88, 40, 3, "88", oneReadout: true, under: true).Under);
    }

    [Fact]
    public void S11300_the_face_is_on_the_step_its_size_names_and_a_ring_one_step_under_in_the_least_ring_that_holds_it()
    {
        Assert.Equal(FamilyType.Big40, TimerFace.Sized(ReadoutSize.Small, Timer).Step);
        Assert.Equal(FamilyType.Hero68, TimerFace.Sized(ReadoutSize.Medium, Timer).Step);
        Assert.Equal(FamilyType.Hero96, TimerFace.Sized(ReadoutSize.Large, Timer).Step);
        Assert.Equal(0, TimerFace.Sized(ReadoutSize.Medium, Timer).Ring);

        var ring = TimerFace.Sized(ReadoutSize.Medium, Timer, ring: true);
        Assert.Equal(FamilyType.Big40, ring.Step);
        Assert.Equal(0, ring.Ring % 4);
        var (holeWidth, holeHeight) = ChartRules.Hole(ring.Ring);
        Assert.True(ReadoutRules.Width(ring.Step, Timer) <= holeWidth && ReadoutRules.Height(ring.Step, Timer) <= holeHeight);
        var (smallerWidth, smallerHeight) = ChartRules.Hole(ring.Ring - 4);
        Assert.False(ReadoutRules.Width(ring.Step, Timer) <= smallerWidth && ReadoutRules.Height(ring.Step, Timer) <= smallerHeight);
    }

    [Fact]
    public void S11300_at_small_the_controls_are_icon_buttons_larger_the_worded_ones_at_their_longest_label()
    {
        Assert.Equal((3 * 28) + (2 * 8), TimerFace.ControlsWidth(ReadoutSize.Small, ["Resume"], icons: 2));
        Assert.Equal(TimerFace.WordedWidth("Resume") + (2 * 28) + (2 * 8), TimerFace.ControlsWidth(ReadoutSize.Medium, ["Resume"], icons: 2));
        Assert.Equal(40, TimerFace.ControlsWidth(ReadoutSize.Large, [], aSwitch: true));
        Assert.Equal("Resume", TimerFace.Longest("Start", "Pause", "Resume"));
    }

    [Fact]
    public void S11300_the_card_is_the_face_and_its_row_in_the_inset_one_height_up_to_half_again_wider()
    {
        var face = TimerFace.Sized(ReadoutSize.Medium, Timer);
        var sizing = TimerFace.Sizing(face, controls: 100, header: false, below: 28);

        Assert.True(sizing.FixedHeight && !sizing.FixedWidth);
        Assert.Equal(12 + Math.Ceiling(face.Height) + 28 + 8 + 28 + 12 + 2, sizing.Natural.Height);
        Assert.Equal(Math.Ceiling(face.Width) + 24 + 2, sizing.Natural.Width);
        Assert.Equal(Math.Ceiling(sizing.Natural.Width * 1.5), sizing.Max.Width);
        Assert.Equal(sizing.Natural.Height + 24, TimerFace.Sizing(face, 100, header: true, below: 28).Natural.Height);
    }
}
