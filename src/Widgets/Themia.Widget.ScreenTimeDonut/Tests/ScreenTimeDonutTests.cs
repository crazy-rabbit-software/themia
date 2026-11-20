using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ScreenTimeDonut.Tests;

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

    [Theory]
    [InlineData(128)]
    [InlineData(168)]
    [InlineData(216)]
    [InlineData(400)]
    public void S11515_the_ring_is_as_large_as_the_card_holds_in_its_inset(double ring)
    {
        // The view's slot is the card under its top inset.
        var box = Box(ring);
        var slot = (Width: box.Width, Height: box.Height - FamilySpace.ContentTop);
        Assert.Equal(ring, ScreenTimeDonutLayout.RingIn(slot.Width, slot.Height, ScreenTimePeriod.Day));

        // A wider card keeps the ring its height holds; a taller one its width's.
        Assert.Equal(ring, ScreenTimeDonutLayout.RingIn(slot.Width + 100, slot.Height, ScreenTimePeriod.Day));
        Assert.Equal(ring, ScreenTimeDonutLayout.RingIn(slot.Width, slot.Height + 100, ScreenTimePeriod.Day));

        var plan = ScreenTimeDonutLayout.Donut(ring);
        Assert.Equal((ring, FamilyShape.Track, ring - (2 * FamilyShape.Track)), (plan.Ring, plan.Thickness, plan.Hole));
    }

    [Fact]
    public void S11515_a_ring_is_never_under_its_least_its_figure_on_28_and_a_large_ring_may_take_40()
    {
        Assert.Equal(ScreenTimeDonutLayout.LeastRing, ScreenTimeDonutLayout.Donut(20).Ring);
        Assert.Equal(FamilyType.Big28, ScreenTimeDonutLayout.Donut(ScreenTimeDonutLayout.NaturalRing).Largest);
        Assert.Equal(FamilyType.Big40, ScreenTimeDonutLayout.Donut(ScreenTimeDonutLayout.LargeRing).Largest);

        // A day's "4h 37m" and its line stand whole in a natural hole.
        var figure = ScreenTimeRules.Figure((4 * 3600) + (37 * 60), FamilyType.Big28, ScreenTimeDonutLayout.Donut(ScreenTimeDonutLayout.NaturalRing).Hole, under: true);
        Assert.Equal((FamilyType.Big28, true), (figure.Step, figure.Under));
    }
}
