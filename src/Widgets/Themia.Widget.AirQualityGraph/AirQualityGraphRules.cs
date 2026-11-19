using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.AirQualityGraph;

/// <summary>[S-10402] [S-10403] The air graph's mapping from the hourly air answer to what it draws, pure and tested.</summary>
public static class AirQualityGraphRules
{
    /// <summary>
    /// [S-10403] The European Environment Agency's bands for a pollutant (µg/m³; the upper edges of Good, Fair,
    /// Moderate, Poor and Very poor, past the last Extremely poor): PM2.5 10, 20, 25, 50, 75; PM10 20, 40, 50, 100, 150;
    /// O₃ 50, 100, 130, 240, 380; NO₂ 40, 90, 120, 230, 340. The index's own are the air widget's ([S-5104]).
    /// </summary>
    public static IReadOnlyList<double> Breakpoints(AirMeasure measure, AirQualityScale scale) => measure switch
    {
        AirMeasure.Pm25 => [10, 20, 25, 50, 75],
        AirMeasure.Pm10 => [20, 40, 50, 100, 150],
        AirMeasure.Ozone => [50, 100, 130, 240, 380],
        AirMeasure.NitrogenDioxide => [40, 90, 120, 230, 340],
        _ => AirQualityRules.Breakpoints(scale),
    };

    /// <summary>[S-10403] A value's band: the first whose upper edge it does not pass (on an edge it is the better band).</summary>
    public static AirBand Band(AirMeasure measure, AirQualityScale scale, double value)
    {
        var edges = Breakpoints(measure, scale);
        for (var i = 0; i < edges.Count; i++)
        {
            if (value <= edges[i])
            {
                return (AirBand)i;
            }
        }

        return AirBand.Worst;
    }

    /// <summary>[S-10403] A band's role on the plot: the accent while good, Caution, then Critical ([S-6170]'s).</summary>
    public static ColourRole Role(AirMeasure measure, AirQualityScale scale, double? value)
        => value is { } v ? AirQualityRules.ArcRole(AirQualityRules.Tone(Band(measure, scale, v))) : ColourRole.Accent;

    /// <summary>[S-10402] An hour's value of the measure (the index on the resolved scale).</summary>
    public static double? Value(AirHour hour, AirMeasure measure, AirQualityScale scale)
    {
        ArgumentNullException.ThrowIfNull(hour);
        return measure switch
        {
            AirMeasure.Pm25 => hour.Pm25,
            AirMeasure.Pm10 => hour.Pm10,
            AirMeasure.Ozone => hour.Ozone,
            AirMeasure.NitrogenDioxide => hour.NitrogenDioxide,
            _ => scale == AirQualityScale.Us ? hour.UsAqi : hour.EuropeanAqi,
        };
    }

    /// <summary>
    /// [S-10402] The graph for the window (one to four days): each hour's measure from 0 (to the highest, at least the
    /// top of its second band, so a clean day is drawn low), each in its band's role, the worst hour's figure over it;
    /// the reading is the hour under way's value and band ("25 · Fair"), in Caution or Critical past the good bands.
    /// The pollutants are read on the European bands, the index on its scale (Auto by the place, [S-5103]). Null when
    /// the window has fewer than two hours with a value.
    /// </summary>
    public static HourGraphModel? Model(AirHours answer, AirQualityGraphConfig config, DateTimeOffset now, Locale locale, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(config);
        var scale = AirQualityRules.Resolve(config.Scale, config.Latitude, config.Longitude);
        var measure = config.Measure;
        var window = HourGraphRules.Window(answer.Hours, h => h.At, now, config.EffectiveHours);
        var values = window.Select(h => Value(h, measure, scale)).ToList();
        if (values.Count(v => v is not null) < 2)
        {
            return null;
        }

        var range = HourGraphRules.Scale(values, 0, fromZero: true, floorTop: Breakpoints(measure, scale)[1])!.Value;
        var slots = window.Select((h, i) => new HourSlot(h.At, HourGraphRules.Share(values[i], range), Role(measure, scale, values[i]))).ToList();
        var marks = new List<HourMark>();
        if (HourGraphRules.Extremes(values) is { } x)
        {
            marks.Add(new HourMark(x.High, LocaleFormat.Integer(locale, (long)Math.Round(values[x.High]!.Value))));
        }

        var first = values.FirstOrDefault(v => v is not null);
        var band = first is { } f ? Band(measure, scale, f) : (AirBand?)null;
        var model = HourGraphRules.Of(slots, config) with
        {
            Range = range,
            Format = v => LocaleFormat.Integer(locale, (long)Math.Round(v)),
            Reading = first is { } v && band is { } b ? $"{LocaleFormat.Integer(locale, (long)Math.Round(v))} · {BandName(b, measure, scale, locale)}" : "--",
            ReadingRole = band is { } r ? AirQualityRules.ValueRole(AirQualityRules.Tone(r)) : ColourRole.TextPrimary,
            Marks = marks,
        };

        return HourGraphRules.Framed(model, AirQualityGraphModule.Glyph, Measure(measure), now, locale, zone);
    }

    /// <summary>
    /// [S-10402] [S-12801] The measure after the reading: the pollutant with its unit, data the reading needs; nothing
    /// for the index, whose band's words say what it is (the kind's name is the Header widget's to say).
    /// </summary>
    public static string Measure(AirMeasure measure) => measure switch
    {
        AirMeasure.Pm25 => "PM2.5 µg/m³",
        AirMeasure.Pm10 => "PM10 µg/m³",
        AirMeasure.Ozone => "O₃ µg/m³",
        AirMeasure.NitrogenDioxide => "NO₂ µg/m³",
        _ => string.Empty,
    };

    /// <summary>[S-10403] A band's name on its scale: the US index's words for it, the European ones for the rest.</summary>
    public static string BandName(AirBand band, AirMeasure measure, AirQualityScale scale, Locale locale)
        => (measure == AirMeasure.Index && scale == AirQualityScale.Us
            ? band switch
            {
                AirBand.Best => AirQualityGraphStr.AirQualityGraph_Us_Good,
                AirBand.Second => AirQualityGraphStr.AirQualityGraph_Us_Moderate,
                AirBand.Third => AirQualityGraphStr.AirQualityGraph_Us_Sensitive,
                AirBand.Fourth => AirQualityGraphStr.AirQualityGraph_Us_Unhealthy,
                AirBand.Fifth => AirQualityGraphStr.AirQualityGraph_Us_VeryUnhealthy,
                _ => AirQualityGraphStr.AirQualityGraph_Us_Hazardous,
            }
            : band switch
            {
                AirBand.Best => AirQualityGraphStr.AirQualityGraph_Eu_Good,
                AirBand.Second => AirQualityGraphStr.AirQualityGraph_Eu_Fair,
                AirBand.Third => AirQualityGraphStr.AirQualityGraph_Eu_Moderate,
                AirBand.Fourth => AirQualityGraphStr.AirQualityGraph_Eu_Poor,
                AirBand.Fifth => AirQualityGraphStr.AirQualityGraph_Eu_VeryPoor,
                _ => AirQualityGraphStr.AirQualityGraph_Eu_ExtremelyPoor,
            }).Text(locale);
}
