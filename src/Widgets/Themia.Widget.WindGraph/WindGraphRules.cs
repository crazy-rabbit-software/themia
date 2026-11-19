using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.WindGraph;

/// <summary>[S-10502] [S-10503] The wind graph's mapping from the hourly answer to what it draws, pure and tested.</summary>
public static class WindGraphRules
{
    /// <summary>[S-10502] The scale reaches at least 30 km/h, so a breeze is drawn low.</summary>
    public const double FloorTop = 30;

    /// <summary>[S-10503] A gale (Beaufort 8, 62 km/h): Caution; a storm (Beaufort 10, 89 km/h): Critical.</summary>
    public const double Gale = 62;

    public const double Storm = 89;

    /// <summary>[S-10503] An hour's role by its speed (the gusts' when they are drawn: what one feels).</summary>
    public static ColourRole Role(double? kilometresPerHour) => kilometresPerHour switch
    {
        >= Storm => ColourRole.Critical,
        >= Gale => ColourRole.Caution,
        _ => ColourRole.Accent,
    };

    /// <summary>
    /// [S-10502] The graph for the window: each hour's mean wind from 0 (to the strongest gust drawn, at least
    /// <see cref="FloorTop"/>), the gusts as the second line when on, the strongest hour's figure over it, an arrow
    /// per hour that has room showing where the wind blows to; the reading is the hour under way's speed and where it
    /// comes from ("14 km/h SW"). Null when the window has fewer than two hours with a speed.
    /// </summary>
    public static HourGraphModel? Model(WeatherHours answer, WindGraphConfig config, DateTimeOffset now, Locale locale, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(config);
        var window = HourGraphRules.Window(answer.Hours, h => h.At, now, config.EffectiveHours);
        var speeds = window.Select(h => h.Wind).ToList();
        if (speeds.Count(v => v is not null) < 2)
        {
            return null;
        }

        var gusts = window.Select(h => config.ShowGusts ? h.Gusts : null).ToList();
        var scale = HourGraphRules.Scale(speeds.Concat(gusts), 0, fromZero: true, floorTop: FloorTop)!.Value;
        var slots = window.Select((h, i) => new HourSlot(
            h.At,
            HourGraphRules.Share(h.Wind, scale),
            Role(h.Wind),
            HourGraphRules.Share(gusts[i], scale),
            h.WindDirection)).ToList();

        var marks = new List<HourMark>();
        if (HourGraphRules.Extremes(speeds) is { } x)
        {
            marks.Add(new HourMark(x.High, WeatherReport.Wind(speeds[x.High]).Split(' ')[0]));
        }

        var first = window.FirstOrDefault(h => h.Wind is not null);
        var model = HourGraphRules.Of(slots, config) with
        {
            Range = scale,
            Format = v => LocaleFormat.Number(locale, v, 0),
            Reading = WeatherReport.WindFrom(locale, first?.Wind, first?.WindDirection),
            Marks = marks,
        };

        return HourGraphRules.Framed(model, WindGraphModule.Glyph, string.Empty, now, locale, zone);
    }
}
