using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.WeatherFacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("weatherTemperature", typeof(WeatherTemperatureModule))]
[assembly: ThemiaWidget("weatherHighLow", typeof(WeatherHighLowModule))]
[assembly: ThemiaWidget("weatherFeelsLike", typeof(WeatherFeelsLikeModule))]
[assembly: ThemiaWidget("weatherHumidity", typeof(WeatherHumidityModule))]
[assembly: ThemiaWidget("weatherWind", typeof(WeatherWindModule))]
[assembly: ThemiaWidget("weatherPrecipitation", typeof(WeatherPrecipitationModule))]
[assembly: ThemiaWidget("weatherPressure", typeof(WeatherPressureModule))]
[assembly: ThemiaWidget("weatherUv", typeof(WeatherUvModule))]
[assembly: ThemiaWidget("weatherCloudCover", typeof(WeatherCloudCoverModule))]
[assembly: ThemiaWidget("weatherGusts", typeof(WeatherGustsModule))]
[assembly: ThemiaWidget("weatherSunrise", typeof(WeatherSunriseModule))]
[assembly: ThemiaWidget("weatherSunset", typeof(WeatherSunsetModule))]

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13900] [S-14604] The family's kinds (owner, 2026-11-19: "these should actually be separate widgets, all of them"),
/// one single metric a fact, in the facts' order (<see cref="WeatherFactKind"/>): each one's id, its row's name, its
/// name, description and search words, its glyph and what history its reading has (the service's hours, [S-14603]).
/// </summary>
internal static class WeatherFactKinds
{
    public static IReadOnlyList<MetricKind> All { get; } =
    [
        Kind(WeatherFactKind.Temperature, "weatherTemperature", WeatherFactsStr.WeatherFacts_Temperature_Label.Local(), WeatherFactsStr.WeatherFacts_Temperature_Label.Local(), WeatherFactsStr.WeatherFacts_Temperature_Desc.Local(),
            ["temperature", "degrees", "warm", "cold", "graph", "hourly", "weather"], () => new WeatherTemperatureConfig()),
        Kind(WeatherFactKind.Today, "weatherHighLow", Str.Common_Today.Local(), WeatherFactsStr.WeatherFacts_HighLow_Label.Local(), WeatherFactsStr.WeatherFacts_HighLow_Desc.Local(),
            ["high", "low", "today", "temperature", "range", "max", "min", "weather"], () => new WeatherHighLowConfig()),
        Kind(WeatherFactKind.FeelsLike, "weatherFeelsLike", WeatherFactsStr.Weather_FeelsLike.Local(), WeatherFactsStr.Weather_FeelsLike.Local(), WeatherFactsStr.WeatherFacts_FeelsLike_Desc.Local(),
            ["feels like", "apparent temperature", "wind chill", "heat index", "temperature", "weather"], () => new WeatherFeelsLikeConfig()),
        Kind(WeatherFactKind.Humidity, "weatherHumidity", WeatherFactsStr.Weather_Humidity.Local(), WeatherFactsStr.Weather_Humidity.Local(), WeatherFactsStr.WeatherFacts_Humidity_Desc.Local(),
            ["humidity", "humid", "moisture", "damp", "weather"], () => new WeatherHumidityConfig()),
        Kind(WeatherFactKind.Wind, "weatherWind", WeatherFactsStr.Weather_Wind.Local(), WeatherFactsStr.Weather_Wind.Local(), WeatherFactsStr.WeatherFacts_Wind_Desc.Local(),
            ["wind", "wind speed", "wind direction", "breeze", "weather"], () => new WeatherWindConfig()),
        Kind(WeatherFactKind.Precipitation, "weatherPrecipitation", WeatherFactsStr.WidgetsC_Weather_Fact_Precipitation.Local(), WeatherFactsStr.WidgetsC_Weather_Fact_Precipitation.Local(), WeatherFactsStr.WeatherFacts_Precipitation_Desc.Local(),
            ["precipitation", "rain", "chance of rain", "snow", "weather"], () => new WeatherPrecipitationConfig()),
        Kind(WeatherFactKind.Pressure, "weatherPressure", WeatherFactsStr.WidgetsC_Weather_Fact_Pressure.Local(), WeatherFactsStr.WidgetsC_Weather_Fact_Pressure.Local(), WeatherFactsStr.WeatherFacts_Pressure_Desc.Local(),
            ["pressure", "air pressure", "barometer", "hpa", "weather"], () => new WeatherPressureConfig()),
        Kind(WeatherFactKind.Uv, "weatherUv", WeatherFactsStr.WidgetsC_Weather_Fact_UvIndex.Local(), WeatherFactsStr.WidgetsC_Weather_Fact_UvIndex.Local(), WeatherFactsStr.WeatherFacts_Uv_Desc.Local(),
            ["uv", "uv index", "sunburn", "sun", "weather"], () => new WeatherUvConfig()),
        Kind(WeatherFactKind.CloudCover, "weatherCloudCover", WeatherFactsStr.WidgetsC_Weather_Fact_CloudCover.Local(), WeatherFactsStr.WidgetsC_Weather_Fact_CloudCover.Local(), WeatherFactsStr.WeatherFacts_CloudCover_Desc.Local(),
            ["cloud cover", "clouds", "cloudy", "overcast", "weather"], () => new WeatherCloudCoverConfig()),
        Kind(WeatherFactKind.Gusts, "weatherGusts", WeatherFactsStr.WidgetsC_Weather_Fact_Gusts.Local(), WeatherFactsStr.WidgetsC_Weather_Fact_Gusts.Local(), WeatherFactsStr.WeatherFacts_Gusts_Desc.Local(),
            ["gusts", "wind gusts", "wind", "storm", "weather"], () => new WeatherGustsConfig()),
        Kind(WeatherFactKind.Sunrise, "weatherSunrise", Str.WidgetUi_Sunrise.Local(), WeatherFactsStr.WeatherFacts_Sunrise_Label.Local(), WeatherFactsStr.WeatherFacts_Sunrise_Desc.Local(),
            ["sunrise", "dawn", "morning", "sun", "weather"], () => new WeatherSunriseConfig()),
        Kind(WeatherFactKind.Sunset, "weatherSunset", Str.WidgetUi_Sunset.Local(), WeatherFactsStr.WeatherFacts_Sunset_Label.Local(), WeatherFactsStr.WeatherFacts_Sunset_Desc.Local(),
            ["sunset", "dusk", "evening", "sun", "weather"], () => new WeatherSunsetConfig()),
    ];

    /// <summary>The kind showing <paramref name="fact"/>.</summary>
    public static MetricKind Of(WeatherFactKind fact) => All[(int)fact];

    private static MetricKind Kind(WeatherFactKind fact, string id, LocalText name, LocalText label, LocalText description, string[] keywords, Func<WeatherFactConfig> create)
        => new(id, fact.ToString(), Glyph(fact), WeatherFactRules.History(fact), name, label, description, keywords, create)
        {
            Opens = MetricOpens.Weather,
            Graph = Graph(fact),
            Merges = Merges(fact),
        };

    /// <summary>
    /// [S-14710] A fact's graph over the place's hours (the weather graphs' types: Area, Line or Bars), drawn by default as
    /// the weather graph it merged drew ([S-6752]: the pressure, the wind and the gusts a line, the rain and the UV columns,
    /// the rest the area); none for a fact the hours do not carry.
    /// </summary>
    internal static MetricGraph? Graph(WeatherFactKind fact) => WeatherFactRules.History(fact) != MetricHistory.Series ? null : fact switch
    {
        WeatherFactKind.Wind or WeatherFactKind.Gusts or WeatherFactKind.Pressure => MetricGraph.Hours(ChartType.Line),
        WeatherFactKind.Precipitation or WeatherFactKind.Uv => MetricGraph.Hours(ChartType.Bars),
        _ => MetricGraph.Hours(),
    };

    /// <summary>
    /// [S-14713] The weather graph a fact stands in for (owner, 2026-11-21: "the wind graph is somewhat similar to the Wind
    /// widget that shows a trendline. i think it would be good if we merge all the different graph types into the general
    /// metric widget"): a placed one becomes the fact with its graph shown, in its spot.
    /// </summary>
    private static MergeFrom[] Merges(WeatherFactKind fact) => fact switch
    {
        WeatherFactKind.Temperature => [new("temperatureGraph") { Graph = true }],
        WeatherFactKind.Humidity => [new("humidityGraph") { Graph = true }],
        WeatherFactKind.Wind => [new("windGraph") { Graph = true }],
        WeatherFactKind.Precipitation => [new("rainGraph") { Graph = true }],
        WeatherFactKind.Pressure => [new("pressureGraph") { Graph = true }],
        WeatherFactKind.Uv => [new("uvGraph") { Graph = true }],
        _ => [],
    };

    /// <summary>The glyph for a reading, checked against the font ([S-733]): its row's lead and its kind's.</summary>
    internal static string Glyph(WeatherFactKind kind) => kind switch
    {
        WeatherFactKind.Temperature => Glyphs.Thermometer,
        WeatherFactKind.Today => Glyphs.Calendar,
        WeatherFactKind.FeelsLike => Glyphs.Thermometer,
        WeatherFactKind.Humidity or WeatherFactKind.Precipitation => Glyphs.Droplet,
        WeatherFactKind.Wind or WeatherFactKind.Gusts => Glyphs.Wind,
        WeatherFactKind.Pressure => Glyphs.Gauge,
        WeatherFactKind.Uv or WeatherFactKind.Sunrise => Glyphs.Sun,
        WeatherFactKind.Sunset => Glyphs.Moon,
        WeatherFactKind.CloudCover => Glyphs.Cloudy,
        _ => Glyphs.Info,
    };
}

// [S-13900] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (WeatherFactModule, WeatherFactConfig, WeatherFactView) over the single metric's base.

public sealed class WeatherTemperatureModule() : WeatherFactModule(WeatherFactKind.Temperature);

public sealed class WeatherHighLowModule() : WeatherFactModule(WeatherFactKind.Today);

public sealed class WeatherFeelsLikeModule() : WeatherFactModule(WeatherFactKind.FeelsLike);

public sealed class WeatherHumidityModule() : WeatherFactModule(WeatherFactKind.Humidity);

public sealed class WeatherWindModule() : WeatherFactModule(WeatherFactKind.Wind);

public sealed class WeatherPrecipitationModule() : WeatherFactModule(WeatherFactKind.Precipitation);

public sealed class WeatherPressureModule() : WeatherFactModule(WeatherFactKind.Pressure);

public sealed class WeatherUvModule() : WeatherFactModule(WeatherFactKind.Uv);

public sealed class WeatherCloudCoverModule() : WeatherFactModule(WeatherFactKind.CloudCover);

public sealed class WeatherGustsModule() : WeatherFactModule(WeatherFactKind.Gusts);

public sealed class WeatherSunriseModule() : WeatherFactModule(WeatherFactKind.Sunrise);

public sealed class WeatherSunsetModule() : WeatherFactModule(WeatherFactKind.Sunset);

public sealed class WeatherTemperatureConfig() : WeatherFactConfig(WeatherFactKind.Temperature);

public sealed class WeatherHighLowConfig() : WeatherFactConfig(WeatherFactKind.Today);

public sealed class WeatherFeelsLikeConfig() : WeatherFactConfig(WeatherFactKind.FeelsLike);

public sealed class WeatherHumidityConfig() : WeatherFactConfig(WeatherFactKind.Humidity);

public sealed class WeatherWindConfig() : WeatherFactConfig(WeatherFactKind.Wind);

public sealed class WeatherPrecipitationConfig() : WeatherFactConfig(WeatherFactKind.Precipitation);

public sealed class WeatherPressureConfig() : WeatherFactConfig(WeatherFactKind.Pressure);

public sealed class WeatherUvConfig() : WeatherFactConfig(WeatherFactKind.Uv);

public sealed class WeatherCloudCoverConfig() : WeatherFactConfig(WeatherFactKind.CloudCover);

public sealed class WeatherGustsConfig() : WeatherFactConfig(WeatherFactKind.Gusts);

public sealed class WeatherSunriseConfig() : WeatherFactConfig(WeatherFactKind.Sunrise);

public sealed class WeatherSunsetConfig() : WeatherFactConfig(WeatherFactKind.Sunset);
