using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-5106] How much of a pollen is in the air, on the scale pollen services use.</summary>
public enum PollenLevel
{
    None = 0,
    Low = 1,
    Moderate = 2,
    High = 3,
}

/// <summary>[S-5105] The WHO UV index categories.</summary>
public enum UvLevel
{
    Low = 0,
    Moderate = 1,
    High = 2,
    VeryHigh = 3,
    Extreme = 4,
}

/// <summary>
/// [S-5104] One index band, 0 (best) to 5 (worst), on either scale: the European index's Good, Fair,
/// Moderate, Poor, Very poor, Extremely poor, or the US index's Good, Moderate, Unhealthy for
/// sensitive groups, Unhealthy, Very unhealthy, Hazardous.
/// </summary>
public enum AirBand
{
    Best = 0,
    Second = 1,
    Third = 2,
    Fourth = 3,
    Fifth = 4,
    Worst = 5,
}

/// <summary>[S-5104] The Fluent status a band is drawn in: success, caution or critical.</summary>
public enum AirTone
{
    Good = 0,
    Caution = 1,
    Critical = 2,
}

/// <summary>[S-5103]..[S-5106] The rules that turn an air reading into what the widget says.</summary>
public static class AirQualityRules
{
    /// <summary>
    /// [S-5103] Auto's test for "in Europe": the CAMS Europe model's domain, 30°N–72°N and 25°W–45°E,
    /// which is also where the pollen forecast exists. Outside it the US index is the one more
    /// widely understood.
    /// </summary>
    public static bool InEurope(double latitude, double longitude)
        => latitude is >= 30 and <= 72 && longitude is >= -25 and <= 45;

    /// <summary>[S-5103] The scale in force for a place.</summary>
    public static AirQualityScale Resolve(AirQualityScale chosen, double? latitude, double? longitude)
        => chosen switch
        {
            AirQualityScale.European or AirQualityScale.Us => chosen,
            _ => latitude is { } lat && longitude is { } lon && InEurope(lat, lon) ? AirQualityScale.European : AirQualityScale.Us,
        };

    /// <summary>The index on a scale; null when the reply did not carry that one.</summary>
    public static double? Index(AirReading reading, AirQualityScale scale)
    {
        ArgumentNullException.ThrowIfNull(reading);
        return scale == AirQualityScale.Us ? reading.UsAqi : reading.EuropeanAqi;
    }

    /// <summary>
    /// [S-5104] The band of an index value. European: 0–20, 20–40, 40–60, 60–80, 80–100, over 100.
    /// US: 0–50, 50–100, 100–150, 150–200, 200–300, over 300. A value on a boundary is in the better band.
    /// </summary>
    public static AirBand Band(AirQualityScale scale, double value)
    {
        var upper = Breakpoints(scale);

        for (var i = 0; i < upper.Count; i++)
        {
            if (value <= upper[i])
            {
                return (AirBand)i;
            }
        }

        return AirBand.Worst;
    }

    /// <summary>[S-5104] The upper edges of the first five bands on a scale.</summary>
    public static IReadOnlyList<double> Breakpoints(AirQualityScale scale)
        => scale == AirQualityScale.Us ? UsBreakpoints : EuropeanBreakpoints;

    private static readonly double[] EuropeanBreakpoints = [20, 40, 60, 80, 100];
    private static readonly double[] UsBreakpoints = [50, 100, 150, 200, 300];

    /// <summary>
    /// [S-5109] Where a value sits on the band strip, 0 to 1: six equal segments, one per band, the
    /// value placed linearly inside its band; the open last band is drawn as wide as the one before.
    /// </summary>
    public static double ScalePosition(AirQualityScale scale, double value)
    {
        var edges = Breakpoints(scale);
        var band = (int)Band(scale, value);
        var lower = band == 0 ? 0 : edges[band - 1];
        var upper = band < edges.Count ? edges[band] : edges[^1] + (edges[^1] - edges[^2]);
        var inside = Math.Clamp((value - lower) / (upper - lower), 0, 1);
        return (band + inside) / 6.0;
    }

    /// <summary>[S-5104] The first two bands are good, the next two caution, the last two critical.</summary>
    public static AirTone Tone(AirBand band) => band switch
    {
        AirBand.Best or AirBand.Second => AirTone.Good,
        AirBand.Third or AirBand.Fourth => AirTone.Caution,
        _ => AirTone.Critical,
    };

    /// <summary>[S-5105] WHO categories: under 3 low, 3–6 moderate, 6–8 high, 8–11 very high, 11 and over extreme.</summary>
    public static UvLevel Uv(double index) => index switch
    {
        < 3 => UvLevel.Low,
        < 6 => UvLevel.Moderate,
        < 8 => UvLevel.High,
        < 11 => UvLevel.VeryHigh,
        _ => UvLevel.Extreme,
    };

    /// <summary>[S-5105] Low UV is good, moderate and high caution, very high and extreme critical.</summary>
    public static AirTone Tone(UvLevel level) => level switch
    {
        UvLevel.Low => AirTone.Good,
        UvLevel.Moderate or UvLevel.High => AirTone.Caution,
        _ => AirTone.Critical,
    };

    /// <summary>
    /// [S-5106] A pollen count's level, in grains/m³: from (low, moderate, high) = trees (alder,
    /// birch, olive) 1, 10, 100; grass and mugwort 1, 10, 50; ragweed 1, 5, 20 — the common
    /// pollen-service thresholds, ragweed's lower because a little of it already sets off most allergies.
    /// </summary>
    public static PollenLevel Level(PollenType type, double grains)
    {
        var (moderate, high) = type switch
        {
            PollenType.Alder or PollenType.Birch or PollenType.Olive => (10.0, 100.0),
            PollenType.Ragweed => (5.0, 20.0),
            _ => (10.0, 50.0),
        };

        return grains switch
        {
            < 1 => PollenLevel.None,
            _ when grains < moderate => PollenLevel.Low,
            _ when grains < high => PollenLevel.Moderate,
            _ => PollenLevel.High,
        };
    }

    /// <summary>
    /// [S-5106] The pollen worth naming, worst level first (the larger count breaks a tie), types at
    /// None left out. Empty when no type is in the air or the place has no pollen forecast.
    /// </summary>
    public static IReadOnlyList<(PollenType Type, PollenLevel Level)> Pollen(AirReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return [.. reading.Pollen
            .Select(p => (Type: p.Key, Level: Level(p.Key, p.Value), Grains: p.Value))
            .Where(p => p.Level > PollenLevel.None)
            .OrderByDescending(p => p.Level)
            .ThenByDescending(p => p.Grains)
            .ThenBy(p => p.Type)
            .Select(p => (p.Type, p.Level))];
    }

    /// <summary>Whether the place has a pollen forecast at all (any type carried a number).</summary>
    public static bool HasPollen(AirReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        return reading.Pollen.Count > 0;
    }
}
