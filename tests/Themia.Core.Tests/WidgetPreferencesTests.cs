using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3770]..[S-3795] The settings every widget shares, and the per-widget ones they replaced.</summary>
public sealed class WidgetPreferencesTests
{
    private static readonly RegionValues Swiss = new(Use24Hour: true, WeekStartDay.Monday, TemperatureUnit.Celsius);

    // ---- [S-3772] regional formats ------------------------------------------------------------

    [Fact]
    public void S3772_follow_windows_takes_each_value_from_windows()
    {
        var us = new RegionValues(false, WeekStartDay.Sunday, TemperatureUnit.Fahrenheit);

        Assert.Equal(us, RegionFormat.Resolve(new RegionPreferences(), us));
        Assert.Equal(Swiss, RegionFormat.Resolve(new RegionPreferences(), Swiss));
    }

    [Fact]
    public void S3772_a_chosen_value_wins_over_windows_one_format_at_a_time()
    {
        var chosen = new RegionPreferences { Clock = ClockHours.Twelve, Temperature = TemperatureScale.Fahrenheit };

        Assert.Equal(new RegionValues(false, WeekStartDay.Monday, TemperatureUnit.Fahrenheit), RegionFormat.Resolve(chosen, Swiss));

        chosen = new RegionPreferences { Clock = ClockHours.TwentyFour, WeekStart = FirstWeekday.Sunday, Temperature = TemperatureScale.Celsius };
        Assert.Equal(new RegionValues(true, WeekStartDay.Sunday, TemperatureUnit.Celsius),
            RegionFormat.Resolve(chosen, new RegionValues(false, WeekStartDay.Monday, TemperatureUnit.Fahrenheit)));
    }

    [Theory]
    [InlineData("HH:mm", 0, 0, true, WeekStartDay.Monday, TemperatureUnit.Celsius)]
    [InlineData("h:mm tt", 6, 1, false, WeekStartDay.Sunday, TemperatureUnit.Fahrenheit)]
    [InlineData("tt hh:mm", 5, 0, false, WeekStartDay.Monday, TemperatureUnit.Celsius)]
    [InlineData("H.mm", 2, 0, true, WeekStartDay.Monday, TemperatureUnit.Celsius)]
    public void S3772_windows_raw_values_are_read(string pattern, int firstDay, int measure, bool use24, WeekStartDay start, TemperatureUnit unit)
        => Assert.Equal(new RegionValues(use24, start, unit), RegionFormat.FromWindows(pattern, firstDay, measure, Swiss));

    [Fact]
    public void S3772_an_unreadable_windows_value_falls_back_to_the_given_one()
    {
        var us = new RegionValues(false, WeekStartDay.Sunday, TemperatureUnit.Fahrenheit);

        Assert.Equal(us, RegionFormat.FromWindows(null, null, null, us));
        Assert.Equal(us, RegionFormat.FromWindows("mm:ss", 9, 7, us));
    }

    [Fact]
    public void S3772_the_language_fallback_is_its_own_habit()
    {
        Assert.Equal(new RegionValues(false, WeekStartDay.Sunday, TemperatureUnit.Celsius), RegionFormat.Language(Locale.En));
        Assert.Equal(new RegionValues(true, WeekStartDay.Monday, TemperatureUnit.Celsius), RegionFormat.Language(Locale.De));
    }

    [Fact]
    public void S3772_unknown_values_in_the_file_follow_windows_again_loudly()
    {
        var region = new RegionPreferences { Clock = (ClockHours)9, WeekStart = (FirstWeekday)9, Temperature = (TemperatureScale)9 };

        Assert.Equal(3, region.Repair().Count);
        Assert.Equal((ClockHours.System, FirstWeekday.System, TemperatureScale.System), (region.Clock, region.WeekStart, region.Temperature));
        Assert.Empty(region.Repair());
    }

    [Fact]
    public void S3772_null_preferences_in_the_file_are_the_defaults()
    {
        var parsed = AppStateStore.Parse("""
            { "schemaVersion": 1, "views": [ { "id": "v1", "name": "Desktop", "widgets": [] } ], "activeViewId": "v1",
              "monitorViews": {}, "preferences": { "region": null, "refresh": { "weatherMinutes": 1, "marketsMinutes": 5, "feedsMinutes": 30, "accountsMinutes": 2 } } }
            """);

        var preferences = parsed.State!.Preferences;
        Assert.Equal(ClockHours.System, preferences.Region.Clock);
        Assert.Equal(15, preferences.Refresh.WeatherMinutes);
        Assert.Contains(parsed.Repairs, r => r.Contains("region", StringComparison.Ordinal));
        Assert.Contains(parsed.Repairs, r => r.Contains("Weather", StringComparison.Ordinal));
    }

    [Fact]
    public void S3770_the_preferences_clone_whole()
    {
        var preferences = new AppPreferences
        {
            Region = new RegionPreferences { Clock = ClockHours.Twelve },
            Refresh = new RefreshPreferences { FeedsMinutes = 120 },
        };

        var copy = preferences.Clone();
        preferences.Region.Clock = ClockHours.TwentyFour;
        preferences.Refresh.FeedsMinutes = 15;

        Assert.Equal(ClockHours.Twelve, copy.Region.Clock);
        Assert.Equal(120, copy.Refresh.FeedsMinutes);
    }

    // ---- [S-3781] update cadences -------------------------------------------------------------

    [Fact]
    public void S3781_the_defaults_are_on_offered_choices()
    {
        var defaults = new RefreshPreferences();

        Assert.Equal(30, UpdateCadence.Minutes(defaults, UpdateSource.Weather));
        Assert.Equal(5, UpdateCadence.Minutes(defaults, UpdateSource.Markets));
        Assert.Equal(30, UpdateCadence.Minutes(defaults, UpdateSource.Feeds));
        Assert.Equal(2, UpdateCadence.Minutes(defaults, UpdateSource.Accounts));
        Assert.Empty(UpdateCadence.Repair(defaults));
    }

    [Theory]
    [InlineData(UpdateSource.Weather, 0, 15)]
    [InlineData(UpdateSource.Weather, 5, 15)]
    [InlineData(UpdateSource.Weather, 45, 60)]
    [InlineData(UpdateSource.Weather, 99999, 120)]
    [InlineData(UpdateSource.Markets, -3, 1)]
    [InlineData(UpdateSource.Markets, 3, 5)]
    [InlineData(UpdateSource.Accounts, 10, 15)]
    public void S3781_a_value_off_the_list_snaps_to_the_nearest_choice_ties_going_slower(UpdateSource source, int stored, int expected)
    {
        var preferences = new RefreshPreferences();
        UpdateCadence.Set(preferences, source, stored);

        Assert.Equal(expected, UpdateCadence.Minutes(preferences, source));
        Assert.Equal(TimeSpan.FromMinutes(expected), UpdateCadence.Interval(preferences, source));
    }

    [Fact]
    public void S3781_repair_says_what_it_moved()
    {
        var preferences = new RefreshPreferences { WeatherMinutes = 1, MarketsMinutes = 0 };

        Assert.Equal(2, UpdateCadence.Repair(preferences).Count);
        Assert.Equal((15, 1), (preferences.WeatherMinutes, preferences.MarketsMinutes));
    }

    // ---- [S-3790] the market comes with the symbol ---------------------------------------------

    [Fact]
    public void S3790_a_search_pick_sets_symbol_market_and_name_together()
    {
        var config = new StocksConfig();
        config.Choose(new SymbolHit("bitcoin", "Bitcoin", QuoteMarket.Crypto, AssetClass.Crypto, string.Empty));

        Assert.Equal(("bitcoin", QuoteMarket.Crypto, "Bitcoin"), (config.Symbol, config.Market, config.DisplayName));
    }

    [Fact]
    public void S3790_a_typed_symbol_is_listed_and_drops_the_old_name()
    {
        var config = new StocksConfig();
        config.Choose(new SymbolHit("bitcoin", "Bitcoin", QuoteMarket.Crypto, AssetClass.Crypto, string.Empty));

        config.ChooseTyped(" BTC-USD ");

        Assert.Equal(("BTC-USD", QuoteMarket.Listed, string.Empty), (config.Symbol, config.Market, config.DisplayName));
    }

    [Fact]
    public void S3790_retyping_the_same_symbol_changes_nothing()
    {
        var config = new StocksConfig();
        config.Choose(new SymbolHit("MSFT", "Microsoft Corporation", QuoteMarket.Listed, AssetClass.Equity, "NASDAQ"));

        config.ChooseTyped("MSFT");

        Assert.Equal("Microsoft Corporation", config.DisplayName);
    }

    // ---- removed per-widget fields ---------------------------------------------------------------

    [Fact]
    public void S3796_fields_that_moved_to_the_shared_settings_are_ignored_in_a_widget()
    {
        var clock = (ClockConfig)ConfigJson.Read("Clock", """{ "kind": "clock", "use24Hour": false, "showSeconds": true }""");
        Assert.True(clock.ShowSeconds);

        var weather = (WeatherNowConfig)ConfigJson.Read("WeatherNow", """{ "kind": "weatherNow", "latitude": 1, "longitude": 2, "unit": "Fahrenheit", "refreshMinutes": 5 }""");
        Assert.True(weather.HasLocation);
        Assert.Equal(UpdateCadence.Interval(UpdateSource.Weather), weather.TickInterval);

        var tasks = (TasksConfig)ConfigJson.Read("Tasks", """{ "kind": "tasks", "refreshSeconds": 30, "showCompleted": true }""");
        Assert.True(tasks.ShowCompleted);
        Assert.Equal(UpdateCadence.Interval(UpdateSource.Accounts), tasks.RefreshInterval);
    }

    [Fact]
    public void S3796_the_calendar_week_start_is_the_shared_one()
        => Assert.Equal(RegionFormat.Current.WeekStart, new MonthCalendarConfig().EffectiveWeekStart);
}
