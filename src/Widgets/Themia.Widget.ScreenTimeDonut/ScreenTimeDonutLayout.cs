using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time donut draws at its Size ([S-11515]).</summary>
/// <param name="Ring">The ring's diameter, DIPs.</param>
/// <param name="Thickness">The ring's stroke: the family's track ([S-6704]).</param>
/// <param name="Hole">The hole's diameter, where the figure stands.</param>
/// <param name="Largest">The largest step the figure in the hole may take: 28, or 40 on a large ring.</param>
public readonly record struct ScreenTimeDonutPlan(double Ring, double Thickness, double Hole, TypeStep Largest);

/// <summary>[S-11515] The donut's sizes: a square ring by its Size, the card round it in the family's inset.</summary>
public static class ScreenTimeDonutLayout
{
    /// <summary>[S-11515] The ring's diameter for each Size.</summary>
    public static double Ring(ReadoutSize size) => ReadoutSizes.Normalise(size) switch
    {
        ReadoutSize.Small => 128,
        ReadoutSize.Large => 216,
        _ => 168,
    };

    /// <summary>[S-11515] The ring for <paramref name="size"/>: its wedges in the family's track (4), the figure in its hole.</summary>
    public static ScreenTimeDonutPlan Donut(ReadoutSize size)
    {
        var ring = Ring(size);
        var thickness = FamilyShape.Track;
        return new ScreenTimeDonutPlan(ring, thickness, ring - (2 * thickness), ReadoutSizes.Normalise(size) == ReadoutSize.Large ? FamilyType.Big40 : FamilyType.Big28);
    }

    /// <summary>[S-11515] One size by the Size, with its header: the ring in the card's 12 inset at the sides and the foot.</summary>
    public static WidgetSizing Sizing(ReadoutSize size)
    {
        var ring = Ring(size);
        return WidgetSizing.Fixed(new(ring + (2 * FamilySpace.Inset), FamilySpace.ContentTop + ring + FamilySpace.Inset));
    }
}
