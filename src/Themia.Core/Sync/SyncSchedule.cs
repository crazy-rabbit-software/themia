using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Sync;

/// <summary>
/// [S-2609] When one synced thing (a mail folder, a calendar, an address book, a task account, a feed)
/// is next asked for, and how the last attempt went.
/// <para>
/// The four triggers are the four ways to move <see cref="NextAttempt"/>: <see cref="Seed"/> at start
/// (due at once only when the shelf is older than the interval, so a restart a minute after the
/// last sync asks nothing), <see cref="Succeeded"/> / <see cref="Failed"/> on the interval and the
/// back-off, <see cref="Wake"/> when the network comes back, <see cref="Force"/> when the user asks.
/// Showing a widget is not a trigger: it reads what is held. Not thread-safe; the owning service
/// keeps it on one thread.
/// </para>
/// </summary>
public sealed class SyncSchedule(TimeSpan interval)
{
    /// <summary>How often a success is followed by the next ask.</summary>
    public TimeSpan Interval { get; set; } = interval;

    /// <summary>When the data held was last fetched whole, or null when it never was.</summary>
    public DateTimeOffset? LastSuccess { get; private set; }

    /// <summary>How the last attempt went; <see cref="FetchStatus.Loading"/> before the first.</summary>
    public FetchStatus Status { get; private set; } = FetchStatus.Loading;

    /// <summary>The last failure in one line, for the log. Never rendered verbatim.</summary>
    public string? Detail { get; private set; }

    public int Failures { get; private set; }

    public DateTimeOffset NextAttempt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>An attempt is out. Nothing starts a second one beside it.</summary>
    public bool Busy { get; set; }

    public bool IsDue(DateTimeOffset now) => !Busy && now >= NextAttempt;

    /// <summary>
    /// Start from a shelf's timestamp. Due once the interval since <paramref name="lastSuccess"/> is
    /// over; a stamp from the future (a clock moved back) is due now rather than in a year.
    /// </summary>
    public void Seed(DateTimeOffset? lastSuccess, DateTimeOffset now)
    {
        LastSuccess = lastSuccess;

        if (lastSuccess is { } at && at <= now)
        {
            Status = FetchStatus.Ok;
            NextAttempt = at + Interval;
        }
        else
        {
            NextAttempt = DateTimeOffset.MinValue;
        }
    }

    public void Succeeded(DateTimeOffset now)
    {
        LastSuccess = now;
        Status = FetchStatus.Ok;
        Detail = null;
        Failures = 0;
        NextAttempt = now + Interval;
    }

    public void Failed(FetchStatus status, string? detail, DateTimeOffset now)
    {
        Status = status;
        Detail = detail;
        Failures++;
        NextAttempt = FetchState.IsRetryable(status)
            ? now + Backoff.Delay(Interval, Failures, status)
            : now + Backoff.Ceiling;
    }

    /// <summary>
    /// Wait at least <paramref name="hold"/> before the next try: a refusal only the user can mend is
    /// not asked again every few minutes ([S-588]).
    /// </summary>
    public void Hold(TimeSpan hold, DateTimeOffset now)
    {
        if (now + hold > NextAttempt)
        {
            NextAttempt = now + hold;
        }
    }

    /// <summary>
    /// The network came back: a wait that was only about the network ends now. A rate limit or a
    /// refusal keeps its wait: the network was never the problem.
    /// </summary>
    public void Wake()
    {
        if (Status is FetchStatus.Offline or FetchStatus.Unreachable or FetchStatus.TimedOut)
        {
            NextAttempt = DateTimeOffset.MinValue;
        }
    }

    /// <summary>The user asked. Everything but a provider's own "slow down" goes now.</summary>
    public void Force(DateTimeOffset now)
    {
        if (Status != FetchStatus.RateLimited || now >= NextAttempt)
        {
            NextAttempt = DateTimeOffset.MinValue;
        }
    }
}

/// <summary>[S-2612] How a widget says the data on screen is a stored copy.</summary>
public static class SyncFreshness
{
    /// <summary>"Updated 10:42" today, "Updated Mon 10:42" this week, "Updated Oct 4" before.</summary>
    public static string Updated(Locale locale, DateTimeOffset at, DateTimeOffset now)
        => Strings.Format(locale, Str.Sync_Updated, ("time", When(locale, at, now)));

    /// <summary>
    /// The header's line for a stored copy the last sync could not improve on: "No network · updated
    /// 10:42". Null while the last sync worked (nothing to flag) or when nothing was ever stored.
    /// </summary>
    public static string? Line(Locale locale, FetchStatus status, DateTimeOffset? at, DateTimeOffset now)
    {
        if (at is not { } stamp || !FetchState.IsFailure(status) || status == FetchStatus.Empty)
        {
            return null;
        }

        return Strings.Format(locale, Str.Sync_StateUpdated, ("state", FetchState.Message(locale, status)), ("time", When(locale, stamp, now)));
    }

    /// <summary>
    /// A problem sentence over a stored copy, with when that copy is from: "No network · updated
    /// 10:42". The sentence alone when nothing is stored.
    /// </summary>
    public static string Stamp(Locale locale, string problem, DateTimeOffset? at, DateTimeOffset now)
        => at is { } stamp ? Strings.Format(locale, Str.Sync_StateUpdated, ("state", problem), ("time", When(locale, stamp, now))) : problem;

    /// <summary>The time part alone: a clock time today, weekday and time within six days, else a short date.</summary>
    public static string When(Locale locale, DateTimeOffset at, DateTimeOffset now)
    {
        var local = at.ToOffset(now.Offset);
        var today = DateOnly.FromDateTime(now.DateTime);
        var day = DateOnly.FromDateTime(local.DateTime);
        var time = LocaleFormat.Time(locale, local.DateTime);

        if (day == today || day > today)
        {
            return time;
        }

        if (today.DayNumber - day.DayNumber < 7)
        {
            return $"{LocaleFormat.WeekdayName(locale, local.DayOfWeek, NameWidth.Short)} {time}";
        }

        return LocaleFormat.Date(locale, day, DateStyle.MonthDayShort);
    }
}
