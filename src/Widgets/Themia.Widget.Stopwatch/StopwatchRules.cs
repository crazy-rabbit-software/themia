using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Themia.Core.Widgets;

/// <summary>[S-4600] What a stopwatch is doing.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StopwatchPhase>))]
public enum StopwatchPhase
{
    /// <summary>Never started, or reset: nought and no laps.</summary>
    Idle = 0,
    Running = 1,
    Paused = 2,
}

/// <summary>
/// [S-4600]..[S-4603] A stopwatch as a value (after Karpo's <c>Clock\Stopwatch.cs</c>): what it banked,
/// since when it runs, and its laps. Nothing in it reads a clock; it is handed the time, and the time
/// it is handed is a <b>monotonic</b> one (the performance counter), never the wall clock, so setting
/// Windows' clock or changing the zone never moves a running stopwatch.
/// </summary>
/// <param name="Phase">What it is doing.</param>
/// <param name="Banked">What it had counted when it last stopped running.</param>
/// <param name="Since">When it last started running, on the monotonic clock.</param>
/// <param name="Laps">Where each lap ended, from the start, oldest first: totals, not lengths.</param>
public readonly record struct StopwatchState(StopwatchPhase Phase, TimeSpan Banked, TimeSpan Since, ImmutableArray<TimeSpan> Laps)
{
    /// <summary>The most laps it keeps; a lap past them is not taken.</summary>
    public const int MostLaps = 999;

    /// <summary>The most it counts to: past this it reads this, as its figures hold no more.</summary>
    public static readonly TimeSpan Longest = new TimeSpan(99, 59, 59) + TimeSpan.FromMilliseconds(900);

    public static StopwatchState None => new(StopwatchPhase.Idle, TimeSpan.Zero, TimeSpan.Zero, []);

    public bool Running => Phase == StopwatchPhase.Running;

    public bool Idle => Phase == StopwatchPhase.Idle;

    /// <summary>Its laps, never the default array a <c>default</c> state holds.</summary>
    public ImmutableArray<TimeSpan> Splits => Laps.IsDefault ? [] : Laps;

    /// <summary>What it has counted at <paramref name="now"/>: never below nought, never back in time, never past <see cref="Longest"/>.</summary>
    public TimeSpan Elapsed(TimeSpan now)
    {
        var counted = Banked + (Running && now > Since ? now - Since : TimeSpan.Zero);
        return counted > Longest ? Longest : counted < TimeSpan.Zero ? TimeSpan.Zero : counted;
    }

    /// <summary>Started from nought at <paramref name="now"/>.</summary>
    public static StopwatchState Start(TimeSpan now) => new(StopwatchPhase.Running, TimeSpan.Zero, now, []);

    public StopwatchState Pause(TimeSpan now) =>
        Running ? this with { Phase = StopwatchPhase.Paused, Banked = Elapsed(now), Since = TimeSpan.Zero } : this;

    public StopwatchState Resume(TimeSpan now) =>
        Phase == StopwatchPhase.Paused ? this with { Phase = StopwatchPhase.Running, Since = now } : this;

    /// <summary>[S-4601] The one start button: starts one that is idle, pauses one that runs, takes up one that is paused.</summary>
    public StopwatchState Act(TimeSpan now) => Phase switch
    {
        StopwatchPhase.Running => Pause(now),
        StopwatchPhase.Paused => Resume(now),
        _ => Start(now),
    };

    /// <summary>[S-4601] A lap ends at <paramref name="now"/>, while it runs: nothing otherwise, nor past <see cref="MostLaps"/>.</summary>
    public StopwatchState Lap(TimeSpan now) =>
        CanLap ? this with { Laps = Splits.Add(Elapsed(now)) } : this;

    /// <summary>Whether a lap can end now: the lap button is enabled by this.</summary>
    public bool CanLap => Running && Splits.Length < MostLaps;

    /// <summary>Whether it has anything to clear: the reset button is enabled by this.</summary>
    public bool CanReset => !Idle;

    /// <summary>
    /// Lap <paramref name="index"/> (from 0, oldest first): how long it was, and where it ended from the
    /// start. Null past the laps it has.
    /// </summary>
    public (TimeSpan Length, TimeSpan Total)? LapAt(int index)
    {
        var laps = Splits;
        if (index < 0 || index >= laps.Length)
        {
            return null;
        }

        var total = laps[index];
        return (total - (index > 0 ? laps[index - 1] : TimeSpan.Zero), total);
    }

    /// <summary>How long the lap under way has run at <paramref name="now"/>: since the last lap ended, or since the start.</summary>
    public TimeSpan CurrentLap(TimeSpan now)
    {
        var elapsed = Elapsed(now);
        var laps = Splits;
        var since = laps.Length > 0 ? elapsed - laps[^1] : elapsed;
        return since < TimeSpan.Zero ? TimeSpan.Zero : since;
    }

    public bool Equals(StopwatchState other) =>
        Phase == other.Phase && Banked == other.Banked && Since == other.Since && Splits.SequenceEqual(other.Splits);

    public override int GetHashCode() => HashCode.Combine(Phase, Banked, Since, Splits.Length);
}

/// <summary>
/// [S-4602] What a stopwatch writes into its widget's config so it outlives a restart: its phase, what
/// it banked, the laps, and — while it runs — the wall-clock instant its run started. The monotonic
/// clock does not survive a restart (it starts again at boot), so the instant is the one thing that
/// can; it is written <i>from</i> the monotonic clock (now less the monotonic time the run has
/// lasted), so a clock change during the session never reaches the file, and read back clamped, so a
/// clock set back while Themia was closed reads as no time passed rather than as negative time.
/// </summary>
/// <param name="Phase">What it was doing.</param>
/// <param name="BankedMs">What it had counted before its current run, in milliseconds.</param>
/// <param name="StartedAt">While running, the instant (UTC) the current run started; null otherwise.</param>
/// <param name="LapsMs">The laps' totals, oldest first, in milliseconds.</param>
public readonly record struct StopwatchRecord(StopwatchPhase Phase, long BankedMs, DateTimeOffset? StartedAt, IReadOnlyList<long> LapsMs)
{
    /// <summary>The state the record says, at <paramref name="wallNow"/> and <paramref name="monoNow"/> of this session.</summary>
    public StopwatchState ToState(DateTimeOffset wallNow, TimeSpan monoNow)
    {
        var banked = Clamp(TimeSpan.FromMilliseconds(Math.Max(0, BankedMs)));
        var laps = (LapsMs ?? [])
            .Select(ms => Clamp(TimeSpan.FromMilliseconds(Math.Max(0, ms))))
            .Take(StopwatchState.MostLaps)
            .ToImmutableArray();

        // Laps are totals, so they rise; a hand-edited file with one out of order loses the rest.
        var kept = ImmutableArray.CreateBuilder<TimeSpan>(laps.Length);
        foreach (var lap in laps)
        {
            if (kept.Count > 0 && lap < kept[^1])
            {
                break;
            }

            kept.Add(lap);
        }

        switch (Phase)
        {
            case StopwatchPhase.Running when StartedAt is { } started:
                // A clock set back since: no time passed, never negative time.
                var ran = wallNow - started;
                ran = ran < TimeSpan.Zero ? TimeSpan.Zero : Clamp(ran);
                return new StopwatchState(StopwatchPhase.Running, banked, monoNow - ran, kept.ToImmutable());

            case StopwatchPhase.Running:
                // Running with no start is a damaged file: what it banked stands, paused.
                return new StopwatchState(StopwatchPhase.Paused, banked, TimeSpan.Zero, kept.ToImmutable());

            case StopwatchPhase.Paused:
                return new StopwatchState(StopwatchPhase.Paused, banked, TimeSpan.Zero, kept.ToImmutable());

            default:
                return StopwatchState.None;
        }
    }

    /// <summary>The record of <paramref name="state"/>, written at <paramref name="wallNow"/> / <paramref name="monoNow"/>.</summary>
    public static StopwatchRecord Of(StopwatchState state, DateTimeOffset wallNow, TimeSpan monoNow)
    {
        var laps = state.Splits.Select(lap => (long)lap.TotalMilliseconds).ToList();

        return state.Phase switch
        {
            StopwatchPhase.Running => new StopwatchRecord(
                StopwatchPhase.Running,
                (long)state.Banked.TotalMilliseconds,
                wallNow - (monoNow > state.Since ? monoNow - state.Since : TimeSpan.Zero),
                laps),
            StopwatchPhase.Paused => new StopwatchRecord(StopwatchPhase.Paused, (long)state.Banked.TotalMilliseconds, null, laps),
            _ => new StopwatchRecord(StopwatchPhase.Idle, 0, null, []),
        };
    }

    private static TimeSpan Clamp(TimeSpan value) => value > StopwatchState.Longest ? StopwatchState.Longest : value;
}

/// <summary>[S-4603] How a stopwatch's time is written.</summary>
public static class StopwatchText
{
    /// <summary>
    /// The widest reading, for the room kept for the time: hours, minutes, seconds and the tenth, every
    /// figure an 8 (tabular figures are all one width), so nothing moves as it counts.
    /// </summary>
    public const string Widest = "88:88:88.8";

    /// <summary>
    /// "0:00", "12:34", "1:02:03": minutes and seconds, with the hours in front from an hour up — whole
    /// seconds, begun not rounded. With <paramref name="tenths"/>, the tenth after
    /// <paramref name="separator"/> (the language's decimal sign): "12:34.5".
    /// </summary>
    public static string Time(TimeSpan elapsed, bool tenths, char separator = '.')
    {
        var whole = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed > StopwatchState.Longest ? StopwatchState.Longest : elapsed;
        var culture = CultureInfo.InvariantCulture;
        var text = whole >= TimeSpan.FromHours(1)
            ? string.Create(culture, $"{(int)whole.TotalHours}:{whole.Minutes:00}:{whole.Seconds:00}")
            : string.Create(culture, $"{whole.Minutes}:{whole.Seconds:00}");

        if (!tenths)
        {
            return text;
        }

        var tenth = (int)(whole.Ticks / (TimeSpan.TicksPerSecond / 10) % 10);
        return string.Create(culture, $"{text}{separator}{tenth}");
    }
}
