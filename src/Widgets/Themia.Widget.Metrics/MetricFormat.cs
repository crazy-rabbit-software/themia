using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Widget.Metrics;

/// <summary>
/// [S-16303] A metric's figures as text, through the host's formats (a share's percent, a rate's bytes a second, a
/// latency, the language's digits): one value with its unit, a pair's two (each with its unit, "8° / 16°"), a candle's
/// close; and the time a slot stands for (the hour, the day, the second of the hub). "--" with nothing to say.
/// </summary>
public static class MetricFormat
{
    /// <summary>What stands for a reading the metric has not.</summary>
    public const string None = "--";

    /// <summary>[S-16303] One figure of <paramref name="metric"/> in its unit.</summary>
    public static string One(Locale locale, MetricDef metric, double value, string currency = "")
    {
        ArgumentNullException.ThrowIfNull(metric);
        if (!double.IsFinite(value))
        {
            return None;
        }

        string Digits(int digits) => LocaleFormat.Number(locale, value, digits);
        return metric.Unit switch
        {
            MetricUnit.Share => Formatting.Percent(locale, value),
            MetricUnit.Percent => LocaleFormat.Percent(locale, Math.Round(Math.Clamp(value, 0, 100))),
            MetricUnit.Rate => Formatting.Rate(locale, Math.Max(0, value)),
            MetricUnit.Milliseconds => Formatting.Latency(locale, value),
            MetricUnit.Temperature => Digits(0) + "°",
            MetricUnit.Millimetres => Digits(metric.Digits) + " mm",
            MetricUnit.Centimetres => Digits(metric.Digits) + " cm",
            MetricUnit.Speed => Digits(0) + " km/h",
            MetricUnit.Pressure => Digits(0) + " hPa",
            MetricUnit.Micrograms => Digits(metric.Digits) + " µg/m³",
            MetricUnit.Grains => Digits(0),
            MetricUnit.Minutes => Digits(0) + " min",
            MetricUnit.Price => currency.Length > 0 ? Digits(metric.Digits) + " " + currency : Digits(metric.Digits),
            MetricUnit.Ratio => ExchangeRules.Format(locale, value),
            _ => Digits(metric.Digits),
        };
    }

    /// <summary>
    /// [S-16324] The metric's widest reading in <paramref name="shape"/>, every figure an 8 (<see cref="MetricFaceRules.Template"/>
    /// of its largest form: a share's "100%", a pair's two, a price's thousands): what a face holds its reading's room by,
    /// so a running value, and "--", stand in one box (WR-15).
    /// </summary>
    public static string Widest(Locale locale, MetricDef metric, MetricShape shape, string currency = "")
    {
        ArgumentNullException.ThrowIfNull(metric);
        var value = metric.Unit switch
        {
            MetricUnit.Share => 1.0,
            MetricUnit.Percent => 100,
            MetricUnit.Rate => 888 * 1024.0 * 1024.0,
            MetricUnit.Milliseconds => 888,
            MetricUnit.Temperature => -88,
            MetricUnit.Millimetres or MetricUnit.Centimetres or MetricUnit.Micrograms => 888.8,
            MetricUnit.Speed or MetricUnit.Index => 888,
            MetricUnit.Pressure => 1088,
            MetricUnit.Grains => 8888,
            MetricUnit.Minutes => 60,
            MetricUnit.Price => 8888.88,
            MetricUnit.Ratio => 888.88,
            _ => 888,
        };
        return MetricFaceRules.Template(Reading(locale, metric, new MetricValue(value, shape == MetricShape.Pair ? value : null), currency));
    }

    /// <summary>[S-16303] A reading as the head says it: one figure, a pair's two ("8° / 16°"), a candle's close; "--" for none.</summary>
    public static string Reading(Locale locale, MetricDef metric, MetricValue? value, string currency = "")
    {
        ArgumentNullException.ThrowIfNull(metric);
        if (value is not { } v)
        {
            return None;
        }

        if (v.Second is not { } second)
        {
            return One(locale, metric, v.Value, currency);
        }

        // [S-16303] A pair in one unit says it once ("14 / 29 km/h", "3.4 / 0.3 MB/s"): the head keeps room for the name.
        if (metric.Unit == MetricUnit.Rate)
        {
            var top = Math.Max(Math.Abs(v.Value), Math.Abs(second));
            var scale = Math.Pow(1024, Math.Clamp(Math.Floor(Math.Log(Math.Max(1, top), 1024)), 0, 4));
            var (_, unit) = Split(Formatting.Rate(locale, scale));
            string Scaled(double x) => LocaleFormat.Number(locale, x / scale, top / scale < 10 ? 1 : 0);
            return unit.Length > 0 ? Scaled(v.Value) + " / " + Scaled(second) + " " + unit : One(locale, metric, v.Value) + " / " + One(locale, metric, second);
        }

        var (a, aUnit) = Split(One(locale, metric, v.Value, currency));
        var (b, bUnit) = Split(One(locale, metric, second, currency));
        return aUnit.Length > 0 && aUnit == bUnit ? a + " / " + b + " " + aUnit : a + (aUnit.Length > 0 ? " " + aUnit : string.Empty) + " / " + b + (bUnit.Length > 0 ? " " + bUnit : string.Empty);
    }

    /// <summary>A figure's number and its unit, at the first space (none: the figure alone, "13°").</summary>
    private static (string Number, string Unit) Split(string text)
    {
        var space = text.IndexOfAny([' ', ' ', ' ']);
        return space < 0 ? (text, string.Empty) : (text[..space], text[(space + 1)..]);
    }

    /// <summary>
    /// [S-16303] The time slot <paramref name="slot"/> of <paramref name="series"/> stands for, as the head says it under the
    /// pointer: the hub's second ("10:41:05"), an hour ("14:00"), a day ("Today", "Tue"), a quote's bar its time within a day
    /// and its date over more.
    /// </summary>
    public static string When(Locale locale, MetricSeries series, int slot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(series);
        var at = series.At(slot).ToOffset(now.Offset);
        var local = at.DateTime;
        if (series.Step < TimeSpan.FromMinutes(1))
        {
            return LocaleFormat.Time(locale, TimeOnly.FromDateTime(local), seconds: true);
        }

        var days = series.Times is { Count: > 1 } times ? (times[^1] - times[0]).TotalDays : series.Step.TotalDays * series.Slots.Count;
        if (series.Step >= TimeSpan.FromDays(1) || days > 2)
        {
            var day = DateOnly.FromDateTime(local);
            var today = DateOnly.FromDateTime(now.DateTime);
            return series.Times is null ? WeatherReport.DayLabel(locale, day, today) : LocaleFormat.Date(locale, day, DateStyle.Medium);
        }

        return LocaleFormat.Time(locale, TimeOnly.FromDateTime(local));
    }
}
