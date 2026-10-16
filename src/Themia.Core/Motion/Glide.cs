using Themia.Core.Model;

namespace Themia.Core.Motion;

/// <summary>[S-6323] What a live readout does with a new value.</summary>
public enum GlideMove
{
    /// <summary>Show the new value at once: animation effects are off, or one end is not a number.</summary>
    Jump = 0,

    /// <summary>Move from the value shown to the new one over one period.</summary>
    Glide = 1,

    /// <summary>The value shown is the new one: nothing to do.</summary>
    Still = 2,
}

/// <summary>
/// [S-6323]..[S-6326] How a live readout moves between two readings (a meter's number, a gauge's arc,
/// a bar's fill, a core tile's shade): from the value it shows now to the new one, **linearly over one
/// period of its series** (a second; a ping's five), the motion the history's slide has ([S-1826]), so
/// a number reaches each reading as that reading's point reaches the chart's edge. Linear rather than
/// ease-out: readings arrive every period, and an ease-out would rush and stall once a second, a pulse
/// a desktop does not need. A reading that arrives mid-glide starts the next glide from where the
/// readout stands, so nothing jumps back.
/// <para>
/// A number does not spin: it steps in its own resolution (a whole percent) and at most
/// <see cref="StepsPerSecond"/> times a second however far it goes (<see cref="Quantum"/>), each step
/// landing on the value the line has reached (<see cref="Shown"/>), and the readout is touched only
/// when the next step is due (<see cref="NextChange"/>) and never once it has arrived.
/// </para>
/// Pure, so the arithmetic is tested without a dispatcher.
/// </summary>
public static class Glide
{
    /// <summary>The most a gliding number changes in a second: enough to read as counting, few enough to stay cheap.</summary>
    public const double StepsPerSecond = 20;

    /// <summary>A boundary reached within rounding (a sixth of a second is not exact in binary) counts as reached.</summary>
    private const double Slack = 1e-9;

    /// <summary>
    /// [S-6329] Whether live values move smoothly now — readouts glide ([S-6323]) and history charts
    /// scroll ([S-1826]): only with Windows' animation effects on ([S-6326], [S-1828]) <b>and</b> the
    /// user's Smooth value changes switch on (<see cref="DesignState.SmoothValues"/>). No design yet
    /// (before the first apply) is the default, on. The one answer both motions ask.
    /// </summary>
    public static bool Smooth(DesignState? design, bool windowsAnimations)
        => windowsAnimations && (design?.SmoothValues ?? true);

    /// <summary>
    /// [S-6324] [S-6326] Glide only between two numbers, and only with Windows' animation effects on
    /// ([S-1010]); a reading appearing, vanishing or becoming unavailable is a state, and jumps.
    /// </summary>
    public static GlideMove Plan(bool animations, double? from, double? to)
    {
        if (from is { } a && to is { } b && double.IsFinite(a) && double.IsFinite(b))
        {
            if (a == b)
            {
                return GlideMove.Still;
            }

            return animations ? GlideMove.Glide : GlideMove.Jump;
        }

        return GlideMove.Jump;
    }

    /// <summary>How far through its glide a readout is, 0..1, linearly; a zero or negative length is done.</summary>
    public static double Progress(TimeSpan elapsed, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 1;
        }

        return Math.Clamp(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
    }

    /// <summary>The value at <paramref name="progress"/> (clamped) along the straight line from one value to the other.</summary>
    public static double At(double from, double to, double progress)
    {
        var p = double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 1;
        return p >= 1 ? to : from + ((to - from) * p);
    }

    /// <summary>
    /// [S-6325] The step a number glides in: its own <paramref name="resolution"/> (0.01 for a whole
    /// percent of a fraction; 0 for a rate, whose unit changes with its size), or coarser when the
    /// distance would take more than <see cref="StepsPerSecond"/> steps a second over
    /// <paramref name="duration"/>.
    /// </summary>
    public static double Quantum(double from, double to, double resolution, TimeSpan duration)
    {
        var distance = Math.Abs(to - from);
        var steps = Math.Max(1, Math.Floor(Math.Max(0, duration.TotalSeconds) * StepsPerSecond));
        var floor = double.IsFinite(resolution) && resolution > 0 ? resolution : 0;
        return Math.Max(floor, double.IsFinite(distance) ? distance / steps : 0);
    }

    /// <summary>
    /// [S-6325] The value a stepping readout shows at <paramref name="progress"/>: the line's value
    /// rounded to the nearest step counted back from the target, so the last step lands on the
    /// reading itself and each one on a value the line has reached; the target once the glide is done.
    /// </summary>
    public static double Shown(double from, double to, double progress, double quantum)
    {
        var value = At(from, to, progress);

        if (progress >= 1 || !(quantum > 0))
        {
            return value;
        }

        // Half a step from the next value counts as there, so a step falls exactly on the boundary
        // NextChange schedules it for (a timer firing on time never shows the old value once more).
        var steps = Math.Ceiling((Math.Abs(to - value) / quantum) - 0.5 - Slack);
        var shown = to - (Math.Sign(to - from) * steps * quantum);

        // Never past the start: the first step is the first value the line has passed.
        return to >= from ? Math.Max(from, shown) : Math.Min(from, shown);
    }

    /// <summary>
    /// [S-6325] The progress at which <see cref="Shown"/> next changes after
    /// <paramref name="progress"/>: when the readout must be touched again. Infinity once it shows the
    /// target (the last step lands on it before the line does) or the glide is over.
    /// </summary>
    public static double NextChange(double from, double to, double progress, double quantum)
    {
        if (progress >= 1 || from == to)
        {
            return double.PositiveInfinity;
        }

        if (!(quantum > 0))
        {
            return 1;
        }

        // Shown changes when (to - value) / quantum passes the next half step towards the target.
        var value = At(from, to, progress);
        var left = Math.Abs(to - value) / quantum;
        var steps = Math.Ceiling(left - 0.5 - Slack);

        // Already showing the target: nothing is left to show, so nothing more to wake for.
        if (steps <= 0)
        {
            return double.PositiveInfinity;
        }

        var boundary = steps - 0.5;

        // Exactly on a boundary: the change is the next one.
        if (boundary >= left - Slack)
        {
            boundary -= 1;
        }

        if (boundary <= 0)
        {
            return 1;
        }

        var at = to - (Math.Sign(to - from) * boundary * quantum);
        return Math.Clamp((at - from) / (to - from), progress, 1);
    }
}
