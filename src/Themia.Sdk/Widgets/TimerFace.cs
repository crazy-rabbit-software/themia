using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11300] A timer's face at its Size: the readout's step, the ring's diameter (0 without one) and the
/// face's own width and height (the readout with its line under, or the ring holding it), the Controls
/// row not included.
/// </summary>
public readonly record struct TimerSized(TypeStep Step, double Ring, double Width, double Height);

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
    // ---- the sized face ([S-11300]) ----------------------------------------------------------

    /// <summary>The room a face is given past its own size, so a box rounded to the device's pixels still holds it (as the clock's).</summary>
    public const double Slack = 2;

    /// <summary>[S-11300] How far a timer's card may be widened past its face: half again, the face staying centred.</summary>
    public const double WidthRange = 1.5;

    /// <summary>
    /// [S-11300] The readout's step inside a ring at each size, one step under the plain face's (28, 40,
    /// 68), so a ring stays a widget's size: the ring is drawn round the number, never the number grown to the ring.
    /// </summary>
    public static TypeStep RingStep(ReadoutSize size) => ReadoutSizes.Normalise(size) switch
    {
        ReadoutSize.Small => FamilyType.Big28,
        ReadoutSize.Large => FamilyType.Hero68,
        _ => FamilyType.Big40,
    };

    /// <summary>
    /// [S-11300] The least ring (in fours) whose hole (<see cref="ChartRules.Hole"/>) holds
    /// <paramref name="worst"/> on <see cref="RingStep"/> with its line under; never under <see cref="RingFloor"/>.
    /// </summary>
    public static double RingAt(ReadoutSize size, ReadoutText worst)
    {
        var step = RingStep(size);
        var need = Math.Max(ReadoutRules.Width(step, worst) / 0.75, 2 * ReadoutRules.Height(step, worst)) + (2 * FamilyShape.Stroke);
        return Math.Max(RingFloor, Math.Ceiling(need / 4) * 4);
    }

    /// <summary>
    /// [S-11300] The face <paramref name="size"/> names for <paramref name="worst"/> (the widest the
    /// reading gets): the readout on <see cref="ReadoutSizes.Step"/> (40, 68, 96) with its line under,
    /// or with <paramref name="ring"/> the ring of <see cref="RingAt"/> with the readout in its hole.
    /// </summary>
    public static TimerSized Sized(ReadoutSize size, ReadoutText worst, bool ring = false)
    {
        worst = worst with { OneReadout = true };
        if (ring)
        {
            var diameter = RingAt(size, worst);
            return new TimerSized(RingStep(size), diameter, diameter, diameter);
        }

        var step = ReadoutSizes.Step(size);
        return new TimerSized(step, 0, ReadoutRules.Width(step, worst), ReadoutRules.Height(step, worst));
    }

    /// <summary>
    /// [S-11300] Whether a timer's controls are icon buttons: at Small, where a worded button would be
    /// wider than the number over it. The row itself is always there.
    /// </summary>
    public static bool IconControls(ReadoutSize size) => ReadoutSizes.Normalise(size) == ReadoutSize.Small;

    /// <summary>
    /// [S-11300] How wide a timer's Controls row is: its <paramref name="worded"/> buttons (each at its
    /// longest label: the main action, a Snooze) as text, or at Small as icon buttons, then
    /// <paramref name="icons"/> icon buttons, 8 apart; a switch counts its own 40.
    /// </summary>
    public static double ControlsWidth(ReadoutSize size, IReadOnlyList<string> worded, int icons = 0, bool aSwitch = false)
    {
        ArgumentNullException.ThrowIfNull(worded);
        var parts = new List<double>();
        if (aSwitch)
        {
            parts.Add(ControlsRules.SwitchWidth);
        }

        foreach (var label in worded)
        {
            parts.Add(IconControls(size) ? FamilyShape.IconButton : WordedWidth(label));
        }

        for (var i = 0; i < icons; i++)
        {
            parts.Add(FamilyShape.IconButton);
        }

        return parts.Sum() + (Math.Max(0, parts.Count - 1) * ControlsRules.Gap);
    }

    /// <summary>A worded button's width as estimated: its Body text and the button's padding either side.</summary>
    public static double WordedWidth(string label) => Math.Ceiling(FamilyType.EstimateWidth(FamilyType.Body, (label ?? string.Empty).Length) + (2 * FamilyShape.ButtonPadding));

    /// <summary>The longest of a button's labels (Start, Pause, Resume), the one its row is sized on.</summary>
    public static string Longest(params string[] labels) => labels.OrderByDescending(l => l?.Length ?? 0).FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// [S-11300] A timer's card from its face: the inset all round (the header's content top over it where
    /// the kind starts with one), the face and <paramref name="extra"/> under it (a progress bar), the
    /// Controls row and its 8 where it has one, <paramref name="below"/> under that (a stopwatch's laps),
    /// as wide as the wider of the face and the row. One height; the width may grow to
    /// <see cref="WidthRange"/> times, centred, nothing appearing.
    /// </summary>
    public static WidgetSizing Sizing(TimerSized face, double controls, bool header, double extra = 0, double below = 0)
    {
        var width = Math.Ceiling(Math.Max(face.Width, controls)) + (2 * FamilySpace.Inset) + Slack;
        var height = (header ? FamilySpace.ContentTop : FamilySpace.Inset)
            + Math.Ceiling(face.Height + extra) + (controls > 0 ? ControlsCost : 0) + below + FamilySpace.Inset + Slack;
        var box = new SizeDip(width, height);
        return WidgetSizing.Range(box, box, box with { Width = Math.Ceiling(width * WidthRange) });
    }
}
