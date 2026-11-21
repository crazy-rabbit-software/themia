using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time donut draws in its card ([S-11515]).</summary>
/// <param name="Ring">The ring's diameter, DIPs.</param>
/// <param name="Thickness">The ring's stroke: the family's track ([S-6704]).</param>
/// <param name="Hole">The hole's diameter, where the figure stands.</param>
/// <param name="Largest">The largest step the figure in the hole may take: 28.</param>
public readonly record struct ScreenTimeDonutPlan(double Ring, double Thickness, double Hole, TypeStep Largest);

/// <summary>
/// [S-11515] [S-14284] The donut's sizes: one plan, the natural ring with its figure on 28 in its hole, that the card
/// scales as one block with its day line (WR-7, `ReadingFit`); the card round it in the family's inset.
/// </summary>
public static class ScreenTimeDonutLayout
{
    /// <summary>[S-11515] The ring the block is planned at, a new card's.</summary>
    public const double NaturalRing = 168;

    /// <summary>[S-11515] The least card's ring: the block scaled to it still draws its line under at the floor.</summary>
    public const double LeastRing = 128;

    /// <summary>[S-11515] [S-14284] The one plan: the natural ring, its wedges in the family's track (4), the figure in its hole on 28.</summary>
    public static ScreenTimeDonutPlan Plan { get; } = new(NaturalRing, FamilyShape.Track, NaturalRing - (2 * FamilyShape.Track), FamilyType.Big28);

    /// <summary>[S-454] [S-12481] [S-12803] The day line over the ring for a day (its line and the gap under it), none for a window.</summary>
    public static double DayLine(ScreenTimePeriod period) => ScreenTimeRules.DayLine(period);

    /// <summary>[S-11515] The card round a ring of <paramref name="ring"/>: its day line, the ring in the card's 12 inset at the sides and the foot.</summary>
    public static SizeDip Box(double ring, ScreenTimePeriod period)
        => new(ring + (2 * FamilySpace.Inset), FamilySpace.ContentTop + DayLine(period) + ring + FamilySpace.Inset);

    /// <summary>
    /// [S-11515] The sizing: a new one round <see cref="NaturalRing"/>, the least round <see cref="LeastRing"/>; the card's
    /// size is the user's ([S-12411]) and the block (the day line over the ring) scales with it as one ([S-14284]).
    /// </summary>
    public static WidgetSizing Sizing(ScreenTimePeriod period)
    {
        var natural = Box(NaturalRing, period);
        return WidgetSizing.Range(Box(LeastRing, period), natural, natural);
    }
}
