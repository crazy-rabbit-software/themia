using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Widget.WeatherFacts;

/// <summary>[S-722] One reading the weather facts can show; the order is the kind's own (what a new pick and a taller card add next).</summary>
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

/// <summary>One labelled reading: "Humidity" / "63%".</summary>
public readonly record struct WeatherFact(WeatherFactKind Kind, string Label, string Value);

/// <summary>
/// [S-722] [S-11105] The weather facts' readings: each picked kind, its name and its value, in the pick's
/// order. A reading the service did not send is the em dash, never a row left out: the card's height
/// is the pick's ([S-11106]), so a row that came and went would leave a hole or need room it does not have.
/// </summary>
public static class WeatherFactRules
{
    /// <summary>Every fact, in the kind's order.</summary>
    public static IReadOnlyList<WeatherFactKind> All { get; } = Enum.GetValues<WeatherFactKind>();

    /// <summary>[S-11105] A new card's pick: today's range, then feels like, humidity and wind (the old card's first rows).</summary>
    public static IReadOnlyList<WeatherFactKind> Defaults { get; } = [WeatherFactKind.Today, WeatherFactKind.FeelsLike, WeatherFactKind.Humidity, WeatherFactKind.Wind];

    /// <summary>What stands in a row whose reading the service did not send.</summary>
    public const string Missing = "—";

    /// <summary>A fact's name in <paramref name="locale"/>.</summary>
    public static string Name(WeatherFactKind kind, Locale locale) => kind switch
    {
        WeatherFactKind.Today => Strings.Get(locale, Str.Common_Today),
        WeatherFactKind.FeelsLike => WeatherFactsStr.Weather_FeelsLike.Text(locale),
        WeatherFactKind.Humidity => WeatherFactsStr.Weather_Humidity.Text(locale),
        WeatherFactKind.Wind => WeatherFactsStr.Weather_Wind.Text(locale),
        WeatherFactKind.Precipitation => WeatherFactsStr.WidgetsC_Weather_Fact_Precipitation.Text(locale),
        WeatherFactKind.Pressure => WeatherFactsStr.WidgetsC_Weather_Fact_Pressure.Text(locale),
        WeatherFactKind.Uv => WeatherFactsStr.WidgetsC_Weather_Fact_UvIndex.Text(locale),
        WeatherFactKind.CloudCover => WeatherFactsStr.WidgetsC_Weather_Fact_CloudCover.Text(locale),
        WeatherFactKind.Gusts => WeatherFactsStr.WidgetsC_Weather_Fact_Gusts.Text(locale),
        WeatherFactKind.Sunrise => Strings.Get(locale, Str.WidgetUi_Sunrise),
        _ => Strings.Get(locale, Str.WidgetUi_Sunset),
    };

    /// <summary>A fact's name in the app's language.</summary>
    public static string Name(WeatherFactKind kind) => Name(kind, Strings.Current);

    public static IReadOnlyList<WeatherFact> For(IReadOnlyList<WeatherFactKind> picked, CurrentWeather reading, TemperatureUnit unit)
        => For(Strings.Current, picked, reading, unit);

    /// <summary>[S-722] The <paramref name="picked"/> readings, labelled and written in <paramref name="locale"/>, one per pick.</summary>
    public static IReadOnlyList<WeatherFact> For(Locale locale, IReadOnlyList<WeatherFactKind> picked, CurrentWeather reading, TemperatureUnit unit)
    {
        ArgumentNullException.ThrowIfNull(picked);
        ArgumentNullException.ThrowIfNull(reading);
        return [.. picked.Select(kind => new WeatherFact(kind, Name(kind, locale), Value(locale, kind, reading, unit)))];
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
