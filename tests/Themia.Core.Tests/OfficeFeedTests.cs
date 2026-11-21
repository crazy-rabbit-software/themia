using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-7077] The office widgets' one feed: its screens, the worst problem, single flight, the stale rule.</summary>
public sealed class OfficeFeedTests
{
    private static readonly Dictionary<string, string> Labels = new() { ["a"] = "Work", ["b"] = "Home" };

    private static OfficeLoad<string> Asked(string? data, bool any, params OfficeProblem[] problems) => new(OfficeLoadState.Asked, data, any, problems, Labels);

    [Fact]
    public void S7077_the_worst_problem_is_one_only_the_user_can_mend_else_the_first()
    {
        var offline = new OfficeProblem("a", FetchStatus.Offline, null);
        var signIn = new OfficeProblem("b", FetchStatus.SignInRequired, null);

        Assert.Equal(signIn, OfficeScreens.Worst([offline, signIn]));
        Assert.Equal(offline, OfficeScreens.Worst([offline, new OfficeProblem("b", FetchStatus.TimedOut, null)]));
        Assert.Null(OfficeScreens.Worst([]));
    }

    [Fact]
    public void S7077_a_load_decides_the_kits_screen()
    {
        var en = Locale.En;
        Assert.Equal(KitScreenKind.Loading, OfficeScreens.Decide<string>(en, null, SyncSubject.Calendar).Kind);
        Assert.Equal(KitScreenKind.NoAccounts, OfficeScreens.Decide(en, OfficeLoad<string>.Of(OfficeLoadState.NoAccounts), SyncSubject.Calendar).Kind);

        var gone = OfficeScreens.Decide(en, OfficeLoad<string>.Of(OfficeLoadState.AccountsGone), SyncSubject.Contacts);
        Assert.Equal(KitScreenKind.AccountsGone, gone.Kind);
        Assert.True(gone.OffersAccounts);
        Assert.Equal(OfficeProblems.Message(FetchStatus.NotConfigured, SyncSubject.Contacts, en), gone.Sentence);

        var nothing = OfficeScreens.Decide(en, Asked(null, false, new("a", FetchStatus.Offline, null), new("b", FetchStatus.Forbidden, null)), SyncSubject.Contacts);
        Assert.Equal(KitScreenKind.Problem, nothing.Kind);
        Assert.Equal(OfficeProblems.Message(FetchStatus.Forbidden, SyncSubject.Contacts, en), nothing.Sentence);
        Assert.True(nothing.OffersAccounts);

        var offline = OfficeScreens.Decide(en, Asked(null, false, new OfficeProblem("a", FetchStatus.Offline, null)), SyncSubject.Calendar);
        Assert.False(offline.OffersAccounts);

        Assert.Equal(KitScreenKind.Items, OfficeScreens.Decide(en, Asked("x", true, new OfficeProblem("a", FetchStatus.Offline, null)), SyncSubject.Calendar).Kind);
        Assert.Equal(KitScreenKind.Items, OfficeScreens.Decide(en, Asked(null, false), SyncSubject.Calendar).Kind);

        var threw = OfficeScreens.Decide(en, Asked("x", true), SyncSubject.Calendar, failed: "could not read");
        Assert.Equal((KitScreenKind.Problem, "could not read"), (threw.Kind, threw.Sentence));
    }

    [Fact]
    public void S7077_the_stale_rule_names_each_account_by_its_label_with_when_its_copy_is_from()
    {
        var at = new DateTimeOffset(2026, 11, 21, 10, 42, 0, TimeSpan.Zero);
        var outcomes = OfficeScreens.Outcomes([new("a", FetchStatus.Offline, at), new("zz", FetchStatus.TimedOut, null)], Labels);

        Assert.Equal([new KitOutcome("Work", FetchStatus.Offline, HeldAt: at), new KitOutcome("zz", FetchStatus.TimedOut)], outcomes);
    }

    [Fact]
    public async Task S7077_loads_are_single_flight_and_one_asked_meanwhile_runs_after_forced_if_any_was()
    {
        var gate = new TaskCompletionSource<OfficeLoad<string>>();
        var asked = new List<bool>();
        var feed = new OfficeFeed<string>((force, _) =>
        {
            asked.Add(force);
            return asked.Count == 1 ? gate.Task : Task.FromResult(Asked("second", true));
        }, SyncSubject.Calendar, _ => "failed");

        var first = feed.RefreshAsync(force: false);
        Assert.True(feed.Busy);
        await feed.RefreshAsync(force: false);
        await feed.RefreshAsync(force: true);
        Assert.Single(asked);

        gate.SetResult(Asked("first", true));
        await first;

        Assert.Equal([false, true], asked);
        Assert.False(feed.Busy);
        Assert.Equal("second", feed.Data);
    }

    [Fact]
    public async Task S7077_a_load_that_throws_is_the_problem_screen_and_the_data_held_stays()
    {
        var throws = false;
        var feed = new OfficeFeed<string>((_, _) => throws ? throw new InvalidOperationException("boom") : Task.FromResult(Asked("held", true, new OfficeProblem("a", FetchStatus.Offline, null))), SyncSubject.Calendar, _ => "failed");

        await feed.RefreshAsync(force: false);
        Assert.Equal(KitScreenKind.Items, feed.Screen(Locale.En).Kind);
        Assert.Single(feed.Outcomes);

        throws = true;
        await feed.RefreshAsync(force: false);
        Assert.Equal((KitScreenKind.Problem, "failed"), (feed.Screen(Locale.En).Kind, feed.Screen(Locale.En).Sentence));
        Assert.Equal("held", feed.Data);
        Assert.Empty(feed.Outcomes);
    }

    [Fact]
    public async Task S7077_a_load_that_brought_nothing_keeps_the_data_of_the_last_that_had_some()
    {
        var loads = new Queue<OfficeLoad<string>>([Asked("first", true), Asked(null, false, new OfficeProblem("a", FetchStatus.Offline, null)), OfficeLoad<string>.Of(OfficeLoadState.NoAccounts)]);
        var feed = new OfficeFeed<string>((_, _) => Task.FromResult(loads.Dequeue()), SyncSubject.Calendar, _ => "failed");

        await feed.RefreshAsync(force: false);
        await feed.RefreshAsync(force: false);
        Assert.Equal("first", feed.Data);
        Assert.Equal(KitScreenKind.Problem, feed.Screen(Locale.En).Kind);

        await feed.RefreshAsync(force: false);
        Assert.Null(feed.Data);
        Assert.Equal(KitScreenKind.NoAccounts, feed.Screen(Locale.En).Kind);
    }
}
