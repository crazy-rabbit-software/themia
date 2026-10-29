using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-4426] What the focus widget shows at a size, on the timers' face ([S-6739]): the ring's diameter
/// (0 without one), the time's step, whether its status line and the Controls row show, and the box
/// over that row.
/// </summary>
public readonly record struct FocusPlan(double Ring, TypeStep Step, bool Status, bool Controls, double Box);

/// <summary>[S-4426] The focus widget's ladder.</summary>
public static class FocusSessionLayout
{
    /// <summary>
    /// [S-4426] The ladder: the time as a one-readout Readout, centred, the status line under it as it fits,
    /// the Controls row (Start with −/+ while idle, End session while it runs) where the floor step and it
    /// fit — the time alone otherwise, a click on it starting or ending; the ring of the time gone around
    /// the time from expanded both ways (<see cref="TimerFace.Ring"/>). Sized for the widest reading of the
    /// length (<see cref="FocusSessionRules.WidestClock"/>), so it never resizes as it counts.
    /// </summary>
    public static FocusPlan Plan(WidgetArea area, string widest)
    {
        if (!area.IsMeasured)
        {
            return new FocusPlan(0, FamilyType.Big28, false, false, 0);
        }

        var worst = new ReadoutText(widest, LineUnder: true, OneReadout: true);
        var width = area.Width - (2 * FamilySpace.Inset);
        var height = area.Height - FamilySpace.Inset;
        if (area.AtLeast(SizeClass.Expanded, SizeClass.Expanded))
        {
            var ring = TimerFace.Ring(area, width, height, worst, controls: true);
            return new FocusPlan(ring.Ring, ring.Step, ring.Under, ring.Controls, ring.Box);
        }

        var plain = TimerFace.Plan(area, width, height, worst, controls: true);
        return new FocusPlan(0, plain.Step, plain.Under, plain.Controls, plain.Box);
    }
}
