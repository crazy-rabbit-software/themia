using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// What a network widget says about the network, and how hard it tries again.
/// <para>
/// Both are easy to get wrong in a way that never shows up on screen: a back-off that does not
/// actually grow looks identical to one that does until somebody reads the service's access log.
/// </para>
/// </summary>
public class FetchStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(FetchStatus.NotConfigured)]
    [InlineData(FetchStatus.Loading)]
    [InlineData(FetchStatus.Offline)]
    [InlineData(FetchStatus.Unreachable)]
    [InlineData(FetchStatus.TimedOut)]
    [InlineData(FetchStatus.RateLimited)]
    [InlineData(FetchStatus.BadResponse)]
    [InlineData(FetchStatus.Empty)]
    [InlineData(FetchStatus.Refused)]
    [InlineData(FetchStatus.SignInRequired)]
    [InlineData(FetchStatus.Forbidden)]
    [InlineData(FetchStatus.NotFound)]
    public void Every_state_but_success_has_its_own_sentence(FetchStatus status)
        => Assert.NotEqual(string.Empty, FetchState.Message(Locale.En, status));

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void No_two_states_share_a_sentence(Locale locale)
    {
        // The point of naming them separately. The original app collapsed a 403, a DNS failure and
        // a genuinely empty feed into one message and made the difference undiagnosable — in every
        // language, not only English.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var status in Enum.GetValues<FetchStatus>())
        {
            var message = FetchState.Message(locale, status);

            if (message.Length == 0)
            {
                continue;
            }

            Assert.True(seen.Add(message), $"{status} reuses another state's sentence in {locale}");
        }
    }

    public static TheoryData<Locale> AllLocales()
    {
        var data = new TheoryData<Locale>();
        foreach (var locale in LanguageResolver.All)
        {
            data.Add(locale);
        }

        return data;
    }

    [Fact]
    public void S1106_the_state_is_said_in_the_asked_language()
        => Assert.Equal("Kein Netzwerk", FetchState.Message(Locale.De, FetchStatus.Offline));

    [Theory]
    [InlineData(FetchStatus.NotConfigured, false)]
    [InlineData(FetchStatus.Refused, false)]
    [InlineData(FetchStatus.Offline, true)]
    [InlineData(FetchStatus.RateLimited, true)]
    [InlineData(FetchStatus.BadResponse, true)]
    [InlineData(FetchStatus.SignInRequired, false)]
    [InlineData(FetchStatus.Forbidden, false)]
    [InlineData(FetchStatus.NotFound, false)]
    public void Only_the_states_a_timer_can_fix_are_worth_retrying(FetchStatus status, bool retryable)
        => Assert.Equal(retryable, FetchState.IsRetryable(status));

    [Theory]
    [InlineData(FetchStatus.Ok, false)]
    [InlineData(FetchStatus.Loading, false)]
    [InlineData(FetchStatus.NotConfigured, false)]
    [InlineData(FetchStatus.Empty, true)]
    [InlineData(FetchStatus.Offline, true)]
    [InlineData(FetchStatus.SignInRequired, true)]
    [InlineData(FetchStatus.Forbidden, true)]
    [InlineData(FetchStatus.NotFound, true)]
    public void A_failure_is_the_thing_that_replaces_the_data(FetchStatus status, bool failure)
        => Assert.Equal(failure, FetchState.IsFailure(status));

    // ---- freshness -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "Just now")]
    [InlineData(59, "Just now")]
    [InlineData(60, "1 min. ago")]
    [InlineData(4 * 60, "4 min. ago")]
    [InlineData(3599, "59 min. ago")]
    [InlineData(3600, "1 hr. ago")]
    [InlineData(23 * 3600, "23 hr. ago")]
    [InlineData(48 * 3600, "2 days ago")]
    public void The_age_of_the_data_is_said_in_the_units_that_carry(int secondsOld, string expected)
        => Assert.Equal(expected, FetchState.Freshness(Locale.En, Now.AddSeconds(-secondsOld), Now));

    [Fact]
    public void S1107_the_age_follows_the_language()
    {
        Assert.Equal("Gerade eben", FetchState.Freshness(Locale.De, Now, Now));
        Assert.Equal("vor 4 Min.", FetchState.Freshness(Locale.De, Now.AddMinutes(-4), Now));
    }

    [Fact]
    public void A_provider_clock_running_ahead_is_not_the_future()
    {
        // Clock skew between a service and this machine is routine, and "-3m ago" reads as a bug.
        Assert.Equal("Just now", FetchState.Freshness(Locale.En, Now.AddMinutes(5), Now));
    }

    [Fact]
    public void Data_is_stale_past_three_refresh_intervals()
    {
        var interval = TimeSpan.FromMinutes(15);

        Assert.False(FetchState.IsStale(Now.AddMinutes(-44), Now, interval));
        Assert.True(FetchState.IsStale(Now.AddMinutes(-46), Now, interval));
    }

    [Fact]
    public void A_fast_widget_does_not_cry_stale_over_one_missed_tick()
    {
        // Three minutes on a one-minute cadence is one skipped refresh, not a stale readout.
        var interval = TimeSpan.FromMinutes(1);

        Assert.False(FetchState.IsStale(Now.AddMinutes(-4), Now, interval));
        Assert.True(FetchState.IsStale(Now.AddMinutes(-6), Now, interval));
    }

    // ---- back-off --------------------------------------------------------------------------

    [Fact]
    public void After_a_success_the_widget_asks_on_its_own_cadence()
    {
        var interval = TimeSpan.FromMinutes(5);

        Assert.Equal(interval, Backoff.Delay(interval, 0, FetchStatus.Ok));
    }

    [Fact]
    public void One_failure_costs_no_extra_delay()
    {
        // A dropped packet should not double the cadence.
        var interval = TimeSpan.FromMinutes(5);

        Assert.Equal(interval, Backoff.Delay(interval, 1, FetchStatus.Unreachable));
    }

    [Fact]
    public void Each_further_failure_doubles_the_wait()
    {
        var interval = TimeSpan.FromMinutes(5);

        Assert.Equal(TimeSpan.FromMinutes(10), Backoff.Delay(interval, 2, FetchStatus.Unreachable));
        Assert.Equal(TimeSpan.FromMinutes(20), Backoff.Delay(interval, 3, FetchStatus.Unreachable));
        Assert.Equal(TimeSpan.FromMinutes(40), Backoff.Delay(interval, 4, FetchStatus.Unreachable));
    }

    [Fact]
    public void The_wait_never_passes_an_hour_for_an_ordinary_failure()
    {
        var interval = TimeSpan.FromMinutes(5);

        for (var failures = 5; failures < 40; failures++)
        {
            Assert.True(Backoff.Delay(interval, failures, FetchStatus.TimedOut) <= Backoff.Ceiling);
        }

        Assert.Equal(Backoff.Ceiling, Backoff.Delay(interval, 20, FetchStatus.TimedOut));
    }

    [Fact]
    public void A_very_fast_widget_still_backs_off_to_at_least_the_floor()
    {
        // A one-minute stocks widget that fails must not keep asking every minute.
        var delay = Backoff.Delay(TimeSpan.FromSeconds(5), 1, FetchStatus.Unreachable);

        Assert.Equal(Backoff.Floor, delay);
    }

    [Fact]
    public void A_rate_limit_backs_off_to_a_quarter_of_an_hour_on_the_very_first_one()
    {
        // 429 is the service asking to be left alone, and honouring it on the second failure
        // rather than the first is fourteen more requests it did not want.
        var delay = Backoff.Delay(TimeSpan.FromMinutes(1), 1, FetchStatus.RateLimited);

        Assert.Equal(Backoff.RateLimitFloor, delay);
    }

    [Fact]
    public void A_rate_limit_grows_far_past_the_ordinary_ceiling()
    {
        var interval = TimeSpan.FromMinutes(1);

        Assert.Equal(TimeSpan.FromMinutes(30), Backoff.Delay(interval, 2, FetchStatus.RateLimited));
        Assert.Equal(TimeSpan.FromHours(1), Backoff.Delay(interval, 3, FetchStatus.RateLimited));
        Assert.Equal(Backoff.RateLimitCeiling, Backoff.Delay(interval, 12, FetchStatus.RateLimited));
    }

    [Fact]
    public void The_delay_never_goes_backwards_as_failures_pile_up()
    {
        var interval = TimeSpan.FromMinutes(5);
        var previous = TimeSpan.Zero;

        for (var failures = 1; failures < 30; failures++)
        {
            var delay = Backoff.Delay(interval, failures, FetchStatus.Unreachable);

            Assert.True(delay >= previous, $"delay shrank at failure {failures}");
            previous = delay;
        }
    }
}
