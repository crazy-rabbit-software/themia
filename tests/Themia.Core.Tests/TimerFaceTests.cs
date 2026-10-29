using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6739] [S-6740] The timers' face, a ring's hole and the split-flap cards (group G5).</summary>
public sealed class TimerFaceTests
{
    private static readonly ReadoutText Timer = new("88:88", LineUnder: true);

    // The view area is the card less its header (44); the content is that less the inset either side and at the foot.
    private static (WidgetArea Area, double Width, double Height) Tier(double w, double h)
        => (new WidgetArea(w, h - FamilySpace.ContentTop), w - (2 * FamilySpace.Inset), h - FamilySpace.ContentTop - FamilySpace.Inset);

    [Fact]
    public void S6739_a_tile_too_short_for_the_readout_and_the_controls_keeps_the_readout()
    {
        var (area, width, height) = Tier(120, 120);
        var plan = TimerFace.Plan(area, width, height, Timer, controls: true);

        Assert.False(plan.Controls);
        Assert.Equal(height, plan.Box);
        Assert.Equal(FamilyType.Big28, plan.Step);
    }

    [Fact]
    public void S6739_the_controls_take_their_row_and_8_and_the_readout_climbs_the_hero_steps_in_the_rest()
    {
        var (area, width, height) = Tier(800, 560);
        var plan = TimerFace.Plan(area, width, height, Timer, controls: true);

        Assert.True(plan.Controls);
        Assert.Equal(height - 40, plan.Box);
        Assert.Equal(FamilyType.Hero192, plan.Step);
        Assert.True(plan.Under);

        var (sArea, sWidth, sHeight) = Tier(200, 150);
        var small = TimerFace.Plan(sArea, sWidth, sHeight, Timer, controls: true);
        Assert.True(small.Controls);
        Assert.Equal(FamilyType.Big28, small.Step);

        // 50 left over the row: the 28 line (36) fits, its Caption line under (16) does not.
        Assert.False(small.Under);
    }

    [Fact]
    public void S6739_without_controls_the_box_is_the_whole_content()
    {
        var (area, width, height) = Tier(320, 240);
        var plan = TimerFace.Plan(area, width, height, Timer, controls: false);

        Assert.False(plan.Controls);
        Assert.Equal(height, plan.Box);
        Assert.Equal(FamilyType.Hero96, plan.Step);
    }

    [Fact]
    public void S6739_a_ring_is_the_short_side_and_its_readout_stands_in_its_hole()
    {
        var (area, width, height) = Tier(560, 420);
        var ring = TimerFace.Ring(area, width, height, Timer, controls: true);

        Assert.True(ring.Controls);
        Assert.Equal(Math.Floor(height - 40), ring.Ring);
        var (holeWidth, holeHeight) = ChartRules.Hole(ring.Ring);
        Assert.True(ReadoutRules.Width(ring.Step, Timer) <= holeWidth);
        Assert.True(ReadoutRules.Height(ring.Step, Timer with { LineUnder = ring.Under }) <= holeHeight);
    }

    [Fact]
    public void S6739_a_ring_under_48_is_not_drawn()
    {
        var ring = TimerFace.Ring(new WidgetArea(200, 196), 120, 120, new ReadoutText("8:88"), controls: false);

        Assert.Equal(120, ring.Ring);

        var tiny = TimerFace.Ring(new WidgetArea(80, 60), 48, 40, Timer, controls: false);
        Assert.Equal(0, tiny.Ring);
    }

    [Fact]
    public void S6739_a_ring_whose_hole_cannot_hold_the_floor_step_is_not_drawn()
    {
        // XS: a 60 ring's hole is 42 × 28, too small for "88:88" on 28 (36 tall): the readout alone.
        var xs = TimerFace.Ring(new WidgetArea(120, 76), 88, 60, Timer, controls: true);
        Assert.Equal(0, xs.Ring);
        Assert.False(TimerFace.HoleHolds(60, Timer));

        // A 104 ring's hole (75 × 50) holds "8:88" on 28 (56 wide, 36 tall).
        Assert.True(TimerFace.HoleHolds(104, new ReadoutText("8:88")));
    }

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
        Assert.Equal(FamilyType.Hero96, FlipRules.Plan(new WidgetArea(320, 196), 288, 180, 2, "88", oneReadout: true).Step);
        Assert.Equal(FamilyType.Big68, FlipRules.Plan(new WidgetArea(320, 196), 288, 180, 2, "88", oneReadout: true, beside: _ => 40).Step);

        // A data widget's cards stop at the area's ceiling; a box too short for the line under sheds it.
        Assert.Equal(FamilyType.Big40, FlipRules.Plan(new WidgetArea(320, 196), 288, 180, 2, "88", oneReadout: false).Step);
        Assert.False(FlipRules.Plan(new WidgetArea(120, 76), 88, 40, 3, "88", oneReadout: true, under: true).Under);
    }
}
