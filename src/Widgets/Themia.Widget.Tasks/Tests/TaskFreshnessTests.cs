using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Tasks.Tests;

/// <summary>[S-607] Tasks' one stale rule: an account list's last refresh as the kit's outcome.</summary>
public sealed class TaskFreshnessTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void S607_a_list_whose_refresh_failed_is_the_headers_one_line_with_its_age_on_refresh()
    {
        var at = Now.AddMinutes(-20);
        var outcomes = TaskFreshness.Outcomes("Work", loaded: true, new AccountException(AccountError.Offline, "no network"), at);

        Assert.Equal(
            [$"Work: {FetchState.Message(Locale.En, FetchStatus.Offline)} · updated {KitFreshness.When(Locale.En, at, Now)}"],
            KitFreshness.Lines(Locale.En, outcomes, Now));
        Assert.EndsWith("20 min. ago", KitFreshness.RefreshLabel(Locale.En, outcomes, Now), StringComparison.Ordinal);
    }

    [Fact]
    public void S607_a_list_that_refreshed_says_nothing_but_its_age_and_a_list_on_this_pc_has_none()
    {
        var at = Now.AddMinutes(-3);
        var ok = TaskFreshness.Outcomes("Work", loaded: true, failure: null, at);

        Assert.Empty(KitFreshness.Lines(Locale.En, ok, Now));
        Assert.EndsWith("3 min. ago", KitFreshness.RefreshLabel(Locale.En, ok, Now), StringComparison.Ordinal);

        // Nothing held yet: no stale line (the screen says it), and a plain Refresh.
        Assert.Empty(KitFreshness.Lines(Locale.En, TaskFreshness.Outcomes("Work", loaded: false, new AccountException(AccountError.Offline, "x"), null), Now));
        Assert.Empty(TaskFreshness.Outcomes(null, loaded: false, failure: null, fetchedAt: null));
    }
}
