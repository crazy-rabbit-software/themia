using Themia.Core.Accounts;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Retry, redirect, paging and error-body rules shared by every account request.</summary>
public class AccountHttpRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(429, 0, true, true)]
    [InlineData(503, 2, true, true)]
    [InlineData(503, 3, true, false)]
    [InlineData(429, 0, false, false)]
    [InlineData(500, 0, true, false)]
    [InlineData(401, 0, true, false)]
    [InlineData(200, 0, true, false)]
    public void S518_only_throttles_of_repeatable_requests_are_retried(int status, int retries, bool idempotent, bool expected)
        => Assert.Equal(expected, RetryRules.ShouldRetry(status, retries, idempotent));

    [Fact]
    public void S518_retry_after_is_honoured_but_capped()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), RetryRules.Delay(0, TimeSpan.FromSeconds(5)));
        Assert.Equal(RetryRules.MaxWait, RetryRules.Delay(0, TimeSpan.FromHours(2)));
        Assert.Equal(TimeSpan.Zero, RetryRules.Delay(0, TimeSpan.FromSeconds(-3)));
    }

    [Fact]
    public void S518_without_retry_after_the_backoff_doubles()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), RetryRules.Delay(0, null));
        Assert.Equal(TimeSpan.FromSeconds(2), RetryRules.Delay(1, null));
        Assert.Equal(TimeSpan.FromSeconds(4), RetryRules.Delay(2, null));
    }

    [Theory]
    [InlineData("120", 120)]
    [InlineData(" 5 ", 5)]
    [InlineData("Sun, 04 Oct 2026 12:00:30 GMT", 30)]
    [InlineData("Sun, 04 Oct 2026 11:00:00 GMT", 0)]
    public void S518_retry_after_reads_seconds_and_dates(string header, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetryRules.ParseRetryAfter(header, Now));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("-5")]
    public void S518_an_unreadable_retry_after_is_none(string? header)
        => Assert.Null(RetryRules.ParseRetryAfter(header, Now));

    [Theory]
    [InlineData("https://a.example/x", "https://a.example/y", true)]
    [InlineData("https://a.example/x", "http://a.example/y", false)]
    [InlineData("https://a.example/x", "https://127.0.0.1/y", false)]
    [InlineData("https://a.example/x", "https://192.168.1.10/y", false)]
    [InlineData("https://a.example/x", "https://localhost/y", false)]
    [InlineData("http://192.168.1.10/x", "http://192.168.1.10/dav/", true)]
    [InlineData("https://a.example/x", "file:///c:/windows", false)]
    [InlineData("https://a.example/x", "https://b.example/y", true)]
    public void S519_redirects_are_followed_only_where_they_are_safe(string from, string to, bool expected)
        => Assert.Equal(expected, RedirectRules.MayFollow(new Uri(from), new Uri(to)));

    [Theory]
    [InlineData("https://a.example/x", "https://a.example/y", CredentialKind.Bearer, true)]
    [InlineData("https://graph.microsoft.com/x", "https://storage.example/blob", CredentialKind.Bearer, false)]
    [InlineData("https://caldav.icloud.com/", "https://p57-caldav.icloud.com/", CredentialKind.Basic, true)]
    [InlineData("http://dav.example/", "http://other.example/", CredentialKind.Basic, false)]
    [InlineData("https://a.example:8443/", "https://a.example/", CredentialKind.Bearer, false)]
    public void S519_credentials_follow_a_redirect_only_where_they_belong(string from, string to, CredentialKind kind, bool expected)
        => Assert.Equal(expected, RedirectRules.MayCarryCredentials(new Uri(from), new Uri(to), kind));

    [Theory]
    [InlineData("https://graph.microsoft.com/v1.0/me/messages?$skip=10", true)]
    [InlineData("https://evil.example/collect", false)]
    [InlineData("http://graph.microsoft.com/v1.0/me/messages", false)]
    public void S519_a_next_link_keeps_the_token_on_its_own_host(string next, bool expected)
        => Assert.Equal(expected, RedirectRules.IsSafeNextLink(new Uri("https://graph.microsoft.com/v1.0/me/messages"), new Uri(next)));

    [Theory]
    [InlineData("""{"error":{"code":"InvalidAuthenticationToken","message":"Access token has expired."}}""", "Access token has expired.")]
    [InlineData("""{"error":"invalid_grant","error_description":"AADSTS70008: The refresh token has expired.\r\nTrace ID: 1"}""", "AADSTS70008: The refresh token has expired.")]
    [InlineData("""{"error":"invalid_grant"}""", "invalid_grant")]
    [InlineData("""{"message":"Bad credentials","documentation_url":"https://docs.github.com"}""", "Bad credentials")]
    [InlineData("""{"type":"error","error":{"type":"permission_error","message":"OAuth token does not meet scope requirement user:profile"}}""", "OAuth token does not meet scope requirement user:profile")]
    public void S520_the_providers_own_words_are_found(string body, string expected)
        => Assert.Equal(expected, ErrorBody.Message(body));

    [Theory]
    [InlineData("<html>502 Bad Gateway</html>")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData(null)]
    public void S520_a_body_in_no_known_shape_has_no_message(string? body)
        => Assert.Null(ErrorBody.Message(body));

    [Fact]
    public void S520_oauth_error_code_is_read()
    {
        Assert.Equal("invalid_grant", ErrorBody.OAuthCode("""{"error":"invalid_grant","error_description":"x"}"""));
        Assert.Null(ErrorBody.OAuthCode("""{"error":{"message":"x"}}"""));
        Assert.Null(ErrorBody.OAuthCode("nope"));
    }

    [Theory]
    [InlineData(401, AccountError.SignInRequired)]
    [InlineData(403, AccountError.Forbidden)]
    [InlineData(404, AccountError.NotFound)]
    [InlineData(429, AccountError.RateLimited)]
    [InlineData(503, AccountError.ServerError)]
    [InlineData(400, AccountError.BadResponse)]
    public void S520_statuses_map_to_named_errors(int status, AccountError expected)
        => Assert.Equal(expected, AccountException.FromStatus(status));

    [Theory]
    [InlineData(AccountError.Offline, FetchStatus.Offline)]
    [InlineData(AccountError.Unreachable, FetchStatus.Unreachable)]
    [InlineData(AccountError.TimedOut, FetchStatus.TimedOut)]
    [InlineData(AccountError.RateLimited, FetchStatus.RateLimited)]
    [InlineData(AccountError.UnknownAccount, FetchStatus.NotConfigured)]
    [InlineData(AccountError.SignInRequired, FetchStatus.SignInRequired)]
    [InlineData(AccountError.Forbidden, FetchStatus.Forbidden)]
    [InlineData(AccountError.NotFound, FetchStatus.NotFound)]
    [InlineData(AccountError.ServerError, FetchStatus.BadResponse)]
    [InlineData(AccountError.BadResponse, FetchStatus.BadResponse)]
    [InlineData(AccountError.Refused, FetchStatus.Refused)]
    public void S9900_errors_map_onto_widget_fetch_states_without_folding(AccountError error, FetchStatus expected)
        => Assert.Equal(expected, new AccountException(error, "x").ToFetchStatus());

    [Fact]
    public void S9900_every_account_error_has_a_fetch_state()
    {
        foreach (var error in Enum.GetValues<AccountError>())
        {
            Assert.True(Enum.IsDefined(new AccountException(error, "x").ToFetchStatus()));
        }
    }

    [Fact]
    public void S9901_a_known_retry_time_holds_the_schedule_until_then()
    {
        var now = new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);
        var schedule = new SyncSchedule(TimeSpan.FromMinutes(2));
        var throttle = new AccountException(AccountError.RateLimited, "slow down", 429) { RetryAt = now.AddMinutes(50) };

        schedule.Failed(throttle, now);

        Assert.Equal(FetchStatus.RateLimited, schedule.Status);
        Assert.False(schedule.IsDue(now.AddMinutes(49)));
        Assert.True(schedule.IsDue(now.AddMinutes(50)));

        // The user's Refresh does not jump a provider's own "slow down".
        schedule.Force(now.AddMinutes(10));
        Assert.False(schedule.IsDue(now.AddMinutes(10)));
    }

    [Fact]
    public void S9901_a_retry_time_never_shortens_the_back_off()
    {
        var now = new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);
        var schedule = new SyncSchedule(TimeSpan.FromMinutes(2));

        schedule.Failed(new AccountException(AccountError.RateLimited, "x", 429) { RetryAt = now.AddSeconds(5) }, now);

        Assert.False(schedule.IsDue(now.AddMinutes(14))); // the rate-limit floor of [S-2609]'s back-off still holds
    }

    [Fact]
    public void S9900_a_refusal_only_the_user_can_mend_is_not_retried_on_the_cadence()
    {
        var now = new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);
        foreach (var error in new[] { AccountError.SignInRequired, AccountError.Forbidden, AccountError.NotFound })
        {
            var schedule = new SyncSchedule(TimeSpan.FromMinutes(2));
            schedule.Failed(new AccountException(error, "x"), now);
            Assert.False(schedule.IsDue(now.AddMinutes(59)));
        }
    }
}
