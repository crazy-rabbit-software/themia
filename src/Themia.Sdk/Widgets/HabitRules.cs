namespace Themia.Core.Widgets;

/// <summary>One dot of a habit's history strip: a period, and whether the habit was done in it.</summary>
public readonly record struct HabitDot(HabitBucket Bucket, bool Done);

/// <summary>
/// [S-1421]..[S-1426] What a habit's log means under a cadence: whether the current period is done,
/// how long the run is, the best run, and what the last few periods looked like.
/// <para>
/// Every function takes the period to reckon from (or the moment and zone that give it) as an
/// argument; none reads the clock, so each is testable at any date and across a DST change.
/// </para>
/// <para>
/// The log is a set of period keys (see <see cref="HabitPeriods"/>), untrusted on every read: it
/// comes from a file anybody can edit. Nothing here trusts a key it did not derive. The streak and
/// the dots walk <i>back</i> from the current period and only ask "is this key in the log", so a key
/// in the future, a malformed key or a key of another cadence is never counted — it simply never
/// comes up. Duplicates collapse in the set.
/// </para>
/// </summary>
public static class HabitRules
{
    /// <summary>
    /// The most dots a row asks for, and the ceiling the config clamps to: a month of days. Past that
    /// the number is only the bound of a render loop a hand-edited file would get to choose.
    /// </summary>
    public const int MaxHistory = 31;

    /// <summary>The window the summary's completion rate looks back over, in periods.</summary>
    public const int RateWindow = 7;

    /// <summary>
    /// The log as a set. Built once per render and handed to the functions below, rather than each
    /// re-reading the same list per row per tick.
    /// </summary>
    public static HashSet<string> Log(IEnumerable<string?>? log)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        if (log is null)
        {
            return set;
        }

        foreach (var key in log)
        {
            if (!string.IsNullOrEmpty(key))
            {
                set.Add(key);
            }
        }

        return set;
    }

    /// <summary>[S-1421] Whether the habit is ticked in <paramref name="bucket"/>.</summary>
    public static bool IsDone(IReadOnlySet<string> log, HabitBucket bucket) => log.Contains(bucket.Key);

    /// <summary>
    /// [S-1423] The current run of completed periods ending at <paramref name="current"/>.
    /// <para>
    /// The current period not being done does <b>not</b> break the run — it is not over yet. So the
    /// count starts at the current period when it is done and at the one before when it is not; a gap
    /// anywhere before that ends it. Bounded by the log: every step consumes a distinct key that is in
    /// it, so the walk cannot outrun the log however far back it goes.
    /// </para>
    /// </summary>
    public static int Streak(IReadOnlySet<string> log, HabitBucket current, HabitCadence cadence, TimeZoneInfo zone)
    {
        if (log.Count == 0)
        {
            return 0;
        }

        var cursor = log.Contains(current.Key) ? current : HabitPeriods.Previous(current, cadence, zone);
        var streak = 0;

        while (streak < log.Count && log.Contains(cursor.Key))
        {
            streak++;
            cursor = HabitPeriods.Previous(cursor, cadence, zone);
        }

        return streak;
    }

    /// <summary>
    /// [S-1426] The last <paramref name="count"/> periods, oldest first, with <paramref name="current"/>
    /// last. Walks the same ladder as the streak, so the dots and the number beside them can never
    /// disagree about which period is which.
    /// </summary>
    public static IReadOnlyList<HabitDot> Recent(IReadOnlySet<string> log, HabitBucket current, HabitCadence cadence, TimeZoneInfo zone, int count)
    {
        var wanted = Math.Clamp(count, 0, MaxHistory);
        var dots = new HabitDot[wanted];
        var cursor = current;

        for (var i = wanted - 1; i >= 0; i--)
        {
            dots[i] = new HabitDot(cursor, log.Contains(cursor.Key));
            cursor = HabitPeriods.Previous(cursor, cadence, zone);
        }

        return dots;
    }

    /// <summary>
    /// [S-1424] The longest run in the whole log under this cadence. Only keys of this cadence that
    /// are not in the future count; two are consecutive when one is the other's
    /// <see cref="HabitPeriods.Previous"/>, the same step the streak takes.
    /// </summary>
    public static int BestStreak(IReadOnlySet<string> log, HabitBucket current, HabitCadence cadence, TimeZoneInfo zone)
    {
        var buckets = new List<HabitBucket>(log.Count);

        foreach (var key in log)
        {
            if (HabitPeriods.TryParse(key, cadence, zone, out var bucket) && bucket.Start <= current.Start)
            {
                buckets.Add(bucket);
            }
        }

        if (buckets.Count == 0)
        {
            return 0;
        }

        buckets.Sort((a, b) => a.Start.CompareTo(b.Start));

        var best = 1;
        var run = 1;

        for (var i = 1; i < buckets.Count; i++)
        {
            run = HabitPeriods.Previous(buckets[i], cadence, zone).Key == buckets[i - 1].Key ? run + 1 : 1;
            best = Math.Max(best, run);
        }

        return best;
    }

    /// <summary>[S-1427] How many of the last <paramref name="window"/> periods, the current one included, are done.</summary>
    public static int DoneIn(IReadOnlySet<string> log, HabitBucket current, HabitCadence cadence, TimeZoneInfo zone, int window)
        => Recent(log, current, cadence, zone, window).Count(d => d.Done);

    /// <summary>
    /// [S-1421] Flip <paramref name="key"/> in a log and return the new log. Un-ticking removes
    /// <i>every</i> copy, so a log that somehow holds a key twice does not take two taps to clear.
    /// </summary>
    public static IReadOnlyList<string> Toggle(IReadOnlyList<string> log, string key)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var without = log.Where(k => k != key).ToList();
        return without.Count != log.Count ? without : [.. log, key];
    }
}
