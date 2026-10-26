using Themia.Core.Time;

namespace Themia.Core.SystemData;

/// <summary>
/// [S-2503] When the hub reads. One app-wide cadence, not a setting and not a widget's: every system
/// figure on the desktop (the meters, the GPU tile, the details, the process list) moves on the same
/// second, so a CPU meter and a memory meter side by side never disagree about when "now" was. Pure,
/// so the arithmetic is tested without a dispatcher.
/// </summary>
public static class SystemCadence
{
    /// <summary>The hub's one ticker subscription: every source is read once a second.</summary>
    public const int TickSeconds = 1;

    /// <summary>
    /// [S-2512] A ping is a packet to somebody else's host, so a prober asks every fifth tick: on the
    /// same second boundaries as everything else, five times slower. Its history spacing is this too.
    /// </summary>
    public const int PingSeconds = 5;

    /// <summary>
    /// [S-2514] The battery pack (the driver's rate, voltage, temperature and capacity) is asked every
    /// fifth tick too: each ask is a round of IOCTLs the firmware answers from its embedded
    /// controller, and what moves between Windows' own power events (each of which re-reads it at
    /// once, [S-2509]) is the rate, which a details tile reads, not charts.
    /// </summary>
    public const int BatteryPackSeconds = 5;

    /// <summary>The hub's wake, as a span.</summary>
    public static TimeSpan Tick { get; } = TimeSpan.FromSeconds(TickSeconds);

    /// <summary>[S-2503] [S-2512] [S-2514] How many seconds apart a source is read (and its history points lie).</summary>
    public static int PeriodSeconds(SystemSource source) => source switch
    {
        SystemSource.Ping => PingSeconds,
        SystemSource.BatteryPack => BatteryPackSeconds,
        _ => TickSeconds,
    };

    /// <summary>
    /// Whether a source last read in period <paramref name="lastPeriod"/> is due at
    /// <paramref name="now"/>: its own period index moved on ([S-26]). <paramref name="period"/> is the
    /// index to remember once it has been read. A clock that went backwards (a correction) is due
    /// too, so a source never waits out the jump.
    /// </summary>
    public static bool IsDue(long lastPeriod, DateTimeOffset now, SystemSource source, out long period)
    {
        period = TickSchedule.PeriodIndex(now, TimeSpan.FromSeconds(PeriodSeconds(source)));
        return period != lastPeriod;
    }
}
