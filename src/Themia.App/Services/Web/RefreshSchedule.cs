using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// When a network widget is next allowed to ask, and when it is worth writing a line about it.
/// <para>
/// Both halves exist because a widget ticks forever. Without the first, a host that is down is
/// asked again every tick for as long as it stays down, which is a request per minute per widget at
/// somebody else's free API — and for a 429 it is the precise behaviour the status code exists to
/// stop. Without the second, the log fills with the same sentence every minute until the outage
/// ends and the one line that mattered, the first, is thousands of lines back.
/// </para>
/// <para>
/// The delay itself is <see cref="Backoff"/>'s, in Core, where it is tested. This only holds the
/// counter and the clock reading.
/// </para>
/// </summary>
internal sealed class RefreshSchedule(string scope, string subject)
{
    private int _failures;
    private DateTimeOffset _nextAttempt = DateTimeOffset.MinValue;
    private FetchStatus _reported = FetchStatus.Loading;
    private bool _everReported;

    /// <summary>The last status recorded. What the widget is currently showing about the network.</summary>
    public FetchStatus Status => _reported;

    /// <summary>Whether a tick at <paramref name="now"/> may fetch, or is still inside a back-off.</summary>
    public bool MayFetch(DateTimeOffset now) => now >= _nextAttempt;

    /// <summary>How long is left of the current back-off, for a widget that wants to say so.</summary>
    public TimeSpan Remaining(DateTimeOffset now)
        => _nextAttempt > now ? _nextAttempt - now : TimeSpan.Zero;

    /// <summary>
    /// Record how an attempt went, schedule the next one, and log the <i>transition</i>.
    /// </summary>
    /// <param name="status">How the attempt ended.</param>
    /// <param name="interval">The widget's configured cadence.</param>
    /// <param name="detail">One line of diagnostic, logged on a change and never rendered.</param>
    /// <param name="now">The clock reading for this attempt.</param>
    public void Record(FetchStatus status, TimeSpan interval, string? detail, DateTimeOffset now)
    {
        if (status is FetchStatus.Ok or FetchStatus.Empty)
        {
            // Empty counts as a success for scheduling: the service answered, correctly, and there
            // was nothing in it. Backing off from a quiet feed would be backing off from working.
            _failures = 0;
            _nextAttempt = now;
        }
        else if (!FetchState.IsRetryable(status))
        {
            // Nothing about a refused address or an unconfigured widget changes on a timer. The
            // next attempt comes from the user editing something, which resets this outright.
            _failures = 0;
            _nextAttempt = DateTimeOffset.MaxValue;
        }
        else
        {
            _failures++;
            _nextAttempt = now + Backoff.Delay(interval, _failures, status);
        }

        Note(status, detail, now);
    }

    /// <summary>
    /// Clear the back-off because the user changed something. A settings edit is an explicit "try
    /// again now", and making somebody wait out a six-hour rate-limit back-off after correcting a
    /// typo would read as a broken widget.
    /// </summary>
    public void Reset()
    {
        _failures = 0;
        _nextAttempt = DateTimeOffset.MinValue;
        _reported = FetchStatus.Loading;
        _everReported = false;
    }

    private void Note(FetchStatus status, string? detail, DateTimeOffset now)
    {
        if (_everReported && status == _reported)
        {
            return;
        }

        var previous = _reported;
        _reported = status;
        _everReported = true;

        if (status == FetchStatus.Ok)
        {
            if (previous != FetchStatus.Loading)
            {
                Log.Info(scope, $"{subject}: answering again");
            }

            return;
        }

        // Only for a state a timer can fix. A refused address is scheduled at DateTimeOffset.MaxValue
        // and would otherwise report a wait of half a billion minutes.
        var wait = Remaining(now);
        var suffix = FetchState.IsRetryable(status) && wait > TimeSpan.Zero
            ? $"; next attempt in {(int)Math.Ceiling(wait.TotalMinutes)}m"
            : string.Empty;

        // [S-1886] An unconfigured widget is a state, not a fault: never a warning.
        var line = $"{subject}: {FetchState.Message(status)}{Describe(detail)}{suffix}";
        if (FetchState.LogLevelFor(status) == LogLevel.Warn)
        {
            Log.Warn(scope, line);
        }
        else
        {
            Log.Debug(scope, line);
        }
    }

    private static string Describe(string? detail)
        => string.IsNullOrWhiteSpace(detail) ? string.Empty : $" ({detail.Trim()})";
}
