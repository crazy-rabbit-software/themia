using Themia.Core.Localization;
using Themia.Sdk;
using Themia.Widget.WeatherFacts;

[assembly: ThemiaWidgetFamily]
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
/// [S-13900] What one kind of the weather facts family is: its fact, its id, its config, its name, its description and
/// the words a search finds it by. One row a fact, in the facts' order (<see cref="WeatherFactKind"/>).
/// </summary>
internal sealed record WeatherFactKindInfo(WeatherFactKind Fact, string Id, Func<WeatherFactConfig> Create, LocalText Label, LocalText Description, string[] Keywords)
{
    public Type ConfigType { get; } = Create().GetType();
}

/// <summary>[S-13900] The family's kinds (owner, 2026-11-19: "these should actually be separate widgets, all of them").</summary>
internal static class WeatherFactKinds
{
    public static IReadOnlyList<WeatherFactKindInfo> All { get; } =
    [
        new(WeatherFactKind.Today, "weatherHighLow", () => new WeatherHighLowConfig(), WeatherFactsStr.WeatherFacts_HighLow_Label.Local(), WeatherFactsStr.WeatherFacts_HighLow_Desc.Local(),
            ["high", "low", "today", "temperature", "range", "max", "min", "weather"]),
        new(WeatherFactKind.FeelsLike, "weatherFeelsLike", () => new WeatherFeelsLikeConfig(), WeatherFactsStr.Weather_FeelsLike.Local(), WeatherFactsStr.WeatherFacts_FeelsLike_Desc.Local(),
            ["feels like", "apparent temperature", "wind chill", "heat index", "temperature", "weather"]),
        new(WeatherFactKind.Humidity, "weatherHumidity", () => new WeatherHumidityConfig(), WeatherFactsStr.Weather_Humidity.Local(), WeatherFactsStr.WeatherFacts_Humidity_Desc.Local(),
            ["humidity", "humid", "moisture", "damp", "weather"]),
        new(WeatherFactKind.Wind, "weatherWind", () => new WeatherWindConfig(), WeatherFactsStr.Weather_Wind.Local(), WeatherFactsStr.WeatherFacts_Wind_Desc.Local(),
            ["wind", "wind speed", "wind direction", "breeze", "weather"]),
        new(WeatherFactKind.Precipitation, "weatherPrecipitation", () => new WeatherPrecipitationConfig(), WeatherFactsStr.WidgetsC_Weather_Fact_Precipitation.Local(), WeatherFactsStr.WeatherFacts_Precipitation_Desc.Local(),
            ["precipitation", "rain", "chance of rain", "snow", "weather"]),
        new(WeatherFactKind.Pressure, "weatherPressure", () => new WeatherPressureConfig(), WeatherFactsStr.WidgetsC_Weather_Fact_Pressure.Local(), WeatherFactsStr.WeatherFacts_Pressure_Desc.Local(),
            ["pressure", "air pressure", "barometer", "hpa", "weather"]),
        new(WeatherFactKind.Uv, "weatherUv", () => new WeatherUvConfig(), WeatherFactsStr.WidgetsC_Weather_Fact_UvIndex.Local(), WeatherFactsStr.WeatherFacts_Uv_Desc.Local(),
            ["uv", "uv index", "sunburn", "sun", "weather"]),
        new(WeatherFactKind.CloudCover, "weatherCloudCover", () => new WeatherCloudCoverConfig(), WeatherFactsStr.WidgetsC_Weather_Fact_CloudCover.Local(), WeatherFactsStr.WeatherFacts_CloudCover_Desc.Local(),
            ["cloud cover", "clouds", "cloudy", "overcast", "weather"]),
        new(WeatherFactKind.Gusts, "weatherGusts", () => new WeatherGustsConfig(), WeatherFactsStr.WidgetsC_Weather_Fact_Gusts.Local(), WeatherFactsStr.WeatherFacts_Gusts_Desc.Local(),
            ["gusts", "wind gusts", "wind", "storm", "weather"]),
        new(WeatherFactKind.Sunrise, "weatherSunrise", () => new WeatherSunriseConfig(), WeatherFactsStr.WeatherFacts_Sunrise_Label.Local(), WeatherFactsStr.WeatherFacts_Sunrise_Desc.Local(),
            ["sunrise", "dawn", "morning", "sun", "weather"]),
        new(WeatherFactKind.Sunset, "weatherSunset", () => new WeatherSunsetConfig(), WeatherFactsStr.WeatherFacts_Sunset_Label.Local(), WeatherFactsStr.WeatherFacts_Sunset_Desc.Local(),
            ["sunset", "dusk", "evening", "sun", "weather"]),
    ];

    /// <summary>The kind showing <paramref name="fact"/>.</summary>
    public static WeatherFactKindInfo Of(WeatherFactKind fact) => All.Single(k => k.Fact == fact);
}

// [S-13900] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (WeatherFactModule, WeatherFactConfig, WeatherFactView).

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
