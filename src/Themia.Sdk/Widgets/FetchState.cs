using Themia.Core.Diagnostics;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// How the last attempt to reach a service ended.
/// <para>
/// Every one of these is a <b>named</b> state rather than a shared "failed", because they need
/// different things from the reader and from the widget. Being offline is a normal state of a
/// laptop and is not the widget's fault; a 429 means stop asking; a malformed reply means the
/// service answered and the answer was not usable, which is a different problem from not answering.
/// A single error state would collapse all of those into one sentence that says nothing, which is
/// the failure mode the original app filed a bug about.
/// </para>
/// </summary>
public enum FetchStatus
{
    /// <summary>The service answered and the answer parsed.</summary>
    Ok = 0,

    /// <summary>Nothing has been asked yet: no location, no symbol, no feed address.</summary>
    NotConfigured,

    /// <summary>A first request is out and there is nothing cached to show meanwhile.</summary>
    Loading,

    /// <summary>This machine has no network at all. Not an error; a state.</summary>
    Offline,

    /// <summary>There is a network, but the host did not answer — DNS, routing, a refused connection.</summary>
    Unreachable,

    /// <summary>The request ran out of time.</summary>
    TimedOut,

    /// <summary>HTTP 429. The one status that must never be retried on the normal cadence.</summary>
    RateLimited,

    /// <summary>The service answered with an error status, or with something that did not parse.</summary>
    BadResponse,

    /// <summary>The service answered correctly and there was nothing in it.</summary>
    Empty,

    /// <summary>The configured address or symbol was refused before any request was made.</summary>
    Refused,
}

/// <summary>
/// What a widget says about the network, and how hard it tries again after it goes wrong.
/// <para>
/// Pure, and in Core, because these are the two decisions in a network widget that are easy to get
/// subtly wrong and impossible to check by looking at the screen: the exact words for each state,
/// and the delay before the next attempt.
/// </para>
/// </summary>
public static class FetchState
{
    /// <summary>The line shown in place of data. One sentence, naming the actual state.</summary>
    public static string Message(FetchStatus status) => Message(Strings.Current, status);

    /// <summary><see cref="Message(FetchStatus)"/> in <paramref name="locale"/>.</summary>
    public static string Message(Locale locale, FetchStatus status) => status switch
    {
        FetchStatus.Ok => string.Empty,
        FetchStatus.NotConfigured => Strings.Get(locale, Str.Shell_Fetch_NotConfigured),
        FetchStatus.Loading => Strings.Get(locale, Str.Shell_Fetch_Loading),
        FetchStatus.Offline => Strings.Get(locale, Str.Shell_Fetch_Offline),
        FetchStatus.Unreachable => Strings.Get(locale, Str.Shell_Fetch_Unreachable),
        FetchStatus.TimedOut => Strings.Get(locale, Str.Shell_Fetch_TimedOut),
        FetchStatus.RateLimited => Strings.Get(locale, Str.Shell_Fetch_RateLimited),
        FetchStatus.BadResponse => Strings.Get(locale, Str.Shell_Fetch_BadResponse),
        FetchStatus.Empty => Strings.Get(locale, Str.Shell_Fetch_Empty),
        FetchStatus.Refused => Strings.Get(locale, Str.Shell_Fetch_Refused),
        _ => Strings.Get(locale, Str.Shell_Fetch_Unreachable),
    };

    /// <summary>
    /// Whether a status is worth trying again on a timer at all. A refused setting and an
    /// unconfigured widget are not: nothing about them changes until the user edits something, and
    /// retrying is pure noise in the log and on the wire.
    /// </summary>
    public static bool IsRetryable(FetchStatus status) => status switch
    {
        FetchStatus.NotConfigured => false,
        FetchStatus.Refused => false,
        _ => true,
    };

    /// <summary>
    /// [S-1886] How loud a change to <paramref name="status"/> is in the log. "Not set up yet" is the
    /// ordinary state of a widget nobody has configured (it shows the Set up button, [S-1862]), not a
    /// fault: a debug line, never a warning. Answering is news worth an info line; every real failure
    /// is a warning.
    /// </summary>
    public static LogLevel LogLevelFor(FetchStatus status) => status switch
    {
        FetchStatus.Ok => LogLevel.Info,
        FetchStatus.NotConfigured or FetchStatus.Loading => LogLevel.Debug,
        _ => LogLevel.Warn,
    };

    /// <summary>Whether the status is one that should replace the data on screen rather than annotate it.</summary>
    public static bool IsFailure(FetchStatus status)
        => status is not (FetchStatus.Ok or FetchStatus.Loading or FetchStatus.NotConfigured);

    /// <summary>
    /// How old the data on screen is, said out loud.
    /// <para>
    /// This is the line that matters most in a network widget. Everything else it shows is a number
    /// that looks exactly as authoritative when it is four days old as when it is four seconds old,
    /// and stale data presented as live is the way a weather tile actively misleads somebody.
    /// </para>
    /// </summary>
    public static string Freshness(DateTimeOffset fetched, DateTimeOffset now) => Freshness(Strings.Current, fetched, now);

    /// <summary>
    /// <see cref="Freshness(DateTimeOffset, DateTimeOffset)"/> in <paramref name="locale"/>: "Just now"
    /// under a minute, then the language's short relative time ("5 min. ago", "vor 3 Std.").
    /// </summary>
    public static string Freshness(Locale locale, DateTimeOffset fetched, DateTimeOffset now)
    {
        var age = now - fetched;

        // A provider clock running ahead of this machine's is not the future.
        if (age < TimeSpan.FromMinutes(1))
        {
            return Strings.Get(locale, Str.Shell_Time_JustNow);
        }

        return LocaleFormat.Relative(locale, -age, RelativeStyle.Short);
    }

    /// <summary>
    /// Whether what is on screen is old enough to be worth flagging: past three refresh intervals,
    /// and never under five minutes, so a one-minute widget does not cry stale over one missed tick.
    /// </summary>
    public static bool IsStale(DateTimeOffset fetched, DateTimeOffset now, TimeSpan refreshInterval)
    {
        var allowance = refreshInterval > TimeSpan.Zero ? refreshInterval * 3 : TimeSpan.FromMinutes(15);

        if (allowance < TimeSpan.FromMinutes(5))
        {
            allowance = TimeSpan.FromMinutes(5);
        }

        return now - fetched > allowance;
    }
}

/// <summary>
/// How long to wait before asking again after a failure.
/// <para>
/// The rule is never to hammer. A widget on a desktop runs for weeks; a host that is refusing,
/// rate-limiting or simply down will keep doing so for minutes or hours, and a retry every tick
/// turns one broken widget into a steady stream of requests at somebody else's free, unauthenticated
/// API. Doubling, with a floor and a ceiling, means a blip costs one extra tick and an outage costs
/// nothing at all.
/// </para>
/// </summary>
public static class Backoff
{
    /// <summary>Never faster than this, whatever the widget's configured cadence says.</summary>
    public static readonly TimeSpan Floor = TimeSpan.FromSeconds(30);

    /// <summary>However long an outage lasts, check again at least this often.</summary>
    public static readonly TimeSpan Ceiling = TimeSpan.FromHours(1);

    /// <summary>
    /// A 429 is the service asking, in so many words, to be left alone. It gets its own floor and
    /// its own much higher ceiling, because backing off to the normal cadence is how a free public
    /// API ends up blocking the app rather than one request.
    /// </summary>
    public static readonly TimeSpan RateLimitFloor = TimeSpan.FromMinutes(15);

    /// <summary>The ceiling for a rate-limited service.</summary>
    public static readonly TimeSpan RateLimitCeiling = TimeSpan.FromHours(6);

    /// <summary>Past this many failures the delay stops growing; it is already at the ceiling.</summary>
    private const int MaxDoublings = 8;

    /// <summary>
    /// The wait after <paramref name="consecutiveFailures"/> failures in a row.
    /// </summary>
    /// <param name="interval">The widget's own refresh cadence.</param>
    /// <param name="consecutiveFailures">Zero after a success, so the answer is the plain cadence.</param>
    /// <param name="status">What went wrong; a 429 backs off on its own, much longer, schedule.</param>
    public static TimeSpan Delay(TimeSpan interval, int consecutiveFailures, FetchStatus status)
    {
        var rateLimited = status == FetchStatus.RateLimited;
        var floor = rateLimited ? RateLimitFloor : Floor;
        var ceiling = rateLimited ? RateLimitCeiling : Ceiling;

        var baseline = interval > floor ? interval : floor;

        if (baseline > ceiling)
        {
            baseline = ceiling;
        }

        if (consecutiveFailures <= 0)
        {
            // A rate limit is a failure even on the first one: the caller has not "succeeded once",
            // so this branch is only reached after a genuine success, where the plain cadence is right.
            return interval > TimeSpan.Zero ? interval : baseline;
        }

        // The first failure costs nothing extra — a dropped packet should not double the cadence —
        // and each one after that doubles, to the ceiling.
        var doublings = Math.Min(consecutiveFailures - 1, MaxDoublings);
        var ticks = baseline.Ticks * (1L << doublings);

        var delay = ticks <= 0 || ticks > ceiling.Ticks ? ceiling : TimeSpan.FromTicks(ticks);
        return delay;
    }
}
