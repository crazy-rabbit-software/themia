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

/// <summary>[S-722] [S-13900]..[S-13903] The weather facts family: a kind a fact, its reading, its sizing and its menu.</summary>
public sealed class WeatherFactTests
{
    private static readonly string[] Ids =
    [
        "weatherHighLow", "weatherFeelsLike", "weatherHumidity", "weatherWind", "weatherPrecipitation", "weatherPressure",
        "weatherUv", "weatherCloudCover", "weatherGusts", "weatherSunrise", "weatherSunset",
    ];

    private static WidgetDescriptor Kind(string id) => ModuleCatalog.Of(new WidgetId(id));

    private static string Value(CurrentWeather reading, WeatherFactKind kind, Locale locale = Locale.En)
        => WeatherFactRules.Value(locale, kind, reading, TemperatureUnit.Celsius);

    // ---- [S-13900] the family -----------------------------------------------------------------------

    [Fact]
    public void S13900_every_fact_is_a_kind_of_its_own_in_the_facts_order()
    {
        Assert.Equal(WeatherFactRules.All.Count, Ids.Length);
        for (var i = 0; i < Ids.Length; i++)
        {
            var kind = Kind(Ids[i]);
            var config = Assert.IsAssignableFrom<WeatherFactConfig>(kind.CreateDefault());
            Assert.Equal(WeatherFactRules.All[i], config.Fact);
            Assert.Equal(kind.ConfigType, config.GetType());
            Assert.Equal(WeatherFactView.FactGlyph(config.Fact), kind.Glyph);
            Assert.Contains("weather", kind.Keywords);
            Assert.Equal(new AddPlacement(AddFolderId.WeatherFacts, 0, i), kind.Add);
        }

        Assert.Equal(Ids.Length, Ids.Select(id => Kind(id).Label.Text(Locale.En)).Distinct().Count());
        Assert.Equal(Ids.Length, Ids.Select(id => Kind(id).ConfigType).Distinct().Count());
    }

    [Fact]
    public void S13900_the_kinds_are_named_after_their_fact()
    {
        Assert.Equal(
            ["High and low", "Feels like", "Humidity", "Wind", "Precipitation", "Pressure", "UV index", "Cloud cover", "Gusts", "Sunrise", "Sunset"],
            Ids.Select(id => Kind(id).Label.Text(Locale.En)));
        Assert.Equal(["Luftfeuchte", "Sonnenaufgang"], new[] { "weatherHumidity", "weatherSunrise" }.Select(id => Kind(id).Label.Text(Locale.De)));
    }

    [Fact]
    public void S13900_the_facts_widget_is_gone_and_its_family_is_one_home_under_weather()
    {
        Assert.False(ModuleCatalog.TryGet(new WidgetId("weatherFacts"), out _));

        var weather = AddTree.Browse.Rows.OfType<AddShelf>().Single(s => s.Id == AddFolderId.Weather);
        var family = weather.Rows.OfType<AddShelf>().Single(s => s.Id == AddFolderId.WeatherFacts);
        Assert.Equal("Weather facts", family.Title.Text(Locale.En));
        Assert.Equal(Ids, family.Rows.OfType<AddLeaf>().Select(l => l.Kind.Value));
        Assert.Equal(AddFolderId.Weather, AddTree.SubjectOf(AddFolderId.WeatherFacts));

        // Each at home there alone: no other folder of the tree lists it but as Popular's again.
        static IEnumerable<AddLeaf> Leaves(AddShelf shelf) => shelf.Rows.SelectMany(r => r switch
        {
            AddLeaf leaf => [leaf],
            AddShelf inner => Leaves(inner),
            _ => [],
        });
        var homes = AddTree.Top.SelectMany(Leaves).Where(l => !l.Again).Select(l => l.Kind.Value).ToList();
        Assert.All(Ids, id => Assert.Single(homes, h => h == id));
    }

    [Fact]
    public void S13900_a_fact_card_keeps_its_place_through_the_state_file_and_a_copy()
    {
        WidgetConfig config = new WeatherWindConfig { Latitude = 47.3769, Longitude = 8.5417, LocationName = "Zurich, Switzerland" };
        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);

        Assert.Contains("\"kind\": \"weatherWind\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("fact", json, StringComparison.OrdinalIgnoreCase);
        var back = Assert.IsType<WeatherWindConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(("Zurich, Switzerland", WeatherFactKind.Wind), (back.LocationName, back.Fact));
        var copy = Assert.IsType<WeatherWindConfig>(config.Clone());
        Assert.NotSame(config, copy);
        Assert.Equal(47.3769, copy.Latitude);
    }

    // ---- [S-13901] the sizing ------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllIds))]
    public void S13901_its_one_28_row_under_and_over_the_inset_200_wide_at_least_and_relaxed(string id)
    {
        var sizing = Kind(id).Sizing(Kind(id).CreateDefault());

        Assert.Equal(new SizeDip(240, 12 + 28 + 12), sizing.Natural);
        Assert.Equal(new SizeDip(200, 52), sizing.Min);
        Assert.Equal((SizeStretch.Ceiling, SizeStretch.Ceiling), (sizing.Max.Width, sizing.Max.Height)); // relaxed, [S-12411]
        Assert.Null(sizing.HeightSteps);
        Assert.Null(Kind(id).SizeSets);
    }

    public static TheoryData<string> AllIds() => [.. Ids];

    // ---- [S-722] [S-13902] the reading ---------------------------------------------------------------

    [Fact]
    public void S13902_without_a_reading_its_row_keeps_its_label_and_the_default()
    {
        Assert.All(WeatherFactRules.All, kind =>
        {
            var fact = WeatherFactRules.Unread(Locale.En, kind);
            Assert.Equal(("--", WeatherFactRules.Name(kind, Locale.En)), (fact.Value, fact.Label));
        });
    }

    [Fact]
    public void S722_a_fact_is_its_name_and_its_value_and_a_missing_reading_is_the_dash()
    {
        var reading = new CurrentWeather(18, 17, 63, 11, 2) { WindDirection = 315, Pressure = null, UvIndex = 3 };

        Assert.Equal(new WeatherFact(WeatherFactKind.Wind, "Wind", "11 km/h NW"), WeatherFactRules.For(Locale.En, WeatherFactKind.Wind, reading, TemperatureUnit.Celsius));
        Assert.Equal("—", Value(reading, WeatherFactKind.Pressure));
        Assert.Equal("—", Value(reading, WeatherFactKind.Today));
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

    // ---- [S-13903] the menu and the click ------------------------------------------------------------

    [Fact]
    public void S13903_the_menu_is_the_place_alone_no_pick_of_facts()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        WeatherFactMenu.Lines(menu, new WeatherHumidityConfig());

        Assert.Contains(menu.Lines, l => l.Search is not null);
        Assert.Equal(Str.Shell_Place_Location.Text(), menu.Lines[^1].Text);
        Assert.Equal(WeatherPlaceRows.Lines(new WidgetMenuLines(() => { }, _ => { }), new WeatherHumidityConfig()).Select(l => l.Text), menu.Lines.Select(l => l.Text));
        Assert.DoesNotContain(menu.Lines, l => l.LinkTo is not null);
        Assert.Empty(menu.StyleLines);
    }

    [Fact]
    public void S13903_a_click_opens_the_forecast_for_its_place_and_nothing_without_one()
    {
        var config = new WeatherUvConfig { Latitude = 47.3769, Longitude = 8.5417, LocationName = "Zurich, Switzerland" };

        Assert.StartsWith("https://www.msn.com/en-us/weather/forecast/in-Zurich?loc=", WeatherFactModule.Opens(config, TemperatureUnit.Celsius, Locale.En)!.Value, StringComparison.Ordinal);
        Assert.Null(WeatherFactModule.Opens(new WeatherUvConfig(), TemperatureUnit.Celsius, Locale.En));
    }
}
