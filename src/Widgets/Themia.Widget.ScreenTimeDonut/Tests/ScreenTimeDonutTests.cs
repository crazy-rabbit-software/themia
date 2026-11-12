using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ScreenTimeDonut.Tests;

/// <summary>[S-11515] The donut is a square ring by its Size, the card round it in the family's inset.</summary>
public sealed class ScreenTimeDonutTests
{
    private static WidgetDescriptor Kind => new ScreenTimeDonutModule().Descriptor;

    [Theory]
    [InlineData(ReadoutSize.Small, 128)]
    [InlineData(ReadoutSize.Medium, 168)]
    [InlineData(ReadoutSize.Large, 216)]
    public void S11515_each_size_is_one_card_round_its_ring(ReadoutSize size, double ring)
    {
        var sizing = Kind.Sizing(new ScreenTimeDonutConfig { Size = size });
        Assert.Equal(new SizeDip(ring + (2 * FamilySpace.Inset), FamilySpace.ContentTop + ring + FamilySpace.Inset), sizing.Natural);
        Assert.True(sizing.FixedWidth && sizing.FixedHeight);

        var plan = ScreenTimeDonutLayout.Donut(size);
        Assert.Equal((ring, FamilyShape.Track, ring - (2 * FamilyShape.Track)), (plan.Ring, plan.Thickness, plan.Hole));
    }

    [Fact]
    public void S11515_a_new_one_is_medium_its_figure_on_28_and_a_large_ring_may_take_40()
    {
        Assert.Equal(ReadoutSize.Medium, new ScreenTimeDonutConfig().Size);
        Assert.Equal(ReadoutSize.Large, ((ScreenTimeDonutConfig)new ScreenTimeDonutConfig { Size = ReadoutSize.Large }.Clone()).Size);
        Assert.Equal(FamilyType.Big28, ScreenTimeDonutLayout.Donut(ReadoutSize.Medium).Largest);
        Assert.Equal(FamilyType.Big40, ScreenTimeDonutLayout.Donut(ReadoutSize.Large).Largest);

        // A day's "4h 37m" and its line stand whole in a medium hole.
        var figure = ScreenTimeRules.Figure((4 * 3600) + (37 * 60), FamilyType.Big28, ScreenTimeDonutLayout.Donut(ReadoutSize.Medium).Hole, under: true);
        Assert.Equal((FamilyType.Big28, true), (figure.Step, figure.Under));
    }
}
