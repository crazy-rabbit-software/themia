using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.WeatherNow;

namespace Themia.Core.Widgets;

/// <summary>What one secondary reading on the current-conditions widget is; the view maps it to a glyph.</summary>
public enum WeatherFactKind
{
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
/// [S-722] The current-conditions widget's secondary readings, in the order they earn their place.
/// <para>
/// The original's three — feels-like, humidity, wind — come first, so every size that shows any
/// detail shows those; the extras the larger tiles add follow in the order a reader is most likely
/// to want them. A reading the service did not send is left out rather than drawn as "--": a
/// tile has a fixed number of cells, and a blank one is a cell a real reading could have used.
/// </para>
/// </summary>
public static class WeatherFacts
{
    /// <summary>How many facts the widget can ever show; the cell grids size against it.</summary>
    public const int Max = 10;

    public static IReadOnlyList<WeatherFact> For(CurrentWeather reading, TemperatureUnit unit)
        => For(Strings.Current, reading, unit);

    /// <summary>The readings, labelled and written in <paramref name="locale"/>.</summary>
    public static IReadOnlyList<WeatherFact> For(Locale locale, CurrentWeather reading, TemperatureUnit unit)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var facts = new List<WeatherFact>(Max);

        void Add(WeatherFactKind kind, string label, string value)
        {
            if (value.Length > 0 && value != "--")
            {
                facts.Add(new WeatherFact(kind, label, value));
            }
        }

        var today = reading.Today;

        Add(WeatherFactKind.FeelsLike, WeatherNowStr.Weather_FeelsLike.Text(locale), WeatherReport.Temperature(reading.FeelsLike, unit));
        Add(WeatherFactKind.Humidity, WeatherNowStr.Weather_Humidity.Text(locale), WeatherReport.Humidity(locale, reading.Humidity));
        Add(WeatherFactKind.Wind, WeatherNowStr.Weather_Wind.Text(locale), WeatherReport.WindFrom(locale, reading.WindKilometresPerHour, reading.WindDirection));

        // Today's chance reads better than the last hour's millimetres on a dry afternoon; the
        // amount wins once it is actually raining.
        var precipitation = reading.Precipitation is > 0
            ? WeatherReport.Precipitation(locale, reading.Precipitation)
            : WeatherReport.Chance(locale, today?.PrecipitationChance);
        Add(WeatherFactKind.Precipitation, WeatherNowStr.WidgetsC_Weather_Fact_Precipitation.Text(locale), precipitation);

        Add(WeatherFactKind.Pressure, WeatherNowStr.WidgetsC_Weather_Fact_Pressure.Text(locale), WeatherReport.Pressure(reading.Pressure));
        Add(WeatherFactKind.Uv, WeatherNowStr.WidgetsC_Weather_Fact_UvIndex.Text(locale), WeatherReport.Uv(reading.UvIndex ?? today?.UvMax));
        Add(WeatherFactKind.CloudCover, WeatherNowStr.WidgetsC_Weather_Fact_CloudCover.Text(locale), WeatherReport.Cover(locale, reading.CloudCover));
        Add(WeatherFactKind.Gusts, WeatherNowStr.WidgetsC_Weather_Fact_Gusts.Text(locale), WeatherReport.Wind(reading.WindGusts));

        if (today is not null)
        {
            Add(WeatherFactKind.Sunrise, Strings.Get(locale, Str.WidgetUi_Sunrise), today.Sunrise is null ? string.Empty : WeatherReport.Clock(locale, today.Sunrise));
            Add(WeatherFactKind.Sunset, Strings.Get(locale, Str.WidgetUi_Sunset), today.Sunset is null ? string.Empty : WeatherReport.Clock(locale, today.Sunset));
        }

        return facts;
    }
}
