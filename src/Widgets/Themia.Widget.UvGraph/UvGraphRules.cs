using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.UvGraph;

/// <summary>[S-10302] [S-10303] The UV graph's mapping from the hourly answer to what it draws, pure and tested.</summary>
public static class UvGraphRules
{
    /// <summary>[S-10302] The scale reaches at least 6 (the top of Moderate), so a low day is drawn low.</summary>
    public const double FloorTop = 6;

    /// <summary>
    /// [S-10303] The WHO band of an index ([S-5105]) as the graph draws it: Low the accent, Moderate and High
    /// Caution, Very high and Extreme Critical (the Chart block's three roles, [S-6717]).
    /// </summary>
    public static ColourRole Role(double? index)
        => index is { } v ? AirQualityRules.ArcRole(AirQualityRules.Tone(AirQualityRules.Uv(v))) : ColourRole.Accent;

    /// <summary>[S-10303] The reading's role: primary while Low, then the band's Caution or Critical (never the accent on text).</summary>
    public static ColourRole ReadingRole(double? index)
        => index is { } v ? AirQualityRules.ValueRole(AirQualityRules.Tone(AirQualityRules.Uv(v))) : ColourRole.TextPrimary;

    /// <summary>[S-10303] A band's name.</summary>
    public static string Level(UvLevel level, Locale locale) => (level switch
    {
        UvLevel.Low => UvGraphStr.UvGraph_Level_Low,
        UvLevel.Moderate => UvGraphStr.UvGraph_Level_Moderate,
        UvLevel.High => UvGraphStr.UvGraph_Level_High,
        UvLevel.VeryHigh => UvGraphStr.UvGraph_Level_VeryHigh,
        _ => UvGraphStr.UvGraph_Level_Extreme,
    }).Text(locale);

    /// <summary>
    /// [S-10302] The graph for the window: each hour's index from 0 (to the highest, at least <see cref="FloorTop"/>),
    /// each in its band's role, the peak's figure over it, the place's daylight shaded behind; the reading is the hour
    /// under way's index and band ("5 · Moderate"). Null when the window has fewer than two hours with an index.
    /// </summary>
    public static HourGraphModel? Model(WeatherHours answer, UvGraphConfig config, DateTimeOffset now, Locale locale, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(config);
        var window = HourGraphRules.Window(answer.Hours, h => h.At, now, config.EffectiveHours);
        var indices = window.Select(h => h.Uv).ToList();
        if (indices.Count(v => v is not null) < 2)
        {
            return null;
        }

        var scale = HourGraphRules.Scale(indices, 0, fromZero: true, floorTop: FloorTop)!.Value;
        var slots = window.Select(h => new HourSlot(h.At, HourGraphRules.Share(h.Uv, scale), Role(h.Uv))).ToList();
        var marks = new List<HourMark>();
        if (HourGraphRules.Extremes(indices) is { } x && indices[x.High] > 0)
        {
            marks.Add(new HourMark(x.High, WeatherReport.Uv(indices[x.High])));
        }

        var now0 = indices.FirstOrDefault(v => v is not null);
        var model = new HourGraphModel(slots, config.Style == UvGraphStyle.Area ? HourGraphLook.Area : HourGraphLook.Bars)
        {
            Reading = now0 is { } v ? $"{WeatherReport.Uv(v)} · {Level(AirQualityRules.Uv(v), locale)}" : "--",
            ReadingRole = ReadingRole(now0),
            Marks = marks,
            Shade = answer.Daylight,
        };

        return HourGraphRules.Framed(model, UvGraphModule.Glyph, string.Empty, now, locale, zone);
    }
}
