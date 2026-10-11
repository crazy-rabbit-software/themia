using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2663] web sync interests, [S-2664] the calendar's stored-copy stamp, [S-2665]/[S-2666] the start-up sweep.</summary>
public sealed class SyncHousekeepingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "themia-sweep-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static byte[] Key() => Enumerable.Repeat((byte)7, SealedBlob.KeySize).ToArray();

    private static MailShelf Mail(string owner) => new() { Owner = owner };

    // ---- [S-2663] weather and stocks in every view ---------------------------------------------

    private static AppState State(params WidgetConfig[][] views) => new()
    {
        Views =
        [
            .. views.Select((configs, v) => new ViewState
            {
                Id = $"v{v}",
                Name = $"View {v}",
                Widgets = [.. configs.Select((c, i) => new WidgetInstance { Id = $"w{v}-{i}", Kind = c.Kind, Config = c })],
            }),
        ],
    };

    [Fact]
    public void S2663_weather_and_stocks_widgets_of_every_view_are_synced_and_unset_ones_ask_nothing()
    {
        var now = new WeatherNowConfig { Latitude = 47.37, Longitude = 8.54 };
        var days = new WeatherForecastConfig { Latitude = 47.37, Longitude = 8.54, Days = 30 };
        var stock = new StocksConfig { Symbol = "aapl", Range = QuoteRange.FiveDays };
        var state = State(
            [now, new WeatherNowConfig(), new WeatherNowConfig { Latitude = 120, Longitude = 8 }],
            [days, stock, new StocksConfig(), new StocksConfig { Symbol = "not a symbol!" }]);

        var interests = SyncInterests.From(state);

        Assert.Equal(2, interests.Weather.Count);
        var reading = Assert.Single(interests.Weather, w => w.ForecastDays is null);
        Assert.Equal((47.37, 8.54, TemperatureUnit.Celsius, TimeSpan.FromMinutes(30)), (reading.Latitude, reading.Longitude, reading.Unit, reading.Interval));
        Assert.Equal(14, Assert.Single(interests.Weather, w => w.ForecastDays is not null).ForecastDays);

        var quote = Assert.Single(interests.Quotes);
        Assert.Equal((stock.EffectiveSymbol, QuoteMarket.Listed, QuoteRange.FiveDays, false), (quote.Symbol, quote.Market, quote.Range, quote.PrePost));
    }

    [Fact]
    public void S2663_the_widget_asks_exactly_what_the_sync_keeps_and_a_change_of_place_or_range_is_heard()
    {
        var now = new WeatherNowConfig { Latitude = 1, Longitude = 2 };
        var stock = new StocksConfig { Symbol = "MSFT" };
        var state = State([now, stock]);

        var interests = SyncInterests.From(state);
        Assert.Equal(SyncInterests.For(now), Assert.Single(interests.Weather));
        Assert.Equal(SyncInterests.For(stock), Assert.Single(interests.Quotes));
        Assert.Null(SyncInterests.For(new WeatherForecastConfig()));
        Assert.Null(SyncInterests.For(new StocksConfig { Symbol = "   " }));

        var key = interests.Key();
        state.Preferences.Region.Temperature = TemperatureScale.Fahrenheit;
        Assert.NotEqual(key, SyncInterests.From(state).Key());
        Assert.Equal(TemperatureUnit.Fahrenheit, Assert.Single(SyncInterests.From(state).Weather).Unit);

        key = SyncInterests.From(state).Key();
        stock.Range = QuoteRange.Year;
        Assert.NotEqual(key, SyncInterests.From(state).Key());

        // A colour is drawing only: nothing to sync changes.
        key = SyncInterests.From(state).Key();
        stock.UpColor = "#00FF00";
        Assert.Equal(key, SyncInterests.From(state).Key());
    }

    [Fact]
    public void S3780_the_shared_cadences_reach_every_interest_and_a_change_is_heard()
    {
        var state = State([new WeatherNowConfig { Latitude = 1, Longitude = 2 }, new StocksConfig { Symbol = "MSFT" }, new RssConfig { Feeds = [new RssFeed { Url = "https://example.com/feed.xml" }] }]);
        state.Preferences.Refresh = new RefreshPreferences { WeatherMinutes = 60, MarketsMinutes = 15, FeedsMinutes = 120, AccountsMinutes = 5 };

        var interests = SyncInterests.From(state);
        Assert.Equal(TimeSpan.FromMinutes(60), Assert.Single(interests.Weather).Interval);
        Assert.Equal(TimeSpan.FromMinutes(15), Assert.Single(interests.Quotes).Interval);
        Assert.Equal(TimeSpan.FromMinutes(120), Assert.Single(interests.Feeds).Interval);
        Assert.Equal(TimeSpan.FromMinutes(5), interests.AccountInterval);

        var key = interests.Key();
        state.Preferences.Refresh.AccountsMinutes = 15;
        Assert.NotEqual(key, SyncInterests.From(state).Key());
    }

    // ---- [S-2664] the calendar's stored copies -------------------------------------------------

    [Fact]
    public void S2664_problems_fold_per_account_stamped_with_the_oldest_stored_copy()
    {
        var older = new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
        var newer = older.AddHours(1);

        var folded = SyncFreshness.Fold(
        [
            ("work", SyncProblem.Offline, newer),
            ("home", SyncProblem.Offline, null),
            ("work", SyncProblem.Offline, older),
            ("work", SyncProblem.Offline, null),
            ("home", SyncProblem.Offline, newer),
            ("work", SyncProblem.Unreachable, null),
        ]);

        Assert.Equal(
            [("work", SyncProblem.Offline, (DateTimeOffset?)older), ("home", SyncProblem.Offline, newer), ("work", SyncProblem.Unreachable, null)],
            folded);
    }

    [Fact]
    public void S2664_a_stored_calendar_says_when_it_is_from_and_one_with_nothing_stored_says_only_why()
    {
        var at = new DateTimeOffset(2026, 10, 11, 10, 42, 0, TimeSpan.Zero);
        var now = at.AddMinutes(30);
        var problem = SyncProblems.Message(SyncProblem.Offline, SyncSubject.Calendar, Locale.En);

        Assert.Equal($"{problem} · updated {LocaleFormat.Time(Locale.En, at.DateTime)}", SyncFreshness.Stamp(Locale.En, problem, at, now));
        Assert.Equal(problem, SyncFreshness.Stamp(Locale.En, problem, null, now));
    }

    // ---- [S-2665] [S-2666] the start-up sweep --------------------------------------------------

    [Fact]
    public void S2665_orphans_are_shelves_of_no_existing_account_and_nothing_else()
    {
        var kept = SyncVault.ShelfName(SyncKind.Calendar, "acc-1");
        var gone = SyncVault.ShelfName(SyncKind.Mail, "acc-gone");
        var goneTasks = SyncVault.ShelfName(SyncKind.Tasks, "acc-gone");
        var web = SyncVault.ShelfName(SyncKind.Web, WebShelf.WeatherOwner);
        var webNetwork = SyncVault.ShelfName(SyncKind.Web, WebShelf.NetworkOwner);

        var orphans = ShelfSweep.Orphans(
            [kept, kept + ".corrupt", gone, goneTasks + ".corrupt", web, webNetwork, "vault.key", "vault.key.corrupt", gone + ".123.tmp", "notes.txt", "mail-xyz.shelf"],
            ["acc-1"]);

        Assert.Equal([gone, goneTasks + ".corrupt"], orphans);
        Assert.Equal("mail", ShelfSweep.KindOf(gone));
    }

    [Fact]
    public void S2666_only_an_account_list_that_loaded_whole_may_say_who_is_gone()
    {
        Assert.True(ShelfSweep.MayRun(new AccountLoad([], LoadOutcome.Loaded, null)));

        Assert.False(ShelfSweep.MayRun(new AccountLoad([], LoadOutcome.Fresh, null)));
        Assert.False(ShelfSweep.MayRun(new AccountLoad([], LoadOutcome.Recovered, "the accounts file is not valid JSON")));
        Assert.False(ShelfSweep.MayRun(new AccountLoad([], LoadOutcome.Recovered, "the accounts file could not be read", Writable: false)));
        Assert.False(ShelfSweep.MayRun(new AccountLoad([], LoadOutcome.Loaded, "dropped an account without an id")));
        Assert.False(ShelfSweep.MayRun(new AccountLoad([], LoadOutcome.Loaded, null, Writable: false)));
    }

    [Fact]
    public async Task S2665_the_sweep_deletes_the_shelves_of_a_signed_out_account_and_keeps_the_rest()
    {
        var vault = new SyncVault(_dir, Key());
        vault.SaveSoon(SyncKind.Mail, "a", Mail("a"), SyncJsonContext.Default.MailShelf);
        vault.SaveSoon(SyncKind.Mail, "b", Mail("b"), SyncJsonContext.Default.MailShelf);
        vault.SaveSoon(SyncKind.Web, WebShelf.NetworkOwner, new WebShelf { Owner = WebShelf.NetworkOwner, Json = "{}" }, SyncJsonContext.Default.WebShelf);
        await vault.FlushAsync();
        File.WriteAllText(vault.ShelfPath(SyncKind.Calendar, "b")! + ".corrupt", "x");
        File.WriteAllText(Path.Combine(_dir, "vault.key"), "key");

        vault.Sweep(() => ["a"]);
        await vault.FlushAsync();

        Assert.True(File.Exists(vault.ShelfPath(SyncKind.Mail, "a")));
        Assert.True(File.Exists(vault.ShelfPath(SyncKind.Web, WebShelf.NetworkOwner)));
        Assert.True(File.Exists(Path.Combine(_dir, "vault.key")));
        Assert.False(File.Exists(vault.ShelfPath(SyncKind.Mail, "b")));
        Assert.False(File.Exists(vault.ShelfPath(SyncKind.Calendar, "b") + ".corrupt"));
        Assert.Equal(ShelfReadOutcome.Loaded, vault.Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version).Outcome);
    }

    [Fact]
    public async Task S2666_an_untrusted_account_list_sweeps_nothing()
    {
        var vault = new SyncVault(_dir, Key());
        vault.SaveSoon(SyncKind.Mail, "b", Mail("b"), SyncJsonContext.Default.MailShelf);
        await vault.FlushAsync();

        vault.Sweep(() => null);
        await vault.FlushAsync();

        Assert.True(File.Exists(vault.ShelfPath(SyncKind.Mail, "b")));
    }
}
