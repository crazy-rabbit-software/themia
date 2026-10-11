using Themia.Core.Time;

namespace Themia.Core.SystemData;

/// <summary>
/// [S-2503] When the hub wakes and which feeds a wake is for. Pure, so the arithmetic is tested
/// without a dispatcher.
/// </summary>
public static class HubCadence
{
    /// <summary>The shared ticker never runs slower than this (<see cref="TickSchedule.Normalise"/>).</summary>
    public const int TickerCeilingSeconds = 60;

    /// <summary>A feed's cadence in whole seconds, at least one. Unlike the ticker's, not capped at a minute: a ping every 5 min stays 5 min.</summary>
    public static int Seconds(TimeSpan interval)
        => double.IsFinite(interval.TotalSeconds)
            ? (int)Math.Clamp(Math.Round(interval.TotalSeconds, MidpointRounding.AwayFromZero), 1, int.MaxValue)
            : 1;

    /// <summary>
    /// The slowest wake that lands on every feed's boundaries: the greatest common divisor of the
    /// intervals. A cadence over the ticker's ceiling joins the ceiling into it, because the ticker
    /// wakes on whole minutes at the slowest — a 90 s feed alone needs a 30 s wake to land on 90, 180.
    /// Null when there is nothing to read.
    /// </summary>
    public static TimeSpan? Arbitrate(IEnumerable<int> intervalSeconds)
    {
        ArgumentNullException.ThrowIfNull(intervalSeconds);

        var gcd = 0;
        var any = false;

        foreach (var raw in intervalSeconds)
        {
            var seconds = Math.Max(1, raw);
            any = true;
            gcd = Gcd(gcd, seconds);

            if (seconds > TickerCeilingSeconds)
            {
                gcd = Gcd(gcd, TickerCeilingSeconds);
            }
        }

        return any ? TimeSpan.FromSeconds(gcd) : null;
    }

    /// <summary>
    /// Whether a feed last read in period <paramref name="lastPeriod"/> is due at
    /// <paramref name="now"/>: its own period index moved on ([S-26], per feed rather than per
    /// ticker subscription). <paramref name="period"/> is the index to remember once it has been read.
    /// A clock that went backwards (a correction) is due too, so a feed never waits out the jump.
    /// </summary>
    public static bool IsDue(long lastPeriod, DateTimeOffset now, int intervalSeconds, out long period)
    {
        period = TickSchedule.PeriodIndex(now, TimeSpan.FromSeconds(Math.Max(1, intervalSeconds)));
        return period != lastPeriod;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }
}
