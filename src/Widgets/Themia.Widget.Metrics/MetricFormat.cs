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

    /// <summary>[S-16303] A reading as the head says it: one figure, a pair's two ("8° / 16°"), a candle's close; "--" for none.</summary>
    public static string Reading(Locale locale, MetricDef metric, MetricValue? value, string currency = "")
    {
        ArgumentNullException.ThrowIfNull(metric);
        if (value is not { } v)
        {
            return None;
        }

        var first = One(locale, metric, v.Value, currency);
        return v.Second is { } second ? first + " / " + One(locale, metric, second, currency) : first;
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
