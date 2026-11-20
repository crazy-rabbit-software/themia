using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.WeatherFacts;

/// <summary>[S-722] [S-13900] One weather reading, and so one widget of the weather facts family: a kind per fact, in this order.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WeatherFactKind>))]
public enum WeatherFactKind
{
    /// <summary>[S-722] Today's high and low, "H 16°  L 8°".</summary>
    Today,
    FeelsLike,
    Humidity,
    Wind,
    Precipitation,
    Pressure,
    Uv,
    CloudCover,
    Gusts,
    Sunrise,
    Sunset,
}

/// <summary>
/// [S-722] [S-13900] The weather facts' readings: a fact's name and its value. A reading the service did not send is
/// the em dash, never a row left out: the card is its one row ([S-13901]), so a row that came and went would leave it empty.
/// </summary>
public static class WeatherFactRules
{
    /// <summary>Every fact, in the kind's order.</summary>
    public static IReadOnlyList<WeatherFactKind> All { get; } = Enum.GetValues<WeatherFactKind>();

    /// <summary>What stands in a row whose reading the service did not send.</summary>
    public const string Missing = "—";

    /// <summary>A fact's name in <paramref name="locale"/>: its kind's row name.</summary>
    public static string Name(WeatherFactKind kind, Locale locale) => WeatherFactKinds.Of(kind).Name.Text(locale);

    /// <summary>
    /// [S-14603] What history a fact has: the readings the service sends hour by hour (feels like, humidity, wind,
    /// precipitation, pressure, UV, gusts) a series, an arrow and a line over the hours; cloud cover the readings the
    /// card saw (the hours carry none); today's high and low and the sun's times none (no trend).
    /// </summary>
    public static MetricHistory History(WeatherFactKind kind) => kind switch
    {
        WeatherFactKind.FeelsLike or WeatherFactKind.Humidity or WeatherFactKind.Wind or WeatherFactKind.Precipitation
            or WeatherFactKind.Pressure or WeatherFactKind.Uv or WeatherFactKind.Gusts => MetricHistory.Series,
        WeatherFactKind.CloudCover => MetricHistory.Readings,
        _ => MetricHistory.None,
    };

    /// <summary>[S-14603] A fact's figure in one of the service's hours (its base units), null where the hours carry none.</summary>
    public static double? Hourly(WeatherFactKind kind, WeatherHour hour)
    {
        ArgumentNullException.ThrowIfNull(hour);
        return kind switch
        {
            WeatherFactKind.FeelsLike => hour.FeelsLike,
            WeatherFactKind.Humidity => hour.Humidity,
            WeatherFactKind.Wind => hour.Wind,
            WeatherFactKind.Precipitation => hour.Precipitation,
            WeatherFactKind.Pressure => hour.Pressure,
            WeatherFactKind.Uv => hour.Uv,
            WeatherFactKind.Gusts => hour.Gusts,
            _ => null,
        };
    }

    /// <summary>[S-14603] The digits a fact is shown with: what a move under them is not (UV and an amount of rain one, the rest whole).</summary>
    public static int Digits(WeatherFactKind kind) => kind is WeatherFactKind.Uv or WeatherFactKind.Precipitation ? 1 : 0;

    /// <summary>[S-14603] [S-722] A fact's reading as a metric: its value and the number a trend compares.</summary>
    public static MetricValue Metric(Locale locale, WeatherFactKind kind, CurrentWeather reading, TemperatureUnit unit)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var text = Value(locale, kind, reading, unit);
        double? number = kind switch
        {
            WeatherFactKind.FeelsLike => reading.FeelsLike,
            WeatherFactKind.Humidity => reading.Humidity,
            WeatherFactKind.Wind => reading.WindKilometresPerHour,
            WeatherFactKind.Precipitation => reading.Precipitation,
            WeatherFactKind.Pressure => reading.Pressure,
            WeatherFactKind.Uv => reading.UvIndex ?? reading.Today?.UvMax,
            WeatherFactKind.CloudCover => reading.CloudCover,
            WeatherFactKind.Gusts => reading.WindGusts,
            _ => null,
        };
        return new MetricValue(text, text == Missing ? null : number);
    }

    /// <summary>[S-722] One reading's value, or <see cref="Missing"/> where the service sent none.</summary>
    public static string Value(Locale locale, WeatherFactKind kind, CurrentWeather reading, TemperatureUnit unit)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var today = reading.Today;
        var value = kind switch
        {
            WeatherFactKind.Today => today is null ? string.Empty : WeatherReport.HighLow(locale, today.High, today.Low),
            WeatherFactKind.FeelsLike => WeatherReport.Temperature(reading.FeelsLike, unit),
            WeatherFactKind.Humidity => WeatherReport.Humidity(locale, reading.Humidity),
            WeatherFactKind.Wind => WeatherReport.WindFrom(locale, reading.WindKilometresPerHour, reading.WindDirection),

            // Today's chance reads better than the last hour's millimetres on a dry afternoon; the
            // amount wins once it is actually raining.
            WeatherFactKind.Precipitation => reading.Precipitation is > 0
                ? WeatherReport.Precipitation(locale, reading.Precipitation)
                : WeatherReport.Chance(locale, today?.PrecipitationChance),
            WeatherFactKind.Pressure => WeatherReport.Pressure(reading.Pressure),
            WeatherFactKind.Uv => WeatherReport.Uv(reading.UvIndex ?? today?.UvMax),
            WeatherFactKind.CloudCover => WeatherReport.Cover(locale, reading.CloudCover),
            WeatherFactKind.Gusts => WeatherReport.Wind(reading.WindGusts),
            WeatherFactKind.Sunrise => today?.Sunrise is null ? string.Empty : WeatherReport.Clock(locale, today.Sunrise),
            _ => today?.Sunset is null ? string.Empty : WeatherReport.Clock(locale, today.Sunset),
        };

        return value.Length == 0 || value == "--" ? Missing : value;
    }
}
