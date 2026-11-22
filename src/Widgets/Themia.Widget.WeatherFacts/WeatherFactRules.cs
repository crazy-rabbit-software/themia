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
    /// <summary>[S-14714] The temperature now, "14°": the temperature graph's metric ([S-14713]).</summary>
    Temperature,

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
        WeatherFactKind.Temperature or WeatherFactKind.FeelsLike or WeatherFactKind.Humidity or WeatherFactKind.Wind or WeatherFactKind.Precipitation
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
            WeatherFactKind.Temperature => hour.Temperature,
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

    /// <summary>
    /// [S-14703] How a fact's value in one of the hours (<see cref="Hourly"/>, its base units) is written as its reading is
    /// ("1,013 hPa", "84%", "12 km/h"): what a Detailed graph writes for its low, its high and its latest.
    /// </summary>
    public static Func<double, string> Figure(Locale locale, WeatherFactKind kind, TemperatureUnit unit) => kind switch
    {
        WeatherFactKind.Temperature or WeatherFactKind.FeelsLike => v => WeatherReport.Temperature(v, unit),
        WeatherFactKind.Humidity => v => WeatherReport.Humidity(locale, v),
        WeatherFactKind.Wind or WeatherFactKind.Gusts => v => WeatherReport.Wind(v),
        WeatherFactKind.Precipitation => v => WeatherReport.Precipitation(locale, v),
        WeatherFactKind.Pressure => v => WeatherReport.Pressure(v),
        WeatherFactKind.Uv => v => WeatherReport.Uv(v),
        _ => v => LocaleFormat.Number(locale, v, Digits(kind)),
    };

    /// <summary>[S-14603] The digits a fact is shown with: what a move under them is not (UV and an amount of rain one, the rest whole).</summary>
    public static int Digits(WeatherFactKind kind) => kind is WeatherFactKind.Uv or WeatherFactKind.Precipitation ? 1 : 0;

    /// <summary>
    /// [S-15513] Whether a fact means something for a day ahead, and so takes a repeater's day: the day's high and low, its
    /// rain, its strongest wind, its UV, and its sun's times (a day's name and sky are day parts, [S-13919]). The present's readings (feels like, humidity,
    /// pressure, cloud cover, gusts) do not.
    /// </summary>
    public static bool TakesDay(WeatherFactKind kind) => kind is WeatherFactKind.Today or WeatherFactKind.Precipitation
        or WeatherFactKind.Wind or WeatherFactKind.Uv or WeatherFactKind.Sunrise or WeatherFactKind.Sunset;

    /// <summary>[S-15513] Day <paramref name="day"/> from today of <paramref name="reading"/>'s days (the place's calendar), or null past them.</summary>
    public static ForecastDay? DayOf(CurrentWeather reading, int day)
    {
        ArgumentNullException.ThrowIfNull(reading);
        return day >= 0 && day < reading.Days.Count ? reading.Days[day] : null;
    }

    /// <summary>
    /// [S-15513] The name a fact's row shows on day <paramref name="day"/>: its kind's, but a day ahead's high and low is that
    /// day's ("Tue"); null for the kind's own.
    /// </summary>
    public static string? DayRowName(Locale locale, WeatherFactKind kind, CurrentWeather reading, int day)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (DayOf(reading, day) is not { } on || DayOf(reading, 0) is not { } today)
        {
            return null;
        }

        return kind switch
        {
            WeatherFactKind.Today when day > 0 => WeatherReport.DayLabel(locale, on.Day, today.Day),
            _ => null,
        };
    }

    /// <summary>[S-14603] [S-722] A fact's reading as a metric: its value and the number a trend compares.</summary>
    public static MetricValue Metric(Locale locale, WeatherFactKind kind, CurrentWeather reading, TemperatureUnit unit)
        => Metric(locale, kind, reading, unit, 0);

    /// <summary>[S-15513] A fact's reading on day <paramref name="day"/> from today: a day ahead's has no number a trend compares.</summary>
    public static MetricValue Metric(Locale locale, WeatherFactKind kind, CurrentWeather reading, TemperatureUnit unit, int day)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (day > 0)
        {
            var said = Value(locale, kind, reading, unit, day);
            return new MetricValue(said, null);
        }

        var text = Value(locale, kind, reading, unit);
        double? number = kind switch
        {
            WeatherFactKind.Temperature => reading.Temperature,
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

    /// <summary>
    /// [S-15513] One reading's value on day <paramref name="day"/> from today (0: as <see cref="Value(Locale, WeatherFactKind, CurrentWeather, TemperatureUnit)"/>):
    /// a day ahead's high and low, its chance of rain (else its amount), its strongest wind, its highest UV, its sun's times;
    /// a day past the forecast, or a fact of the present, is <see cref="Missing"/>.
    /// </summary>
    public static string Value(Locale locale, WeatherFactKind kind, CurrentWeather reading, TemperatureUnit unit, int day)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (day == 0)
        {
            return Value(locale, kind, reading, unit);
        }

        var on = DayOf(reading, day);
        var value = on is null ? string.Empty : kind switch
        {
            WeatherFactKind.Today => WeatherReport.HighLow(locale, on.High, on.Low),
            WeatherFactKind.Precipitation => on.PrecipitationChance is not null
                ? WeatherReport.Chance(locale, on.PrecipitationChance)
                : WeatherReport.Precipitation(locale, on.Precipitation),
            WeatherFactKind.Wind => WeatherReport.Wind(on.WindMax),
            WeatherFactKind.Uv => WeatherReport.Uv(on.UvMax),
            WeatherFactKind.Sunrise => on.Sunrise is null ? string.Empty : WeatherReport.Clock(locale, on.Sunrise),
            WeatherFactKind.Sunset => on.Sunset is null ? string.Empty : WeatherReport.Clock(locale, on.Sunset),
            _ => string.Empty,
        };

        return value.Length == 0 || value == "--" ? Missing : value;
    }

    /// <summary>[S-722] One reading's value, or <see cref="Missing"/> where the service sent none.</summary>
    public static string Value(Locale locale, WeatherFactKind kind, CurrentWeather reading, TemperatureUnit unit)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var today = reading.Today;
        var value = kind switch
        {
            WeatherFactKind.Today => today is null ? string.Empty : WeatherReport.HighLow(locale, today.High, today.Low),
            WeatherFactKind.Temperature => WeatherReport.Temperature(reading.Temperature, unit),
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
