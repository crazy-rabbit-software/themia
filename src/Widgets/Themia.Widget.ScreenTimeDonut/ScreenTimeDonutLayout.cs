namespace Themia.Core.Widgets;

/// <summary>What the screen-time donut shows at one size ([S-3342]).</summary>
/// <param name="Ring">The ring's diameter, DIPs: never dropped.</param>
/// <param name="Thickness">The ring's stroke.</param>
/// <param name="Detail">A second line in the hole (the share, "of 4h"), when the hole has room for it.</param>
public readonly record struct ScreenTimeDonutPlan(double Ring, double Thickness, bool Detail);

/// <summary>[S-3342] The donut's ladder.</summary>
public static class ScreenTimeDonutLayout
{
    /// <summary>The ring never shrinks below this: under it the total in its hole cannot be read.</summary>
    public const double MinRing = 56;

    /// <summary>A hole narrower than this carries the figure alone.</summary>
    public const double DetailHole = 72;

    /// <summary>
    /// [S-3342] The donut: one ring as large as the tile lets it be, the total in its hole, and the
    /// line under it once the hole is wide enough to read it.
    /// </summary>
    public static ScreenTimeDonutPlan Donut(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new ScreenTimeDonutPlan(MinRing, Thickness(MinRing), false);
        }

        var fits = Math.Min(area.Width - (2 * ScreenTimeLayout.Inset), area.Height - ScreenTimeLayout.Gap);
        var ring = Math.Floor(Math.Max(Math.Min(MinRing, Math.Max(0, area.Height - 2)), fits));
        var thickness = Thickness(ring);
        return new ScreenTimeDonutPlan(ring, thickness, ring - (2 * thickness) >= DetailHole);
    }

    /// <summary>[S-3342] The ring's stroke: a seventh of its size, never thinner than 8.</summary>
    public static double Thickness(double ring) => Math.Max(8, ring * 0.14);
}
