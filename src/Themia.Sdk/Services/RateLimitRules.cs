using System.Globalization;
using Themia.Sdk.Services;

namespace Themia.Core.Accounts;

/// <summary>What a response said about the account's request budget.</summary>
/// <param name="Limit">Requests allowed per window, when sent.</param>
/// <param name="Remaining">Requests left in this window, when sent.</param>
/// <param name="ResetAt">When the window starts over, when sent.</param>
/// <param name="RetryAfter">The server's own "ask again in" (<c>Retry-After</c>), when sent and meant as a wait.</param>
/// <param name="Bucket">Which budget the reply says it counted against, when the scheme names a bucket header.</param>
public sealed record RateLimitState(int? Limit, int? Remaining, DateTimeOffset? ResetAt, TimeSpan? RetryAfter, string? Bucket = null)
{
    public static RateLimitState None { get; } = new(null, null, null, null);

    /// <summary>The window is used up: asking before <see cref="ResetAt"/> only earns another refusal.</summary>
    public bool Exhausted => Remaining is 0;
}

/// <summary>
/// [S-9920] Reading a service's rate-limit headers through its descriptor's <see cref="RateLimitScheme"/>,
/// so a throttle is waited out rather than retried. The rules name no service and no header but the
/// standard <c>Retry-After</c>: a scheme says which headers hold each budget, in which unit the reset
/// is written, which header names the bucket, and which statuses are a throttle when a budget is spent
/// or a wait is named.
/// <para>
/// A service may answer a spent budget with 403 (or 429, or 400) and its budget at zero, or a
/// secondary limit with a 403 carrying <c>Retry-After</c>. Read as a plain refusal that is
/// "forbidden": a permission problem, retried on the next tick, which is exactly how a budget stays
/// spent. Read here, it is a rate limit with a known end, and nothing asks again before it.
/// </para>
/// </summary>
public static class RateLimitRules
{
    /// <summary>
    /// The longest a reset is believed. The services' windows are an hour at most; a reset further
    /// out than this is a clock problem or a hostile header, and must not silence a widget for days.
    /// </summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromHours(2);

    /// <summary>What to wait when a throttle names no end: at least a minute.</summary>
    public static readonly TimeSpan DefaultWait = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The budget of one response, as <paramref name="scheme"/> reads it. Unreadable values are absent,
    /// never zero. With several budgets (requests and complexity points), a spent one wins (the latest
    /// reset among the spent), else the one with the least left. A <c>Retry-After</c> on a status the
    /// scheme says it is not a wait on (a 401 that wants a new token) is left out.
    /// </summary>
    public static RateLimitState Read(RateLimitScheme scheme, IReadOnlyDictionary<string, string>? headers, DateTimeOffset now, int status = 0)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        if (headers is null || headers.Count == 0)
        {
            return RateLimitState.None;
        }

        RateLimitState? chosen = null;
        foreach (var budget in scheme.Budgets)
        {
            var state = new RateLimitState(
                Int(headers, budget.LimitHeader),
                Int(headers, budget.RemainingHeader),
                Reset(headers, budget.ResetHeader, scheme.Reset, now),
                null);

            if (state.Limit is null && state.Remaining is null && state.ResetAt is null)
            {
                continue;
            }

            chosen = chosen is null ? state : Worse(chosen, state);
        }

        var retryAfter = !scheme.RetryAfterIgnoredOn.Contains(status) && headers.TryGetValue("Retry-After", out var retryText)
            ? RetryAfterHeader.Parse(retryText, now)
            : null;

        var bucket = scheme.BucketHeader is { } name && headers.TryGetValue(name, out var said) && said.Trim() is { Length: > 0 and <= 64 } named
            ? named
            : null;

        if (chosen is null && retryAfter is null && bucket is null)
        {
            return RateLimitState.None;
        }

        return (chosen ?? RateLimitState.None) with { RetryAfter = retryAfter, Bucket = bucket };
    }

    /// <summary>
    /// Whether a refusal is a throttle rather than a "no": any 429, and a status the scheme lists
    /// when the budget is spent or the server names a time to come back.
    /// </summary>
    public static bool IsThrottle(RateLimitScheme scheme, int status, RateLimitState state)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(state);
        return status == 429 || (scheme.ThrottleStatuses.Contains(status) && (state.Exhausted || state.RetryAfter is not null));
    }

    /// <summary>
    /// When asking again is worth it, or null when nothing says to wait. A throttle waits for
    /// <c>Retry-After</c>, else the reset, else a minute; a success that spent the last request
    /// waits for the reset too: the next request is already known to fail. Never past
    /// <see cref="MaxWait"/>.
    /// </summary>
    public static DateTimeOffset? WaitUntil(RateLimitScheme scheme, int status, RateLimitState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(state);

        DateTimeOffset? until = null;

        if (IsThrottle(scheme, status, state))
        {
            until = Throttled(state, now);
        }
        else if (status is >= 200 and < 400 && state.Exhausted && state.ResetAt is { } reset && reset > now)
        {
            until = reset;
        }

        return Cap(until, now);
    }

    /// <summary>
    /// The wait of a throttle: its <c>Retry-After</c>, else the seconds its body named (a service that
    /// says it there), else the reset, else a minute; capped.
    /// </summary>
    public static DateTimeOffset ThrottledUntil(RateLimitState state, TimeSpan? bodyWait, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        var until = state.RetryAfter is null && bodyWait is { } wait && wait >= TimeSpan.Zero ? now + wait : Throttled(state, now);
        return Cap(until, now)!.Value;
    }

    private static DateTimeOffset Throttled(RateLimitState state, DateTimeOffset now)
        => state.RetryAfter is { } after
            ? now + after
            : state.ResetAt is { } reset && reset > now ? reset : now + DefaultWait;

    private static DateTimeOffset? Cap(DateTimeOffset? until, DateTimeOffset now)
        => until is { } at && at - now > MaxWait ? now + MaxWait : until;

    private static RateLimitState Worse(RateLimitState a, RateLimitState b)
    {
        if (a.Exhausted != b.Exhausted)
        {
            return a.Exhausted ? a : b;
        }

        if (a.Exhausted)
        {
            return (a.ResetAt ?? DateTimeOffset.MinValue) >= (b.ResetAt ?? DateTimeOffset.MinValue) ? a : b;
        }

        return (a.Remaining ?? int.MaxValue) <= (b.Remaining ?? int.MaxValue) ? a : b;
    }

    private static DateTimeOffset? Reset(IReadOnlyDictionary<string, string> headers, string name, ResetUnit unit, DateTimeOffset now)
    {
        if (!headers.TryGetValue(name, out var text)
            || !long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return unit switch
        {
            ResetUnit.EpochSeconds when value is > 0 and < 32_503_680_000 => DateTimeOffset.FromUnixTimeSeconds(value),
            ResetUnit.EpochMillis when value is > 0 and < 32_503_680_000_000 => DateTimeOffset.FromUnixTimeMilliseconds(value),
            ResetUnit.DeltaSeconds when value is >= 0 and <= 86_400 => now + TimeSpan.FromSeconds(value),
            _ => null,
        };
    }

    private static int? Int(IReadOnlyDictionary<string, string> headers, string name)
        => headers.TryGetValue(name, out var text)
            && int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
}

/// <summary>[S-518] The standard <c>Retry-After</c> header, read one way everywhere.</summary>
public static class RetryAfterHeader
{
    /// <summary>
    /// Delta seconds (what most services send) or an HTTP date. Null when absent or unreadable, so the
    /// caller falls back to its own back-off; never more than a day.
    /// </summary>
    public static TimeSpan? Parse(string? header, DateTimeOffset now)
    {
        var text = header?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(Math.Min(seconds, (long)TimeSpan.FromDays(1).TotalSeconds));
        }

        if (DateTimeOffset.TryParseExact(text, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
        {
            var delta = at - now;
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        return null;
    }
}
