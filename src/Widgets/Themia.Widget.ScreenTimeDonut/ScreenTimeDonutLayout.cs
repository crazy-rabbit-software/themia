using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time donut shows at one size ([S-3342]).</summary>
/// <param name="Ring">The ring's diameter, DIPs: never dropped.</param>
/// <param name="Thickness">The ring's stroke: the family's track ([S-6704]).</param>
/// <param name="Hole">The hole's diameter, where the figure stands.</param>
public readonly record struct ScreenTimeDonutPlan(double Ring, double Thickness, double Hole);

/// <summary>[S-3342] The donut's ladder.</summary>
public static class ScreenTimeDonutLayout
{
    /// <summary>
    /// [S-3342] The donut: one ring as large as the view's area less the card's insets lets it be
    /// (16 at the sides and the foot), its wedges in the family's track (4), the figure in its hole
    /// (<see cref="ScreenTimeRules.Figure"/>).
    /// </summary>
    public static ScreenTimeDonutPlan Donut(WidgetArea area)
    {
        var thickness = FamilyShape.Track;
        if (!area.IsMeasured)
        {
            return new ScreenTimeDonutPlan(0, thickness, 0);
        }

        var ring = Math.Floor(Math.Max(0, Math.Min(area.Width - (2 * FamilySpace.Inset), area.Height - FamilySpace.Inset)));
        return new ScreenTimeDonutPlan(ring, thickness, Math.Max(0, ring - (2 * thickness)));
    }
}
