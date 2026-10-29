using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-6739] A timer's face as planned: the readout's step, whether its line under shows, whether the Controls row shows, and the height of the readout's box over that row.</summary>
public readonly record struct TimerPlan(TypeStep Step, bool Under, bool Controls, double Box);

/// <summary>[S-6739] A timer's ring face as planned: the ring's diameter (0 when no ring fits: the readout stands alone), the readout's step in its hole, its line under, the Controls row and the box over that row.</summary>
public readonly record struct TimerRingPlan(double Ring, TypeStep Step, bool Under, bool Controls, double Box);

/// <summary>
/// [S-6739] The face the timers share (Alarm, Countdown, Stopwatch, Pomodoro, Focus session). A timer is
/// a one-readout widget ([S-6737]), so its Readout climbs the Hero steps as far as they fit, on fixed
/// steps, with its line under. The Controls row stands at the bottom inset, 8 under the readout's box,
/// and only where the floor step's line, that gap and the row fit: a tile too short for both keeps the
/// readout, which is the widget. A ring face draws the Chart block's ring as large as the box's short
/// side and sets the readout in its hole (<see cref="ChartRules.Hole"/>); a ring under 48, or whose hole cannot
/// hold the floor step's value, is not drawn.
/// </summary>
public static class TimerFace
{
    /// <summary>A ring's least diameter: under it the readout stands alone.</summary>
    public const double RingFloor = FamilyShape.IconHuge;

    /// <summary>What the Controls row takes from the content: the row and the gap over it.</summary>
    public const double ControlsCost = FamilyShape.Button + FamilySpace.S2;

    /// <summary>
    /// [S-6739] Whether the Controls row fits under a readout in content <paramref name="height"/> tall:
    /// the floor step's line (without its line under), the gap and the row.
    /// </summary>
    public static bool ControlsFit(double height, ReadoutText worst)
        => double.IsFinite(height) && ReadoutRules.Height(FamilyType.Big28, worst with { LineUnder = false }) + ControlsCost <= height;

    /// <summary>[S-6739] The plain face in a <paramref name="width"/> × <paramref name="height"/> content box of a view given <paramref name="area"/>.</summary>
    public static TimerPlan Plan(WidgetArea area, double width, double height, ReadoutText worst, bool controls)
    {
        worst = worst with { OneReadout = true };
        var shown = controls && ControlsFit(height, worst);
        var box = Math.Max(0, height - (shown ? ControlsCost : 0));
        var step = ReadoutRules.Step(area, width, box, worst);
        var under = worst.LineUnder && ReadoutRules.Height(step, worst) <= box;
        return new TimerPlan(step, under, shown, box);
    }

    /// <summary>[S-6739] The ring face: a ring of the box's short side over the Controls row, the readout in its hole.</summary>
    public static TimerRingPlan Ring(WidgetArea area, double width, double height, ReadoutText worst, bool controls)
    {
        worst = worst with { OneReadout = true };
        var shown = controls && ControlsFit(height, worst);
        var box = Math.Max(0, height - (shown ? ControlsCost : 0));
        var ring = Math.Floor(Math.Min(width, box));
        if (!double.IsFinite(ring) || ring < RingFloor || !HoleHolds(ring, worst))
        {
            var plain = Plan(area, width, height, worst, controls);
            return new TimerRingPlan(0, plain.Step, plain.Under, plain.Controls, plain.Box);
        }

        var (holeWidth, holeHeight) = ChartRules.Hole(ring);
        var step = ReadoutRules.Step(area, holeWidth, holeHeight, worst);
        var under = worst.LineUnder && ReadoutRules.Height(step, worst) <= holeHeight;
        return new TimerRingPlan(ring, step, under, shown, box);
    }

    /// <summary>[S-6739] Whether a ring's hole holds the readout's floor step (its value and its line, without the line under): else no ring is drawn.</summary>
    public static bool HoleHolds(double ring, ReadoutText worst)
    {
        var (holeWidth, holeHeight) = ChartRules.Hole(ring);
        var bare = worst with { LineUnder = false };
        return ReadoutRules.Width(FamilyType.Big28, bare) <= holeWidth && ReadoutRules.Height(FamilyType.Big28, bare) <= holeHeight;
    }
}
