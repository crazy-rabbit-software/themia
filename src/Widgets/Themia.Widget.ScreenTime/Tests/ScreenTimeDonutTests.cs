using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ScreenTime.Tests;

/// <summary>[S-11515] The donut is a square ring as large as its card holds, the card round it in the family's inset.</summary>
public sealed class ScreenTimeDonutTests
{
    private static WidgetDescriptor Kind => new ScreenTimeDonutModule().Descriptor;

    private static SizeDip Box(double ring) => new(ring + (2 * FamilySpace.Inset), FamilySpace.ContentTop + ScreenTimeDonutLayout.DayLine(ScreenTimePeriod.Day) + ring + FamilySpace.Inset);

    [Fact]
    public void S11515_S2449_retired_a_new_card_rounds_the_natural_ring_the_least_the_least_ring_and_no_practical_max()
    {
        var sizing = Kind.Sizing(new ScreenTimeDonutConfig());
        Assert.Equal(Box(ScreenTimeDonutLayout.NaturalRing), sizing.Natural);
        Assert.Equal(Box(ScreenTimeDonutLayout.LeastRing), sizing.Min);
        Assert.Equal(new SizeDip(SizeStretch.Ceiling, SizeStretch.Ceiling), sizing.Max); // relaxed, [S-12411]
        Assert.Null(typeof(ScreenTimeDonutConfig).GetProperty("Size"));
    }

    [Fact]
    public void S14284_one_plan_the_natural_ring_its_figure_on_28_in_its_hole()
    {
        var plan = ScreenTimeDonutLayout.Plan;
        Assert.Equal((ScreenTimeDonutLayout.NaturalRing, FamilyShape.Track, ScreenTimeDonutLayout.NaturalRing - (2 * FamilyShape.Track)), (plan.Ring, plan.Thickness, plan.Hole));
        Assert.Equal(FamilyType.Big28, plan.Largest);

        // A day's "4h 37m" and its line stand whole in the hole.
        var figure = ScreenTimeRules.Figure((4 * 3600) + (37 * 60), plan.Largest, plan.Hole, under: true);
        Assert.Equal((FamilyType.Big28, true), (figure.Step, figure.Under));
    }

    [Theory]
    [InlineData(ScreenTimePeriod.Day)]
    [InlineData(ScreenTimePeriod.Week)]
    public void S14284_the_least_card_scales_the_block_no_further_than_its_smallest_text_reads(ScreenTimePeriod period)
    {
        // [S-12833] WR-14: the block (the day line over the natural ring) scaled into the least card's inset still draws
        // its smallest text, the figure's line under, at the floor.
        var least = ScreenTimeDonutLayout.Sizing(period).Min;
        var room = (Width: least.Width - (2 * FamilySpace.Inset), Height: least.Height - FamilySpace.ContentTop - FamilySpace.Inset);
        var block = (Width: ScreenTimeDonutLayout.NaturalRing, Height: ScreenTimeDonutLayout.DayLine(period) + ScreenTimeDonutLayout.NaturalRing);
        var scale = Math.Min(room.Width / block.Width, room.Height / block.Height);

        Assert.True(scale >= ScaleFloor.Least(FamilyType.Under(FamilyType.Big28).FontSize), $"{period}: {scale}");
    }
}
