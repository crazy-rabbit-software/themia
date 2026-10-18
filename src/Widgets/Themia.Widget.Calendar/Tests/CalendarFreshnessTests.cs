using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Calendar.Tests;

/// <summary>[S-3651] The calendar's one stale rule: its sync problems as the kit's outcomes.</summary>
public sealed class CalendarFreshnessTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void S3651_a_calendar_that_could_not_sync_is_one_line_in_the_kits_words_with_its_age_on_refresh()
    {
        var at = Now.AddMinutes(-20);
        var labels = new Dictionary<string, string> { ["w"] = "Work" };
        var outcomes = CalendarFreshness.Outcomes([("w", SyncProblem.Offline, at), ("w", SyncProblem.Offline, at.AddMinutes(5))], labels);

        Assert.Equal(
            [$"Work: {FetchState.Message(Locale.En, FetchStatus.Offline)} · updated {KitFreshness.When(Locale.En, at, Now)}"],
            KitFreshness.Lines(Locale.En, outcomes, Now));
        Assert.EndsWith("20 min. ago", KitFreshness.RefreshLabel(Locale.En, outcomes, Now), StringComparison.Ordinal);
    }

    [Fact]
    public void S3651_an_account_without_a_label_is_named_by_its_id_and_nothing_failed_is_no_line()
    {
        Assert.Empty(KitFreshness.Lines(Locale.En, CalendarFreshness.Outcomes([], new Dictionary<string, string>()), Now));
        Assert.Equal("x", CalendarFreshness.Outcomes([("x", SyncProblem.TimedOut, null)], new Dictionary<string, string>())[0].Owner);
    }

    [Theory]
    [InlineData(SyncProblem.Offline, FetchStatus.Offline)]
    [InlineData(SyncProblem.Unreachable, FetchStatus.Unreachable)]
    [InlineData(SyncProblem.TimedOut, FetchStatus.TimedOut)]
    [InlineData(SyncProblem.RateLimited, FetchStatus.RateLimited)]
    [InlineData(SyncProblem.SignInRequired, FetchStatus.SignInRequired)]
    [InlineData(SyncProblem.Forbidden, FetchStatus.Forbidden)]
    [InlineData(SyncProblem.NoAccount, FetchStatus.NotConfigured)]
    [InlineData(SyncProblem.ServerError, FetchStatus.BadResponse)]
    [InlineData(SyncProblem.BadResponse, FetchStatus.BadResponse)]
    public void S3651_every_sync_problem_has_its_status(SyncProblem problem, FetchStatus status)
        => Assert.Equal(status, SyncProblems.ToFetchStatus(problem));
}
