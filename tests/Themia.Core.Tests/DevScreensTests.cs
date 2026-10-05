using Themia.Core.Localization;
using Themia.Core.Developer;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-623][S-628][S-638] Which accounts a developer widget reads, and what it says when that goes wrong.</summary>
public sealed class DevScreensTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(2));

    private static readonly DevAccount Work = new("gh:work", "work-login", null);
    private static readonly DevAccount Home = new("gh:home", "home-login", null);
    private static readonly DevAccount Broken = new("gh:old", "old-login", "Sign in again.");

    private static DevScreen Decide(
        IReadOnlyList<DevAccount> accounts,
        IReadOnlyList<string>? chosen = null,
        IReadOnlyList<DevOutcome>? outcomes = null,
        bool hasContent = false,
        bool single = false)
        => DevScreens.Decide("GitHub", accounts, chosen ?? [], single, outcomes ?? [], hasContent, "No issues assigned to you.", Now, Locale.En);

    [Fact]
    public void S623_none_chosen_means_every_account_or_the_first()
    {
        Assert.Equal(["gh:work", "gh:home"], DevScreens.Targets([Work, Home], [], single: false).Select(a => a.Id));
        Assert.Equal(["gh:work"], DevScreens.Targets([Work, Home], [], single: true).Select(a => a.Id));
        Assert.Equal(["gh:home"], DevScreens.Targets([Work, Home], ["gh:home", "gh:gone"], single: false).Select(a => a.Id));
    }

    [Fact]
    public void S1862_no_account_is_left_to_the_set_up_button()
    {
        var screen = Decide([]);

        Assert.Equal(DevScreenKind.NoAccounts, screen.Kind);
        Assert.False(screen.OfferAccounts);
        Assert.Equal(string.Empty, screen.Text);
    }

    [Fact]
    public void S628_an_anonymous_widget_whose_chosen_account_left_asks_for_a_sign_in()
    {
        var screen = DevScreens.Decide("GitHub", [], ["gh:gone"], true, [], false, string.Empty, Now, Locale.En, anonymous: true);

        Assert.Equal(DevScreenKind.NoAccounts, screen.Kind);
        Assert.True(screen.OfferAccounts);
        Assert.Contains("Sign in to GitHub", screen.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void S628_a_chosen_account_that_left_says_so()
    {
        var screen = Decide([Work], chosen: ["gh:gone"]);

        Assert.Equal(DevScreenKind.AccountsGone, screen.Kind);
        Assert.True(screen.OfferAccounts);
    }

    [Fact]
    public void S628_an_account_that_needs_signing_in_names_itself()
    {
        var screen = Decide([Broken]);

        Assert.Equal(DevScreenKind.SignInRequired, screen.Kind);
        Assert.Contains("old-login", screen.Text, StringComparison.Ordinal);
        Assert.True(screen.OfferAccounts);
    }

    [Fact]
    public void S628_a_refused_sign_in_counts_before_the_account_list_hears_of_it()
    {
        var screen = Decide([Work], outcomes: [new DevOutcome("gh:work", FetchStatus.NotConfigured)]);
        Assert.Equal(DevScreenKind.SignInRequired, screen.Kind);
    }

    [Fact]
    public void S628_loading_until_every_usable_account_answered()
    {
        Assert.Equal(DevScreenKind.Loading, Decide([Work, Home], outcomes: [new DevOutcome("gh:work", FetchStatus.Ok)]).Kind);
        Assert.Equal(DevScreenKind.Empty, Decide([Work, Home], outcomes: [new DevOutcome("gh:work", FetchStatus.Ok), new DevOutcome("gh:home", FetchStatus.Empty)]).Kind);
    }

    [Fact]
    public void S622_every_account_failing_is_a_failure_not_an_empty_list()
    {
        // Classic R-03: a total outage used to read "No pending reviews.".
        var screen = Decide([Work, Home], outcomes:
        [
            new DevOutcome("gh:work", FetchStatus.Offline),
            new DevOutcome("gh:home", FetchStatus.Offline),
        ]);

        Assert.Equal(DevScreenKind.Failed, screen.Kind);
        Assert.Equal("No network.", screen.Text);
    }

    [Fact]
    public void S622_one_account_answering_is_enough()
    {
        var screen = Decide([Work, Home], outcomes:
        [
            new DevOutcome("gh:work", FetchStatus.Unreachable),
            new DevOutcome("gh:home", FetchStatus.Empty),
        ]);

        Assert.Equal(DevScreenKind.Empty, screen.Kind);
        Assert.Equal("Could not refresh. Can't reach GitHub.", screen.Warning);
    }

    [Fact]
    public void S638_content_stays_through_a_failure_and_the_failure_is_flagged()
    {
        var screen = Decide([Work], outcomes: [new DevOutcome("gh:work", FetchStatus.TimedOut)], hasContent: true);

        Assert.Equal(DevScreenKind.Content, screen.Kind);
        Assert.Equal("Could not refresh. GitHub did not answer in time.", screen.Warning);
    }

    [Fact]
    public void S638_a_broken_second_account_is_flagged_beside_content()
    {
        var screen = Decide([Work, Broken], hasContent: true, outcomes: [new DevOutcome("gh:work", FetchStatus.Ok)]);

        Assert.Equal(DevScreenKind.Content, screen.Kind);
        Assert.True(screen.OfferAccounts);
        Assert.Contains("old-login", screen.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void S625_a_rate_limit_says_when_it_will_ask_again_in_local_time()
    {
        var outcome = new DevOutcome("gh:work", FetchStatus.RateLimited, null, new DateTimeOffset(2026, 10, 4, 10, 45, 0, TimeSpan.Zero));

        Assert.Equal("GitHub asked Themia to slow down; trying again at 12:45 PM", DevScreens.Explain("GitHub", outcome, Now, Locale.En));
        Assert.Equal("GitHub asked Themia to slow down", DevScreens.Explain("GitHub", outcome with { RetryAt = null }, Now, Locale.En));
    }

    [Fact]
    public void S628_a_specific_sentence_wins_over_the_generic_one()
    {
        var outcome = new DevOutcome("gh:work", FetchStatus.BadResponse, "Repository not found, or this account cannot see it.");
        Assert.Equal("Repository not found, or this account cannot see it", DevScreens.Explain("GitHub", outcome, Now, Locale.En));
    }
}
