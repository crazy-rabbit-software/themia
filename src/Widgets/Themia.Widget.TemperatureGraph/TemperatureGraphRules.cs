using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.TemperatureGraph;

/// <summary>[S-10102] [S-10103] The temperature graph's mapping from the hourly answer to what it draws, pure and tested.</summary>
public static class TemperatureGraphRules
{
    /// <summary>[S-10103] Heat: Caution from 30 °C, Critical from 35 °C (on the °C reading, whatever the unit shown).</summary>
    public const double Hot = 30;

    public const double Hottest = 35;

    /// <summary>[S-10102] The least span drawn: 5 °C (9 °F), so a flat day is not drawn as a cliff.</summary>
    public static double MinSpan(TemperatureUnit unit) => unit == TemperatureUnit.Fahrenheit ? 9 : 5;

    /// <summary>[S-10103] An hour's role by its °C reading.</summary>
    public static ColourRole Role(double? celsius) => celsius switch
    {
        >= Hottest => ColourRole.Critical,
        >= Hot => ColourRole.Caution,
        _ => ColourRole.Accent,
    };

    /// <summary>
    /// [S-10102] The graph for the window from <paramref name="now"/>: each hour's temperature in the shared unit on
    /// one scale with the feels-like line (when on), the highest marked over its point and the lowest under it, the
    /// reading the hour under way's. Null when the window holds fewer than two hours with a temperature.
    /// </summary>
    public static HourGraphModel? Model(WeatherHours answer, TemperatureGraphConfig config, DateTimeOffset now, TemperatureUnit unit, Locale locale, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(config);
        var window = HourGraphRules.Window(answer.Hours, h => h.At, now, config.EffectiveHours);
        var temps = window.Select(h => HourGraphRules.Temperature(h.Temperature, unit)).ToList();
        if (temps.Count(t => t is not null) < 2)
        {
            return null;
        }

        var feels = window.Select(h => config.ShowFeelsLike ? HourGraphRules.Temperature(h.FeelsLike, unit) : null).ToList();
        var scale = HourGraphRules.Scale(temps.Concat(feels), MinSpan(unit))!.Value;
        var slots = window.Select((h, i) => new HourSlot(h.At, HourGraphRules.Share(temps[i], scale), Role(h.Temperature), HourGraphRules.Share(feels[i], scale))).ToList();

        var marks = new List<HourMark>();
        if (HourGraphRules.Extremes(temps) is { } x)
        {
            marks.Add(new HourMark(x.High, WeatherReport.Degrees(temps[x.High])));
            if (x.Low != x.High)
            {
                marks.Add(new HourMark(x.Low, WeatherReport.Degrees(temps[x.Low]), Under: true));
            }
        }

        var model = new HourGraphModel(slots, config.Style == TemperatureGraphStyle.Line ? HourGraphLook.Line : HourGraphLook.Area)
        {
            Reading = WeatherReport.Temperature(temps.FirstOrDefault(t => t is not null), unit),
            Marks = marks,
        };

        return HourGraphRules.Framed(model, TemperatureGraphModule.Glyph, string.Empty, now, locale, zone);
    }
}
