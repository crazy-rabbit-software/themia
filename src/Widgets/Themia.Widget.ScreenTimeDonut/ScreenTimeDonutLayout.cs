using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time donut draws in its card ([S-11515]).</summary>
/// <param name="Ring">The ring's diameter, DIPs.</param>
/// <param name="Thickness">The ring's stroke: the family's track ([S-6704]).</param>
/// <param name="Hole">The hole's diameter, where the figure stands.</param>
/// <param name="Largest">The largest step the figure in the hole may take: 28, or 40 on a large ring.</param>
public readonly record struct ScreenTimeDonutPlan(double Ring, double Thickness, double Hole, TypeStep Largest);

/// <summary>
/// [S-11515] The donut's sizes: a square ring as large as the card holds (no Size setting, [S-2449] retired), the card
/// round it in the family's inset.
/// </summary>
public static class ScreenTimeDonutLayout
{
    /// <summary>[S-11515] A new donut's ring.</summary>
    public const double NaturalRing = 168;

    /// <summary>[S-11515] The least ring: its figure on 28 with its line under still fits the hole.</summary>
    public const double LeastRing = 128;

    /// <summary>[S-11515] From this ring the figure may take 40.</summary>
    public const double LargeRing = 216;

    /// <summary>[S-11515] The ring of <paramref name="diameter"/> (never under <see cref="LeastRing"/>): its wedges in the family's track (4), the figure in its hole.</summary>
    public static ScreenTimeDonutPlan Donut(double diameter)
    {
        var ring = double.IsFinite(diameter) ? Math.Max(LeastRing, Math.Floor(diameter)) : NaturalRing;
        var thickness = FamilyShape.Track;
        return new ScreenTimeDonutPlan(ring, thickness, ring - (2 * thickness), ring >= LargeRing ? FamilyType.Big40 : FamilyType.Big28);
    }

    /// <summary>[S-454] [S-12481] [S-12803] The day line over the ring for a day (its line and the gap under it), none for a window.</summary>
    public static double DayLine(ScreenTimePeriod period) => ScreenTimeRules.DayLine(period);

    /// <summary>
    /// [S-11515] The ring the view's slot (<paramref name="width"/> × <paramref name="height"/>: the card under its top
    /// inset) holds under the day line, in the inset at the sides and the foot.
    /// </summary>
    public static double RingIn(double width, double height, ScreenTimePeriod period)
        => Math.Min(width - (2 * FamilySpace.Inset), height - DayLine(period) - FamilySpace.Inset);

    /// <summary>[S-11515] The card round a ring of <paramref name="ring"/>: its day line, the ring in the card's 12 inset at the sides and the foot.</summary>
    public static SizeDip Box(double ring, ScreenTimePeriod period)
        => new(ring + (2 * FamilySpace.Inset), FamilySpace.ContentTop + DayLine(period) + ring + FamilySpace.Inset);

    /// <summary>
    /// [S-11515] The sizing: a new one round <see cref="NaturalRing"/>, the least round <see cref="LeastRing"/>; the card's
    /// size is the user's and the ring grows with it ([S-12411]), centred across, at the content's top.
    /// </summary>
    public static WidgetSizing Sizing(ScreenTimePeriod period)
    {
        var natural = Box(NaturalRing, period);
        return WidgetSizing.Range(Box(LeastRing, period), natural, natural);
    }
}
