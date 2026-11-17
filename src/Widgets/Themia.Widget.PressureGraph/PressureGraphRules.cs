using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.PressureGraph;

/// <summary>[S-10702] The pressure graph's mapping from the hourly answer to what it draws, pure and tested.</summary>
public static class PressureGraphRules
{
    /// <summary>[S-10702] The least span drawn: 10 hPa, so a steady day is drawn flat, not as a cliff.</summary>
    public const double MinSpan = 10;

    /// <summary>
    /// [S-10702] The graph for the window: each hour's sea-level pressure against the window's range (at least
    /// <see cref="MinSpan"/>), all in the accent, the highest marked over its point and the lowest under it (bare
    /// hectopascals); the reading is the hour under way's ("1012 hPa"). Null when the window has fewer than two hours
    /// with a pressure.
    /// </summary>
    public static HourGraphModel? Model(WeatherHours answer, PressureGraphConfig config, DateTimeOffset now, Locale locale, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(config);
        var window = HourGraphRules.Window(answer.Hours, h => h.At, now, config.EffectiveHours);
        var values = window.Select(h => h.Pressure).ToList();
        if (values.Count(v => v is not null) < 2)
        {
            return null;
        }

        var scale = HourGraphRules.Scale(values, MinSpan)!.Value;
        var slots = window.Select(h => new HourSlot(h.At, HourGraphRules.Share(h.Pressure, scale))).ToList();

        var marks = new List<HourMark>();
        if (HourGraphRules.Extremes(values) is { } x)
        {
            marks.Add(new HourMark(x.High, Bare(values[x.High])));
            if (x.Low != x.High)
            {
                marks.Add(new HourMark(x.Low, Bare(values[x.Low]), Under: true));
            }
        }

        var model = new HourGraphModel(slots, config.Style == PressureGraphStyle.Area ? HourGraphLook.Area : HourGraphLook.Line)
        {
            Reading = WeatherReport.Pressure(values.FirstOrDefault(v => v is not null)),
            Marks = marks,
        };

        return HourGraphRules.Framed(model, PressureGraphModule.Glyph, string.Empty, now, locale, zone);
    }

    /// <summary>A figure on the plot: the hectopascals without their unit (the reading carries it).</summary>
    public static string Bare(double? hectopascals) => WeatherReport.Pressure(hectopascals).Split(' ')[0];
}
