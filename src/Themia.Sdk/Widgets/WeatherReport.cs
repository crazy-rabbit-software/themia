using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// The broad shape of a sky, as a widget needs to draw it.
/// <para>
/// Core does not know what a Segoe code point is, so it names the <i>look</i> and the view picks the
/// glyph. That keeps the WMO table — which is the part with ninety-nine numbered cases and is worth
/// testing — out of a XAML file and away from an icon font.
/// </para>
/// </summary>
public enum WeatherLook
{
    /// <summary>No code, or one this table does not know.</summary>
    Unknown = 0,

    Clear,
    PartlyCloudy,
    Overcast,
    Fog,
    Drizzle,
    Rain,
    Snow,
    Thunderstorm,
}

/// <summary>
/// WMO weather codes, and the strings the two weather widgets print.
/// <para>
/// Open-meteo reports conditions as a WMO 4677 code, which is a number between 0 and 99 with gaps.
/// An unknown code is reported as "Code 78" rather than guessed at or blanked: the reader can see
/// that the service said something this app does not have a word for, which is true, and a blank
/// would read as a broken widget.
/// </para>
/// <para>
/// Every text in words comes from the string table, in the app's language or in the one passed in
/// (the explicit overloads keep tests independent of the process-wide language). Whole numbers are
/// written with plain digits, which every supported language shares; a number with a fraction or a
/// percentage goes through <see cref="LocaleFormat"/>, so "0,3 mm" and "59 %" read right in German.
/// </para>
/// </summary>
public static class WeatherReport
{
    private static readonly Dictionary<int, (WeatherLook Look, Str Text)> Codes = new()
    {
        [0] = (WeatherLook.Clear, Str.Forecast_Wmo_Clear),
        [1] = (WeatherLook.Clear, Str.Forecast_Wmo_MainlyClear),
        [2] = (WeatherLook.PartlyCloudy, Str.Forecast_Wmo_PartlyCloudy),
        [3] = (WeatherLook.Overcast, Str.Forecast_Wmo_Overcast),
        [45] = (WeatherLook.Fog, Str.Forecast_Wmo_Fog),
        [48] = (WeatherLook.Fog, Str.Forecast_Wmo_IcyFog),
        [51] = (WeatherLook.Drizzle, Str.WidgetsC_Weather_Condition_LightDrizzle),
        [53] = (WeatherLook.Drizzle, Str.Forecast_Wmo_Drizzle),
        [55] = (WeatherLook.Drizzle, Str.WidgetsC_Weather_Condition_HeavyDrizzle),
        [56] = (WeatherLook.Drizzle, Str.WidgetsC_Weather_Condition_FreezingDrizzle),
        [57] = (WeatherLook.Drizzle, Str.WidgetsC_Weather_Condition_HeavyFreezingDrizzle),
        [61] = (WeatherLook.Rain, Str.Forecast_Wmo_LightRain),
        [63] = (WeatherLook.Rain, Str.Forecast_Wmo_Rain),
        [65] = (WeatherLook.Rain, Str.Forecast_Wmo_HeavyRain),
        [66] = (WeatherLook.Rain, Str.WidgetsC_Weather_Condition_FreezingRain),
        [67] = (WeatherLook.Rain, Str.WidgetsC_Weather_Condition_HeavyFreezingRain),
        [71] = (WeatherLook.Snow, Str.Forecast_Wmo_LightSnow),
        [73] = (WeatherLook.Snow, Str.Forecast_Wmo_Snow),
        [75] = (WeatherLook.Snow, Str.Forecast_Wmo_HeavySnow),
        [77] = (WeatherLook.Snow, Str.Forecast_Wmo_SnowGrains),
        [80] = (WeatherLook.Rain, Str.WidgetsC_Weather_Condition_LightShowers),
        [81] = (WeatherLook.Rain, Str.Forecast_Wmo_Showers),
        [82] = (WeatherLook.Rain, Str.Forecast_Wmo_HeavyShowers),
        [85] = (WeatherLook.Snow, Str.Forecast_Wmo_SnowShowers),
        [86] = (WeatherLook.Snow, Str.WidgetsC_Weather_Condition_HeavySnowShowers),
        [95] = (WeatherLook.Thunderstorm, Str.WidgetsC_Weather_Condition_Thunderstorm),
        [96] = (WeatherLook.Thunderstorm, Str.WidgetsC_Weather_Condition_ThunderstormHail),
        [99] = (WeatherLook.Thunderstorm, Str.WidgetsC_Weather_Condition_SevereThunderstorm),
    };

    /// <summary>What the sky is doing, in words, in the app's language.</summary>
    public static string Describe(int? code) => Describe(Strings.Current, code);

    /// <summary>What the sky is doing, in words, in <paramref name="locale"/>.</summary>
    public static string Describe(Locale locale, int? code)
    {
        if (code is not { } value)
        {
            return Strings.Get(locale, Str.WidgetsC_Weather_Condition_Unknown);
        }

        return Codes.TryGetValue(value, out var entry)
            ? Strings.Get(locale, entry.Text)
            : Strings.Format(locale, Str.WidgetsC_Weather_Condition_Code, ("code", value.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Which icon family the condition belongs to.</summary>
    public static WeatherLook Look(int? code)
        => code is { } value && Codes.TryGetValue(value, out var entry) ? entry.Look : WeatherLook.Unknown;

    /// <summary>The degree symbol and scale letter, e.g. <c>°C</c>.</summary>
    public static string UnitSymbol(TemperatureUnit unit)
        => unit == TemperatureUnit.Fahrenheit ? "°F" : "°C";

    /// <summary>
    /// A temperature as a whole number and its unit: <c>18°C</c>.
    /// <para>
    /// Rounded, because a tenth of a degree is below what any forecast can actually claim and
    /// "17.3°C" reads as a measurement of this room rather than of the weather. A reading that came
    /// back as null or NaN prints as <c>--</c> and never throws on the render path.
    /// </para>
    /// </summary>
    public static string Temperature(double? value, TemperatureUnit unit)
    {
        if (value is not { } number || !double.IsFinite(number))
        {
            return "--";
        }

        return $"{Whole(number)}{UnitSymbol(unit)}";
    }

    /// <summary>
    /// [S-667] A temperature split for the hero readout: the number ("14", "-3") and the unit
    /// ("°C") it is drawn smaller beside. "--" and no unit when there is no reading.
    /// </summary>
    public static (string Number, string Unit) TemperatureParts(double? value, TemperatureUnit unit)
    {
        if (value is not { } number || !double.IsFinite(number))
        {
            return ("--", string.Empty);
        }

        return (Whole(number), UnitSymbol(unit));
    }

    /// <summary>A temperature with no unit, for a column where the unit is on the heading.</summary>
    public static string Degrees(double? value)
    {
        if (value is not { } number || !double.IsFinite(number))
        {
            return "--";
        }

        return Whole(number) + "°";
    }

    /// <summary>
    /// A reading as a whole number.
    /// <para>
    /// Negative zero is flattened on the way out. Rounding -0.4 away from zero yields the double
    /// -0.0, which formats as "-0" — so a mild frost showed as "-0°C", which reads as a typo
    /// rather than as a temperature.
    /// </para>
    /// </summary>
    private static string Whole(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);

        if (rounded == 0)
        {
            rounded = 0;
        }

        return rounded.ToString("F0", CultureInfo.InvariantCulture);
    }

    /// <summary>A relative humidity as a whole percentage, clamped to the range it can occupy.</summary>
    public static string Humidity(double? percent) => Humidity(Strings.Current, percent);

    public static string Humidity(Locale locale, double? percent)
    {
        if (percent is not { } value || !double.IsFinite(value))
        {
            return "--";
        }

        var clamped = Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);
        return LocaleFormat.Percent(locale, clamped);
    }

    /// <summary>A wind speed in km/h. Negative is not a speed, so it reads as missing.</summary>
    public static string Wind(double? kilometresPerHour)
    {
        if (kilometresPerHour is not { } value || !double.IsFinite(value) || value < 0)
        {
            return "--";
        }

        return $"{Math.Round(value, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture)} km/h";
    }

    /// <summary>
    /// A precipitation total in millimetres, or empty when there is none.
    /// <para>
    /// Empty rather than "0 mm": a dry day should look dry at a glance, and a column of zeroes is
    /// noise that makes the one wet day harder to find.
    /// </para>
    /// </summary>
    public static string Precipitation(double? millimetres) => Precipitation(Strings.Current, millimetres);

    public static string Precipitation(Locale locale, double? millimetres)
    {
        var amount = PrecipitationAmount(locale, millimetres);
        return amount.Length == 0 ? amount : $"{amount} mm";
    }

    /// <summary>
    /// The label on a forecast row: <c>Today</c>, then the short weekday name.
    /// <para>
    /// Computed against the current day, never baked into the cached forecast. A forecast fetched
    /// yesterday evening and restored this morning would otherwise label a stale row "Today", which
    /// is the one label a reader will act on without checking.
    /// </para>
    /// </summary>
    public static string DayLabel(DateOnly day, DateOnly today) => DayLabel(Strings.Current, day, today);

    /// <summary>
    /// [S-721] The same label in <paramref name="locale"/>: its word for today and its own weekday
    /// abbreviation ("Heute", "Do.").
    /// </summary>
    public static string DayLabel(Locale locale, DateOnly day, DateOnly today)
        => day == today
            ? Strings.Get(locale, Str.Common_Today)
            : LocaleFormat.WeekdayName(locale, day.DayOfWeek, NameWidth.Short);

    /// <summary>
    /// A precipitation amount as the bare number, for a column whose droplet says what it is
    /// ("0.3"). Empty when dry, for the same reason <see cref="Precipitation(double?)"/> is. [S-727]
    /// </summary>
    public static string PrecipitationAmount(double? millimetres) => PrecipitationAmount(Strings.Current, millimetres);

    public static string PrecipitationAmount(Locale locale, double? millimetres)
    {
        if (millimetres is not { } value || !double.IsFinite(value) || value <= 0)
        {
            return string.Empty;
        }

        // A tenth below 10mm, where the difference between 0.2 and 2 is the difference between
        // damp and wet; whole millimetres above it, where it no longer carries.
        return LocaleFormat.Number(locale, value, value < 10 ? 1 : 0);
    }

    /// <summary>A chance of precipitation, "40%", or empty when the service did not say.</summary>
    public static string Chance(double? percent) => Chance(Strings.Current, percent);

    public static string Chance(Locale locale, double? percent)
        => percent is { } value && double.IsFinite(value) ? Humidity(locale, value) : string.Empty;

    /// <summary>A share of the sky, "75%".</summary>
    public static string Cover(double? percent) => Humidity(Strings.Current, percent);

    public static string Cover(Locale locale, double? percent) => Humidity(locale, percent);

    /// <summary>
    /// Sea-level pressure in hectopascals, "1013 hPa". Outside 850..1100 is not weather. Plain
    /// digits with no grouping in every language, as weather services print it ("1013", not "1.013").
    /// </summary>
    public static string Pressure(double? hectopascals)
    {
        if (hectopascals is not { } value || !double.IsFinite(value) || value < 850 || value > 1100)
        {
            return "--";
        }

        return $"{Math.Round(value, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture)} hPa";
    }

    /// <summary>A UV index as a whole number; the scale has no use for a tenth.</summary>
    public static string Uv(double? index)
    {
        if (index is not { } value || !double.IsFinite(value) || value < 0)
        {
            return "--";
        }

        return Math.Round(value, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Where the wind comes from, on the eight-point rose: "NW" ("NO" in German). Empty for a
    /// missing or impossible bearing rather than a guess.
    /// </summary>
    public static string Compass(double? degrees) => Compass(Strings.Current, degrees);

    public static string Compass(Locale locale, double? degrees)
    {
        if (degrees is not { } value || !double.IsFinite(value) || value < 0 || value > 360)
        {
            return string.Empty;
        }

        var sector = (int)Math.Round(value / 45, MidpointRounding.AwayFromZero) % 8;
        return Strings.Get(locale, Points[sector]);
    }

    /// <summary>Wind speed and, when known, its direction: "6 km/h NW".</summary>
    public static string WindFrom(double? kilometresPerHour, double? degrees)
        => WindFrom(Strings.Current, kilometresPerHour, degrees);

    public static string WindFrom(Locale locale, double? kilometresPerHour, double? degrees)
    {
        var speed = Wind(kilometresPerHour);
        var from = Compass(locale, degrees);
        return speed == "--" || from.Length == 0
            ? speed
            : Strings.Format(locale, Str.WidgetsC_Weather_WindFrom, ("speed", speed), ("direction", from));
    }

    /// <summary>A sunrise or sunset on the language's clock, or "--" for a polar day.</summary>
    public static string Clock(TimeOnly? time) => Clock(Strings.Current, time);

    public static string Clock(Locale locale, TimeOnly? time)
        => time is { } value ? LocaleFormat.Time(locale, value) : "--";

    /// <summary>"H 24°  L 14°" for today's line beside the condition, or empty when neither is known.</summary>
    public static string HighLow(double? high, double? low) => HighLow(Strings.Current, high, low);

    public static string HighLow(Locale locale, double? high, double? low)
    {
        if (high is null && low is null)
        {
            return string.Empty;
        }

        return Strings.Format(locale, Str.WidgetsC_Weather_HighLow, ("high", Degrees(high)), ("low", Degrees(low)));
    }

    /// <summary>
    /// Where a day's high and low sit in the range covered by the whole forecast, as two fractions
    /// of 0..1 for the bar that shows it.
    /// <para>
    /// Scaled against the <i>set</i> of days rather than against each day's own range, which is the
    /// entire point of the bar: a week where every day is 12° to 14° should draw seven short bars in
    /// the same place, not seven full-width ones.
    /// </para>
    /// </summary>
    public static (double Start, double End) RangeBar(double low, double high, double coldest, double warmest)
    {
        if (!double.IsFinite(low) || !double.IsFinite(high) || !double.IsFinite(coldest) || !double.IsFinite(warmest))
        {
            return (0, 1);
        }

        var span = warmest - coldest;

        // Every day identical, or a single day: one full bar says "no spread" better than a
        // division by zero does.
        if (span <= 0)
        {
            return (0, 1);
        }

        var start = Math.Clamp((low - coldest) / span, 0, 1);
        var end = Math.Clamp((high - coldest) / span, 0, 1);

        return end < start ? (end, start) : (start, end);
    }

    private static readonly Str[] Points =
    [
        Str.WidgetsC_Weather_Compass_N, Str.WidgetsC_Weather_Compass_Ne, Str.WidgetsC_Weather_Compass_E, Str.WidgetsC_Weather_Compass_Se,
        Str.WidgetsC_Weather_Compass_S, Str.WidgetsC_Weather_Compass_Sw, Str.WidgetsC_Weather_Compass_W, Str.WidgetsC_Weather_Compass_Nw,
    ];
}
