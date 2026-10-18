using Themia.Core.Accounts;
using Themia.Sdk.Services;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-9920] A service's rate-limit headers, read through its descriptor's scheme so a throttle is
/// waited out rather than retried. The first scheme is shaped on GitHub's REST budget (the rules'
/// constants of before; the assertions are the ones they had), the others on Linear's two budgets in
/// epoch milliseconds and Todoist's body-only limits with a Retry-After on 401 that is not a wait.
/// </summary>
public sealed class RateLimitRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    internal static readonly RateLimitScheme GitHubShaped = new(
        [new RateBudget("X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset")],
        ResetUnit.EpochSeconds,
        "X-RateLimit-Resource",
        new HashSet<int> { 403, 429 },
        new HashSet<int>());

    internal static readonly RateLimitScheme LinearShaped = new(
        [
            new RateBudget("X-RateLimit-Requests-Limit", "X-RateLimit-Requests-Remaining", "X-RateLimit-Requests-Reset"),
            new RateBudget("X-RateLimit-Complexity-Limit", "X-RateLimit-Complexity-Remaining", "X-RateLimit-Complexity-Reset"),
        ],
        ResetUnit.EpochMillis,
        null,
        new HashSet<int> { 400, 429 },
        new HashSet<int>());

    internal static readonly RateLimitScheme TodoistShaped = new([], ResetUnit.DeltaSeconds, null, new HashSet<int> { 429 }, new HashSet<int> { 401 });

    internal static Dictionary<string, string> Headers(params (string Name, string Value)[] pairs)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in pairs)
        {
            headers[name] = value;
        }

        return headers;
    }

    private static string Epoch(DateTimeOffset at) => at.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

    // ---- the GitHub-shaped scheme: what the rules did before, unchanged ---------------------------

    [Fact]
    public void S9920_reads_a_budgets_headers()
    {
        var reset = Now.AddMinutes(30);
        var state = RateLimitRules.Read(GitHubShaped,
            Headers(("x-ratelimit-limit", "5000"), ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", Epoch(reset)), ("x-ratelimit-resource", "search")),
            Now);

        Assert.Equal(5000, state.Limit);
        Assert.True(state.Exhausted);
        Assert.Equal(reset, state.ResetAt);
        Assert.Null(state.RetryAfter);
        Assert.Equal("search", state.Bucket);
    }

    [Fact]
    public void S9920_a_403_with_a_spent_budget_is_a_throttle_not_a_refusal()
    {
        var spent = new RateLimitState(5000, 0, Now.AddMinutes(30), null);
        var fine = new RateLimitState(5000, 4000, Now.AddMinutes(30), null);

        Assert.True(RateLimitRules.IsThrottle(GitHubShaped, 403, spent));
        Assert.False(RateLimitRules.IsThrottle(GitHubShaped, 403, fine));
        Assert.True(RateLimitRules.IsThrottle(GitHubShaped, 403, fine with { RetryAfter = TimeSpan.FromSeconds(60) })); // secondary limit
        Assert.True(RateLimitRules.IsThrottle(GitHubShaped, 429, RateLimitState.None));
        Assert.False(RateLimitRules.IsThrottle(GitHubShaped, 404, spent));
    }

    [Fact]
    public void S9920_waits_for_retry_after_then_the_reset_then_a_minute()
    {
        var reset = Now.AddMinutes(30);

        Assert.Equal(Now.AddSeconds(90), RateLimitRules.WaitUntil(GitHubShaped, 403, new RateLimitState(null, 0, reset, TimeSpan.FromSeconds(90)), Now));
        Assert.Equal(reset, RateLimitRules.WaitUntil(GitHubShaped, 403, new RateLimitState(null, 0, reset, null), Now));
        Assert.Equal(Now + RateLimitRules.DefaultWait, RateLimitRules.WaitUntil(GitHubShaped, 429, RateLimitState.None, Now));
    }

    [Fact]
    public void S9920_a_success_that_spent_the_last_request_waits_for_the_reset()
    {
        var reset = Now.AddMinutes(12);
        Assert.Equal(reset, RateLimitRules.WaitUntil(GitHubShaped, 200, new RateLimitState(60, 0, reset, null), Now));
        Assert.Null(RateLimitRules.WaitUntil(GitHubShaped, 200, new RateLimitState(60, 3, reset, null), Now));
        Assert.Null(RateLimitRules.WaitUntil(GitHubShaped, 304, RateLimitState.None, Now));
    }

    [Fact]
    public void S9920_a_reset_days_away_is_not_believed()
    {
        var wait = RateLimitRules.WaitUntil(GitHubShaped, 403, new RateLimitState(null, 0, Now.AddDays(3), null), Now);
        Assert.Equal(Now + RateLimitRules.MaxWait, wait);
    }

    [Fact]
    public void S9920_junk_headers_read_as_absent_not_zero()
    {
        var state = RateLimitRules.Read(GitHubShaped, Headers(("X-RateLimit-Remaining", "lots"), ("X-RateLimit-Reset", "-1")), Now);

        Assert.Null(state.Remaining);
        Assert.False(state.Exhausted);
        Assert.Null(state.ResetAt);
        Assert.Same(RateLimitState.None, RateLimitRules.Read(GitHubShaped, null, Now));
    }

    // ---- other shapes -------------------------------------------------------------------------------

    [Fact]
    public void S9920_of_several_budgets_the_spent_one_wins_and_milliseconds_are_read_as_such()
    {
        var reset = Now.AddMinutes(20);
        var state = RateLimitRules.Read(LinearShaped, Headers(
            ("X-RateLimit-Requests-Limit", "5000"), ("X-RateLimit-Requests-Remaining", "4100"), ("X-RateLimit-Requests-Reset", (Now.AddMinutes(50).ToUnixTimeMilliseconds()).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("X-RateLimit-Complexity-Limit", "2000000"), ("X-RateLimit-Complexity-Remaining", "0"), ("X-RateLimit-Complexity-Reset", reset.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture))), Now);

        Assert.True(state.Exhausted);
        Assert.Equal(reset, state.ResetAt);
        Assert.True(RateLimitRules.IsThrottle(LinearShaped, 400, state));
        Assert.False(RateLimitRules.IsThrottle(LinearShaped, 400, state with { Remaining = 3 })); // a 400 with budget left is a bad request
    }

    [Fact]
    public void S9920_a_retry_after_on_a_status_the_scheme_says_is_no_wait_is_left_out()
    {
        var headers = Headers(("Retry-After", "120"));

        Assert.Null(RateLimitRules.Read(TodoistShaped, headers, Now, status: 401).RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(120), RateLimitRules.Read(TodoistShaped, headers, Now, status: 429).RetryAfter);
        Assert.False(RateLimitRules.IsThrottle(TodoistShaped, 401, RateLimitRules.Read(TodoistShaped, headers, Now, status: 401)));
    }

    [Fact]
    public void S9920_the_host_providers_say_slow_down_with_retry_after_alone()
    {
        var state = RateLimitRules.Read(RateLimitScheme.RetryAfterOnly, Headers(("X-RateLimit-Remaining", "0"), ("Retry-After", "30")), Now, 403);

        Assert.Null(state.Remaining); // no budget is read for them
        Assert.True(RateLimitRules.IsThrottle(RateLimitScheme.RetryAfterOnly, 403, state));
        Assert.Equal(Now.AddSeconds(30), RateLimitRules.WaitUntil(RateLimitScheme.RetryAfterOnly, 403, state, Now));
        Assert.False(RateLimitRules.IsThrottle(RateLimitScheme.RetryAfterOnly, 403, RateLimitState.None));
    }
}
