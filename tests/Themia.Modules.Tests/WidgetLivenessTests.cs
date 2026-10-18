using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1880]..[S-1882], [S-1886] Which widgets are live: only those of a view a display shows, only
/// once set up; a surface releases the cards that go before it builds the ones that come.
/// </summary>
public class WidgetLivenessTests
{
    private static readonly FracRect Cell = new(0.1, 0.1, 0.2, 0.2);

    private static WidgetInstance Clock() => WidgetInstance.Create(new WidgetId("clock"), Cell);

    private static WidgetInstance Weather(bool placed)
    {
        var widget = WidgetInstance.Create(new WidgetId("weatherNow"), Cell);
        if (placed && widget.Config is WeatherNowConfig weather)
        {
            weather.Latitude = 47.4;
            weather.Longitude = 8.5;
        }

        return widget;
    }

    /// <summary>Three views: the first two each on a display, the third shown nowhere.</summary>
    private static (AppState State, ViewState Left, ViewState Right, ViewState Hidden) ThreeViews()
    {
        var left = ViewState.Create("Left");
        left.Widgets.Add(Clock());
        left.Widgets.Add(Weather(placed: true));

        var right = ViewState.Create("Right");
        right.Widgets.Add(Clock());

        var hidden = ViewState.Create("Hidden");
        hidden.Widgets.Add(Clock());
        hidden.Widgets.Add(Weather(placed: true));

        var state = new AppState { Views = [left, right, hidden], ActiveViewId = left.Id };
        state.ShowViewOn("a", left.Id);
        state.ShowViewOn("b", right.Id);
        return (state, left, right, hidden);
    }

    [Fact]
    public void S1880_a_widget_in_a_view_no_display_shows_is_never_live()
    {
        var (state, left, right, hidden) = ThreeViews();

        var live = WidgetLiveness.Live(state, ["a", "b"], FeatureSet.None);

        Assert.Equal(left.Widgets.Concat(right.Widgets), live);
        Assert.DoesNotContain(live, w => hidden.Widgets.Contains(w));
    }

    [Fact]
    public void S1880_a_view_on_two_displays_counts_once()
    {
        var (state, left, _, _) = ThreeViews();
        state.MonitorViews["b"] = left.Id;

        Assert.Equal([left], WidgetLiveness.ShownViews(state, ["a", "b"]));
        Assert.Equal(left.Widgets, WidgetLiveness.Live(state, ["a", "b"], FeatureSet.None));
    }

    [Fact]
    public void S1880_a_display_without_its_own_view_shows_the_active_one()
    {
        var (state, left, _, _) = ThreeViews();
        state.ActiveViewId = left.Id;

        Assert.Equal([left], WidgetLiveness.ShownViews(state, ["new-display"]));
    }

    [Fact]
    public void S1880_no_display_means_nothing_runs()
    {
        var (state, _, _, _) = ThreeViews();

        Assert.Empty(WidgetLiveness.Live(state, [], FeatureSet.None));
    }

    [Fact]
    public void S1881_a_widget_that_needs_setting_up_does_not_run()
    {
        Assert.False(WidgetLiveness.Runs(Weather(placed: false).Config, FeatureSet.None));
        Assert.True(WidgetLiveness.Runs(Weather(placed: true).Config, FeatureSet.None));
        Assert.False(WidgetLiveness.Runs(new StocksConfig(), FeatureSet.None));
        Assert.False(WidgetLiveness.Runs(new RssConfig { Feeds = [] }, FeatureSet.None));
        Assert.False(WidgetLiveness.Runs(new EmailConfig(), FeatureSet.None));
        Assert.True(WidgetLiveness.Runs(new EmailConfig(), HostFeatures.Mail));
    }

    [Fact]
    public void S1881_every_kind_runs_exactly_when_it_needs_no_setup_except_the_ones_set_up_in_place()
    {
        foreach (var kind in ModuleCatalog.Widgets.Select(w => w.Id).ToArray())
        {
            var config = WidgetConfig.CreateDefault(kind);
            var expected = !WidgetSetup.Needed(config, FeatureSet.None) || kind == new WidgetId("shortcuts");
            Assert.True(WidgetLiveness.Runs(config, FeatureSet.None) == expected, kind.ToString());
        }
    }

    [Fact]
    public void S1881_an_empty_shortcuts_widget_keeps_running_to_take_a_drop()
    {
        var empty = new ShortcutsConfig { Items = [] };

        Assert.True(WidgetSetup.Needed(empty, FeatureSet.None));
        Assert.True(WidgetLiveness.Runs(empty, FeatureSet.None));
    }

    [Fact]
    public void S1881_an_unconfigured_widget_on_screen_is_not_live()
    {
        var view = ViewState.Create("Shown");
        var unplaced = Weather(placed: false);
        view.Widgets.Add(Clock());
        view.Widgets.Add(unplaced);
        var state = new AppState { Views = [view], ActiveViewId = view.Id };

        var live = WidgetLiveness.Live(state, ["a"], FeatureSet.None);

        Assert.Single(live);
        Assert.DoesNotContain(unplaced, live);
    }

    private sealed record Card(WidgetInstance Model);

    [Fact]
    public void S1882_a_view_switch_releases_every_old_card_and_builds_every_new_one()
    {
        var (_, left, right, _) = ThreeViews();
        var cards = left.Widgets.Select(w => new Card(w)).ToList();

        var change = WidgetLiveness.Reconcile(cards, c => c.Model, right.Widgets);

        Assert.Equal(cards, change.Release);
        Assert.All(change.Slots, s => Assert.Null(s.Existing));
        Assert.Equal(right.Widgets, change.Slots.Select(s => s.Model));
    }

    [Fact]
    public void S1882_a_card_is_kept_only_for_the_same_widget_object()
    {
        var kept = Clock();
        var gone = Clock();
        var restored = Clock();
        var cards = new List<Card> { new(kept), new(gone), new(restored) };

        // An undo brings "restored" back as a new object with the same id: its card is rebuilt.
        var again = new WidgetInstance { Id = restored.Id, X = 0.1, Y = 0.1, W = 0.2, H = 0.2, Config = restored.Config };
        var added = Clock();

        var change = WidgetLiveness.Reconcile(cards, c => c.Model, [kept, again, added]);

        Assert.Equal([cards[1], cards[2]], change.Release.OrderBy(c => cards.IndexOf(c)));
        Assert.Same(cards[0], change.Slots[0].Existing);
        Assert.Null(change.Slots[1].Existing);
        Assert.Same(again, change.Slots[1].Model);
        Assert.Null(change.Slots[2].Existing);
    }

    [Fact]
    public void S1882_nothing_changes_when_the_same_view_is_laid_out_again()
    {
        var (_, left, _, _) = ThreeViews();
        var cards = left.Widgets.Select(w => new Card(w)).ToList();

        var change = WidgetLiveness.Reconcile(cards, c => c.Model, left.Widgets);

        Assert.Empty(change.Release);
        Assert.Equal(cards, change.Slots.Select(s => s.Existing));
    }

    [Fact]
    public void S1882_a_duplicate_card_for_one_widget_is_released()
    {
        var widget = Clock();
        var cards = new List<Card> { new(widget), new(widget) };

        var change = WidgetLiveness.Reconcile(cards, c => c.Model, [widget]);

        Assert.Same(cards[1], Assert.Single(change.Release));
        Assert.Same(cards[0], Assert.Single(change.Slots).Existing);
    }

    [Fact]
    public void S1886_not_set_up_is_never_a_warning()
    {
        Assert.Equal(LogLevel.Debug, FetchState.LogLevelFor(FetchStatus.NotConfigured));
        Assert.Equal(LogLevel.Debug, FetchState.LogLevelFor(FetchStatus.Loading));
        Assert.Equal(LogLevel.Info, FetchState.LogLevelFor(FetchStatus.Ok));

        foreach (var status in new[] { FetchStatus.Offline, FetchStatus.Unreachable, FetchStatus.TimedOut, FetchStatus.RateLimited, FetchStatus.BadResponse, FetchStatus.Empty, FetchStatus.Refused })
        {
            Assert.Equal(LogLevel.Warn, FetchState.LogLevelFor(status));
        }
    }
}
