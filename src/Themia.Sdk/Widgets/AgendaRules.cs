namespace Themia.Core.Widgets;

/// <summary>
/// The two rules a glanceable agenda turns on, kept apart from the view that draws it so they can be
/// tested against a fixed clock instead of against whatever today happens to be.
/// </summary>
public static class AgendaRules
{
    /// <summary>
    /// Which day's heading a row belongs under: its own, or today for one that began earlier and has
    /// not ended.
    /// <para>
    /// A meeting that started yesterday and is still running belongs at the top of today, not under
    /// a heading that has already gone by — where the reader would never look for it.
    /// </para>
    /// </summary>
    public static DateOnly FiledUnder(DateOnly day, DateOnly today) => day < today ? today : day;

    /// <summary>
    /// Whether an event is happening right now — the one thing a glanceable calendar must not lose.
    /// <para>
    /// An all-day entry is never "now": it is a label on a date, not a span of the clock, so treating
    /// it as in progress would mark a whole day's worth of them as under way from midnight.
    /// An entry with a start but no end counts as running from its start, because an event with no
    /// stated end has not been stated to have finished.
    /// </para>
    /// </summary>
    public static bool IsInProgress(bool allDay, DateTimeOffset? startsAt, DateTimeOffset? endsAt, DateTimeOffset now)
        => !allDay
            && startsAt is { } start
            && start <= now
            && (endsAt is not { } end || end > now);
}
