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

        var plan = FlipRules.Plan(768, 528, 2, "88");
        Assert.Equal(FamilyType.Hero192, plan.Step);
        Assert.Equal(252, plan.CardHeight);
    }

    [Fact]
    public void S6740_cards_take_the_largest_step_on_which_the_row_and_what_stands_beside_it_fit()
    {
        // Two cards on 96 are 2 × (112 + 24) + 8 = 280 wide: a 288 box holds them, not with 40 beside them.
        Assert.Equal(FamilyType.Hero96, FlipRules.Plan(288, 180, 2, "88").Step);
        Assert.Equal(FamilyType.Hero68, FlipRules.Plan(288, 180, 2, "88", beside: _ => 40).Step);

        // A box too short for the line under sheds it.
        Assert.False(FlipRules.Plan(88, 40, 3, "88", under: true).Under);
    }

    [Fact]
    public void S11300_the_face_is_on_the_one_readout_step_and_a_ring_one_step_under_in_the_least_ring_that_holds_it()
    {
        Assert.Equal(FamilyType.Hero68, TimerFace.Sized(Timer).Step);
        Assert.Equal(0, TimerFace.Sized(Timer).Ring);

        var ring = TimerFace.Sized(Timer, ring: true);
        Assert.Equal(FamilyType.Big40, ring.Step);
        Assert.Equal(0, ring.Ring % 4);
        var (holeWidth, holeHeight) = ChartRules.Hole(ring.Ring);
        Assert.True(ReadoutRules.Width(ring.Step, Timer) <= holeWidth && ReadoutRules.Height(ring.Step, Timer) <= holeHeight);
        var (smallerWidth, smallerHeight) = ChartRules.Hole(ring.Ring - 4);
        Assert.False(ReadoutRules.Width(ring.Step, Timer) <= smallerWidth && ReadoutRules.Height(ring.Step, Timer) <= smallerHeight);
    }

    [Fact]
    public void S11300_the_controls_are_the_worded_ones_at_their_longest_label_and_the_icon_buttons()
    {
        Assert.Equal(TimerFace.WordedWidth("Resume") + (2 * 28) + (2 * 8), TimerFace.ControlsWidth(["Resume"], icons: 2));
        Assert.Equal(40, TimerFace.ControlsWidth([], aSwitch: true));
        Assert.Equal("Resume", TimerFace.Longest("Start", "Pause", "Resume"));
    }

    [Fact]
    public void S11300_the_card_is_the_face_and_its_row_in_the_inset_one_height_up_to_half_again_wider()
    {
        var face = TimerFace.Sized(Timer);
        var sizing = TimerFace.Sizing(face, controls: 100, header: false, below: 28);

        Assert.True(sizing.Natural.Height == sizing.Max.Height && !sizing.FixedWidth);
        Assert.Equal(12 + Math.Ceiling(face.Height) + 28 + 8 + 28 + 12 + 2, sizing.Natural.Height);
        Assert.Equal(Math.Ceiling(face.Width) + 24 + 2, sizing.Natural.Width);
        Assert.Equal(Math.Ceiling(sizing.Natural.Width * 1.5), sizing.Max.Width);
        Assert.Equal(sizing.Natural.Height, TimerFace.Sizing(face, 100, header: true, below: 28).Natural.Height);
    }

    [Fact]
    public void S11300_S12833_the_card_scales_down_as_one_until_its_smallest_text_is_at_the_floor()
    {
        // Owner, 2026-11-20: "the size should not be settable, it is set by the size of the widget": the natural card is
        // the face at the one-readout step; a smaller card draws it smaller, down to its Body text (the line under, the
        // buttons) at 9 (WR-14).
        var face = TimerFace.Sized(Timer);
        var sizing = TimerFace.Sizing(face, controls: 100, header: false);
        Assert.Equal(Math.Ceiling(sizing.Natural.Height * 9 / 14), sizing.Min.Height);
        Assert.Equal(Math.Ceiling(sizing.Natural.Width * 9 / 14), sizing.Min.Width);

        // A ring's readout is 40 with a Body line under it: the same floor.
        var ring = TimerFace.Sizing(TimerFace.Sized(Timer, ring: true), controls: 0, header: false);
        Assert.Equal(Math.Max(Themia.Sdk.WidgetSizing.LeastScaled, Math.Ceiling(ring.Natural.Height * 9 / 14)), ring.Min.Height);
    }
}
