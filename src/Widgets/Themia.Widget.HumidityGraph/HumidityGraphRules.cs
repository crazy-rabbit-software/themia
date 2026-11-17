using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.HumidityGraph;

/// <summary>[S-10602] The humidity graph's mapping from the hourly answer to what it draws, pure and tested.</summary>
public static class HumidityGraphRules
{
    /// <summary>[S-10602] The least span drawn: 20 points, so a steady day is not drawn as a cliff.</summary>
    public const double MinSpan = 20;

    /// <summary>
    /// [S-10602] The graph for the window: each hour's relative humidity against the window's range (at least
    /// <see cref="MinSpan"/>, held to 0..100 %), all in the accent (humidity has no danger band of its own), the
    /// highest marked over its point and the lowest under it; the reading is the hour under way's ("84%"). Null when
    /// the window has fewer than two hours with a humidity.
    /// </summary>
    public static HourGraphModel? Model(WeatherHours answer, HumidityGraphConfig config, DateTimeOffset now, Locale locale, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(config);
        var window = HourGraphRules.Window(answer.Hours, h => h.At, now, config.EffectiveHours);
        var values = window.Select(h => h.Humidity).ToList();
        if (values.Count(v => v is not null) < 2)
        {
            return null;
        }

        var (low, high) = HourGraphRules.Scale(values, MinSpan)!.Value;
        (low, high) = low < 0 ? (0, high - low) : high > 100 ? (low - (high - 100), 100) : (low, high);
        var slots = window.Select(h => new HourSlot(h.At, HourGraphRules.Share(h.Humidity, (low, high)))).ToList();

        var marks = new List<HourMark>();
        if (HourGraphRules.Extremes(values) is { } x)
        {
            marks.Add(new HourMark(x.High, WeatherReport.Humidity(locale, values[x.High])));
            if (x.Low != x.High)
            {
                marks.Add(new HourMark(x.Low, WeatherReport.Humidity(locale, values[x.Low]), Under: true));
            }
        }

        var model = new HourGraphModel(slots, config.Style == HumidityGraphStyle.Line ? HourGraphLook.Line : HourGraphLook.Area)
        {
            Reading = WeatherReport.Humidity(locale, values.FirstOrDefault(v => v is not null)),
            Marks = marks,
        };

        return HourGraphRules.Framed(model, HumidityGraphModule.Glyph, string.Empty, now, locale, zone);
    }
}
