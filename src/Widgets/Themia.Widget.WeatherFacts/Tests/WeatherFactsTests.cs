using System.Text.Json;
using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.WeatherFacts.Tests;

/// <summary>[S-722] [S-11105]..[S-11107] The weather facts: its readings, its pick, its sizing and its menu.</summary>
public sealed class WeatherFactsTests
{
    private static readonly WidgetDescriptor Kind = new WeatherFactsModule().Descriptor;

    private static string Value(CurrentWeather reading, WeatherFactKind kind, Locale locale = Locale.En)
        => WeatherFactRules.Value(locale, kind, reading, TemperatureUnit.Celsius);

    // ---- [S-722] the readings ---------------------------------------------------------------------

    [Fact]
    public void S722_each_picked_fact_is_one_row_in_the_pick_s_order_and_a_missing_reading_is_the_dash()
    {
        var reading = new CurrentWeather(18, 17, 63, 11, 2) { WindDirection = 315, Pressure = null, UvIndex = 3 };

        var facts = WeatherFactRules.For(Locale.En, [WeatherFactKind.Wind, WeatherFactKind.Pressure, WeatherFactKind.Today], reading, TemperatureUnit.Celsius);

        Assert.Equal([WeatherFactKind.Wind, WeatherFactKind.Pressure, WeatherFactKind.Today], facts.Select(f => f.Kind));
        Assert.Equal(["11 km/h NW", "—", "—"], facts.Select(f => f.Value));
    }

    [Fact]
    public void S722_today_s_range_is_written_high_then_low()
    {
        var today = new ForecastDay(new DateOnly(2026, 10, 4), 2, 8, 16, 0);
        var reading = new CurrentWeather(14, 13, 71, 13, 2) { Days = [today] };

        Assert.Equal(("Today", "H 16°  L 8°"), (WeatherFactRules.Name(WeatherFactKind.Today, Locale.En), Value(reading, WeatherFactKind.Today)));
    }

    [Fact]
    public void S722_precipitation_is_the_amount_while_it_rains_and_today_s_chance_otherwise()
    {
        var today = new ForecastDay(new DateOnly(2026, 10, 4), 61, 9, 15, 3) { PrecipitationChance = 80 };
        var dry = new CurrentWeather(15, 14, 80, 5, 61) { Days = [today] };
        var wet = dry with { Precipitation = 0.6 };

        Assert.Equal("80%", Value(dry, WeatherFactKind.Precipitation));
        Assert.Equal("0.6 mm", Value(wet, WeatherFactKind.Precipitation));
        Assert.Equal("0,6 mm", Value(wet, WeatherFactKind.Precipitation, Locale.De));
    }

    [Fact]
    public void S722_sunrise_and_sunset_come_from_today_on_the_language_s_clock()
    {
        var today = new ForecastDay(new DateOnly(2026, 10, 4), 0, 9, 15, 0) { Sunrise = new TimeOnly(7, 31), Sunset = new TimeOnly(18, 54) };
        var reading = new CurrentWeather(15, 14, 50, 5, 0) { Days = [today] };

        Assert.Equal("7:31 AM", Value(reading, WeatherFactKind.Sunrise));
        Assert.Equal("18:54", Value(reading, WeatherFactKind.Sunset, Locale.De));
    }

    [Fact]
    public void S722_the_names_are_in_the_language_asked_for()
    {
        Assert.Equal(["Gefühlt", "Luftfeuchte", "Wind", "Luftdruck"],
            new[] { WeatherFactKind.FeelsLike, WeatherFactKind.Humidity, WeatherFactKind.Wind, WeatherFactKind.Pressure }.Select(k => WeatherFactRules.Name(k, Locale.De)));
    }

    // ---- [S-11105] [S-11106] the pick and the sizing ----------------------------------------------

    [Fact]
    public void S11105_a_new_card_shows_today_feels_like_humidity_and_wind()
        => Assert.Equal([WeatherFactKind.Today, WeatherFactKind.FeelsLike, WeatherFactKind.Humidity, WeatherFactKind.Wind], new WeatherFactsConfig().Facts);

    [Fact]
    public void S11105_a_stored_pick_is_kept_in_order_and_survives_the_state_file()
    {
        var config = new WeatherFactsConfig { Facts = [WeatherFactKind.Uv, WeatherFactKind.Wind] };
        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);

        Assert.Contains("\"uv\"", json, StringComparison.OrdinalIgnoreCase);
        var back = (WeatherFactsConfig)JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!;
        Assert.Equal([WeatherFactKind.Uv, WeatherFactKind.Wind], back.Facts);
        Assert.Equal([WeatherFactKind.Uv, WeatherFactKind.Wind], ((WeatherFactsConfig)config.Clone()).Facts);
        Assert.NotSame(config.Facts, ((WeatherFactsConfig)config.Clone()).Facts);
    }

    [Fact]
    public void S11105_an_empty_or_doubled_pick_is_repaired()
    {
        var empty = new WeatherFactsConfig { Facts = [] };
        Assert.NotEmpty(empty.Repair());
        Assert.Equal(WeatherFactRules.Defaults, empty.Facts);

        var twice = new WeatherFactsConfig { Facts = [WeatherFactKind.Wind, WeatherFactKind.Wind, (WeatherFactKind)99] };
        Assert.NotEmpty(twice.Repair());
        Assert.Equal([WeatherFactKind.Wind], twice.Facts);
        Assert.Empty(new WeatherFactsConfig().Repair());
    }

    [Fact]
    public void S11106_its_height_is_a_28_row_a_fact_under_the_header_and_its_width_200_to_420()
    {
        var sizing = Kind.Sizing(new WeatherFactsConfig());

        Assert.Equal(new SizeDip(240, 36 + (4 * 28) + 12), sizing.Natural);
        Assert.Equal((200.0, 420.0), (sizing.Min.Width, sizing.Max.Width));
        Assert.Equal((36 + 28 + 12.0, 36 + (11 * 28) + 12.0), (sizing.Min.Height, sizing.Max.Height));
        Assert.Equal(36 + (2 * 28) + 12, Kind.Sizing(new WeatherFactsConfig { Facts = [WeatherFactKind.Uv, WeatherFactKind.Wind] }).Natural.Height);
    }

    [Fact]
    public void S11106_dragging_taller_adds_the_next_facts_not_picked_and_shorter_keeps_the_first()
    {
        var config = new WeatherFactsConfig { Facts = [WeatherFactKind.Uv, WeatherFactKind.Today] };

        var taller = (WeatherFactsConfig)Kind.SizeSets!(config, new SizeDip(240, 36 + (4 * 28) + 12));
        var shorter = (WeatherFactsConfig)Kind.SizeSets(config, new SizeDip(240, 36 + 28 + 12));

        Assert.Equal([WeatherFactKind.Uv, WeatherFactKind.Today, WeatherFactKind.FeelsLike, WeatherFactKind.Humidity], taller.Facts);
        Assert.Equal([WeatherFactKind.Uv], shorter.Facts);
        Assert.Equal([WeatherFactKind.Uv, WeatherFactKind.Today], config.Facts);
        Assert.Same(config, Kind.SizeSets(config, new SizeDip(300, 36 + (2 * 28) + 12)));
    }

    // ---- [S-11107] the menu ---------------------------------------------------------------------

    [Fact]
    public void S11107_the_menu_is_the_facts_then_the_place_then_the_links()
    {
        var config = new WeatherFactsConfig();
        var menu = new WidgetMenuLines(() => { }, _ => { });
        WeatherFactsMenu.Lines(menu, config);

        Assert.Equal(WeatherFactsStr.WeatherFacts_Menu_Facts.Text(), menu.Lines[0].Text);
        Assert.True(menu.Lines[1].IsSeparator);
        Assert.Contains(menu.Lines, l => l.Search is not null);
        Assert.Equal(Str.EditBar_Link_UpdateInterval.Text(), menu.Lines[^1].Text);
        Assert.Empty(menu.StyleLines);
    }

    [Fact]
    public void S11107_the_facts_line_lists_the_pick_and_adds_each_fact_not_picked()
    {
        var config = new WeatherFactsConfig();
        var saves = 0;
        var menu = new WidgetMenuLines(() => saves++, _ => { });
        WeatherFactsMenu.Lines(menu, config);
        var facts = menu.Lines[0].Children!;

        var add = facts.Single(l => l.Text == WeatherFactRules.Name(WeatherFactKind.Pressure));
        add.Action!();

        Assert.Equal([.. WeatherFactRules.Defaults, WeatherFactKind.Pressure], config.Facts);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void S11107_a_click_opens_the_forecast_for_its_place_and_nothing_without_one()
    {
        var config = new WeatherFactsConfig { Latitude = 47.3769, Longitude = 8.5417, LocationName = "Zurich, Switzerland" };

        Assert.StartsWith("https://www.msn.com/en-us/weather/forecast/in-Zurich?loc=", WeatherFactsModule.Opens(config, TemperatureUnit.Celsius, Locale.En)!.Value, StringComparison.Ordinal);
        Assert.Null(WeatherFactsModule.Opens(new WeatherFactsConfig(), TemperatureUnit.Celsius, Locale.En));
    }
}
