using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.RainGraph;

/// <summary>[S-10202] [S-10203] The rain graph's mapping from the hourly answer or the nowcast to what it draws, pure and tested.</summary>
public static class RainGraphRules
{
    /// <summary>[S-10202] The columns' scale reaches at least 2 mm in an hour, so a drizzle stays low.</summary>
    public const double FloorTop = 2;

    /// <summary>[S-10202] Heavy rain in an hour (7.6 mm): Caution; violent (50 mm): Critical.</summary>
    public const double Heavy = 7.6;

    public const double Violent = 50;

    public static ColourRole Role(double? millimetres) => millimetres switch
    {
        >= Violent => ColourRole.Critical,
        >= Heavy => ColourRole.Caution,
        _ => ColourRole.Accent,
    };

    /// <summary>
    /// [S-10202] The hours look: a column per hour of the window on a scale from 0 (to the wettest hour, at least
    /// <see cref="FloorTop"/>), the chance (0..100 %) as the second line when on, the wettest hour's amount over its
    /// column; the reading is the window's total, or "Dry". Null when no hour of the window has an amount.
    /// </summary>
    public static HourGraphModel? Hours(WeatherHours answer, RainGraphConfig config, DateTimeOffset now, Locale locale, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(config);
        var window = HourGraphRules.Window(answer.Hours, h => h.At, now, config.EffectiveHours);
        var amounts = window.Select(h => h.Precipitation).ToList();
        if (window.Count < 2 || amounts.All(a => a is null))
        {
            return null;
        }

        var scale = HourGraphRules.Scale(amounts, 0, fromZero: true, floorTop: FloorTop)!.Value;
        var slots = window.Select(h => new HourSlot(
            h.At,
            HourGraphRules.Share(h.Precipitation, scale),
            Role(h.Precipitation),
            config.ShowChance && h.PrecipitationChance is { } c ? c / 100 : null)).ToList();

        var marks = new List<HourMark>();
        if (HourGraphRules.Extremes(amounts) is { } x && amounts[x.High] > 0)
        {
            marks.Add(new HourMark(x.High, WeatherReport.PrecipitationAmount(locale, amounts[x.High])));
        }

        var total = amounts.Sum(a => a ?? 0);
        var model = new HourGraphModel(slots, HourGraphLook.Bars)
        {
            Reading = Total(total, locale),
            Marks = marks,
        };

        return HourGraphRules.Framed(model, RainGraphModule.Glyph, string.Empty, now, locale, zone);
    }

    /// <summary>
    /// [S-10203] The nowcast look: the quarter under way and the seven after it ([S-5124]'s window) as columns on the
    /// rain widget's fixed scale ([S-5125]), heavy quarters in Caution, the reading the window's total. Null when the
    /// nowcast is too old to speak for now.
    /// </summary>
    public static HourGraphModel? Nowcast(RainNowcast answer, RainGraphConfig config, DateTimeOffset now, Locale locale, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(config);
        var outlook = RainRules.Outlook(answer, now);
        if (outlook.Headline == RainHeadline.Outdated || outlook.Steps.Count < 2)
        {
            return null;
        }

        var slots = outlook.Steps.Select(s => new HourSlot(
            s.Start,
            RainRules.BarShare(s.Millimetres),
            RainRules.Intensity(s.Millimetres) == RainIntensity.Heavy ? ColourRole.Caution : ColourRole.Accent)).ToList();
        var model = new HourGraphModel(slots, HourGraphLook.Bars)
        {
            Reading = Total(outlook.Total, locale),
            Step = RainRules.Step,
        };

        return HourGraphRules.Framed(model, RainGraphModule.Glyph, string.Empty, now, locale, zone) with
        {
            // The window's end is two hours on from the quarter under way, as the rain widget's ([S-5125]).
            EndLabel = LocaleFormat.Time(locale, TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(outlook.Steps[0].Start.AddHours(2), zone).DateTime)),
        };
    }

    /// <summary>The reading: "2.6 mm", or "Dry" for a window with nothing in it.</summary>
    public static string Total(double millimetres, Locale locale)
        => WeatherReport.Precipitation(locale, millimetres) is { Length: > 0 } amount ? amount : RainGraphStr.RainGraph_Dry.Text(locale);
}
