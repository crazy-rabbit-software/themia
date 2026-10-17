namespace Themia.Core.Time;

/// <summary>
/// When a shared clock timer should next wake. Pure, so the drift rules are testable without a
/// dispatcher.
/// </summary>
public static class TickSchedule
{
    /// <summary>
    /// Waking exactly on a boundary rounds down roughly half the time, because timers fire a hair
    /// early and the formatter then renders the *previous* second for a whole tick. Landing just
    /// after the boundary is always correct, so every delay carries this bias.
    /// </summary>
    public static readonly TimeSpan Bias = TimeSpan.FromMilliseconds(15);

    /// <summary>
    /// Time from <paramref name="now"/> until just after the next whole <paramref name="interval"/>.
    /// Re-computed on every tick rather than using a repeating timer, so a late wake does not push
    /// the whole schedule permanently off the second.
    /// </summary>
    public static TimeSpan DelayToNextBoundary(DateTimeOffset now, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            return Bias;
        }

        var ticksIntoPeriod = now.Ticks % interval.Ticks;
        var remaining = TimeSpan.FromTicks(interval.Ticks - ticksIntoPeriod);

        if (remaining < Bias)
        {
            remaining += interval;
        }

        return remaining + Bias;
    }

    /// <summary>
    /// The period index <paramref name="now"/> falls in. A subscriber fires when its index changes,
    /// which is how a once-a-minute widget shares a one-second timer without firing every second.
    /// </summary>
    public static long PeriodIndex(DateTimeOffset now, TimeSpan interval)
        => interval <= TimeSpan.Zero ? now.Ticks : now.Ticks / interval.Ticks;

    /// <summary>
    /// [S-1825] The cadences the shared timer supports: whole seconds under a minute (at least one),
    /// and once a minute for anything slower. Every request under a minute used to collapse to one
    /// second, so a meter asked to read every 5 s read every second and its chart spanned a fifth
    /// of its span; <see cref="PeriodIndex"/> already fires a 5 s subscriber once per 5 s period.
    /// </summary>
    public static TimeSpan Normalise(TimeSpan interval)
        => interval >= TimeSpan.FromMinutes(1)
            ? TimeSpan.FromMinutes(1)
            : TimeSpan.FromSeconds(Math.Max(1, Math.Round(interval.TotalSeconds)));
}
