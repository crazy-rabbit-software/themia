using System.Text.Json;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-12700] One hour of open-meteo's <c>hourly</c> block at a place: every figure optional (a field the
/// reply did not carry, carried as null or out of its physical range is null here, never 0), always in the
/// service's base units: °C, mm, km/h, %, hPa. The graphs convert a temperature to the shared unit when
/// they draw it ([S-3770]), so one answer serves either scale.
/// </summary>
/// <param name="At">The hour's stamp: an instant for a temperature, the end of the hour for a precipitation sum.</param>
public sealed record WeatherHour(DateTimeOffset At)
{
    public double? Temperature { get; init; }

    public double? FeelsLike { get; init; }

    public double? Precipitation { get; init; }

    /// <summary>0 to 100.</summary>
    public double? PrecipitationChance { get; init; }

    public double? Uv { get; init; }

    public double? Wind { get; init; }

    public double? Gusts { get; init; }

    /// <summary>Where the wind comes <i>from</i>, degrees clockwise from north.</summary>
    public double? WindDirection { get; init; }

    /// <summary>0 to 100.</summary>
    public double? Humidity { get; init; }

    /// <summary>Sea-level pressure, hPa.</summary>
    public double? Pressure { get; init; }

    /// <summary>[S-16330] Snow over the hour, in centimetres.</summary>
    public double? Snowfall { get; init; }

    /// <summary>[S-16330] 0 to 100.</summary>
    public double? CloudCover { get; init; }

    /// <summary>[S-16330] Minutes of sunshine in the hour, 0 to 60.</summary>
    public double? Sunshine { get; init; }
}

/// <summary>[S-12700] One day's daylight at the place: sunrise to sunset as instants.</summary>
public readonly record struct DaylightSpan(DateTimeOffset Rise, DateTimeOffset Set);

/// <summary>[S-12700] The next couple of days hour by hour at a place, and each day's daylight, as the service sent them.</summary>
public sealed record WeatherHours(IReadOnlyList<WeatherHour> Hours, IReadOnlyList<DaylightSpan> Daylight)
{
    /// <summary>The reply this was parsed from, so the offline store can save and re-parse it ([S-5161]).</summary>
    public string Source { get; init; } = string.Empty;
}

/// <summary>[S-12701] One hour of the air-quality forecast at a place (CAMS): the two indices and four pollutants in µg/m³.</summary>
public sealed record AirHour(DateTimeOffset At)
{
    public double? EuropeanAqi { get; init; }

    public double? UsAqi { get; init; }

    public double? Pm25 { get; init; }

    public double? Pm10 { get; init; }

    public double? Ozone { get; init; }

    public double? NitrogenDioxide { get; init; }

    /// <summary>[S-16331] The pollen forecast for the hour, grains/m³ by type: only the types the reply carried (Europe only).</summary>
    public IReadOnlyDictionary<PollenType, double> Pollen { get; init; } = new Dictionary<PollenType, double>();
}

/// <summary>[S-12701] The next days of air hour by hour at a place.</summary>
public sealed record AirHours(IReadOnlyList<AirHour> Hours)
{
    /// <summary>The reply this was parsed from ([S-5161]).</summary>
    public string Source { get; init; } = string.Empty;
}

/// <summary>[S-12700] [S-12701] The hourly replies' parsers: the trust boundary for the weather graphs.</summary>
public static class WeatherHoursWire
{
    /// <summary>What the weather graphs ask the forecast API's <c>hourly</c> block for.</summary>
    public const string HourlyFields =
        "temperature_2m,apparent_temperature,precipitation,precipitation_probability,uv_index"
        + ",wind_speed_10m,wind_gusts_10m,wind_direction_10m,relative_humidity_2m,pressure_msl"
        + ",snowfall,cloud_cover,sunshine_duration";

    /// <summary>What the air graph asks the air-quality API's <c>hourly</c> block for.</summary>
    public const string AirFields = "european_aqi,us_aqi,pm2_5,pm10,ozone,nitrogen_dioxide"
        + ",alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,olive_pollen,ragweed_pollen";

    /// <summary>
    /// [S-16332] How many hours before the one under way both hourly requests ask for: a day back, so a graph of the past day
    /// (the Metric widget's) reads what the forecast's own reply carries. The graphs that look ahead start at the hour under way.
    /// </summary>
    public const int PastHours = 24;

    /// <summary>[S-16331] The air reply's pollen fields, in the order of <see cref="PollenType"/>.</summary>
    private static readonly (string Field, PollenType Type)[] PollenFields =
    [
        ("alder_pollen", PollenType.Alder),
        ("birch_pollen", PollenType.Birch),
        ("grass_pollen", PollenType.Grass),
        ("mugwort_pollen", PollenType.Mugwort),
        ("olive_pollen", PollenType.Olive),
        ("ragweed_pollen", PollenType.Ragweed),
    ];

    /// <summary>How many hours from the one under way the weather graphs ask for: two days and the hour on the end.</summary>
    public const int Hours = 49;

    /// <summary>How many hours the air graph asks for: four days and the hour on the end (CAMS forecasts up to five).</summary>
    public const int AirHourCount = 97;

    /// <summary>
    /// [S-12700] Parse a forecast reply asked with <c>timeformat=unixtime</c>. Not JSON, an error document or no
    /// <c>hourly</c> block with a <c>time</c> array is rejected; an hour with a bad stamp is dropped, a figure out of
    /// its range is null on its own (a gap, never a 0), the hours kept in time order, a repeated stamp once. No hour
    /// at all is <see cref="FetchStatus.Empty"/>. The daily sunrise and sunset are an extra: missing, the hours still stand.
    /// </summary>
    public static WireResult<WeatherHours> Parse(string? json)
    {
        if (OpenMeteoJson.Open(json, out var document, out var problem) is not { } root)
        {
            return WireResult<WeatherHours>.Bad(problem);
        }

        using (document)
        {
            if (Block(root) is not { } block)
            {
                return WireResult<WeatherHours>.Bad("no hourly block with times");
            }

            var hours = new SortedDictionary<DateTimeOffset, WeatherHour>();
            for (var i = 0; i < block.Times.GetArrayLength(); i++)
            {
                if (OpenMeteoJson.Time(block.Times[i]) is not { } at || hours.ContainsKey(at))
                {
                    continue;
                }

                hours[at] = new WeatherHour(at)
                {
                    Temperature = At(block.Owner, "temperature_2m", i, -90, 60),
                    FeelsLike = At(block.Owner, "apparent_temperature", i, -100, 70),
                    Precipitation = At(block.Owner, "precipitation", i, 0, 500),
                    PrecipitationChance = At(block.Owner, "precipitation_probability", i, 0, 100),
                    Uv = At(block.Owner, "uv_index", i, 0, 30),
                    Wind = At(block.Owner, "wind_speed_10m", i, 0, 500),
                    Gusts = At(block.Owner, "wind_gusts_10m", i, 0, 600),
                    WindDirection = At(block.Owner, "wind_direction_10m", i, 0, 360),
                    Humidity = At(block.Owner, "relative_humidity_2m", i, 0, 100),
                    Pressure = At(block.Owner, "pressure_msl", i, 850, 1100),
                    Snowfall = At(block.Owner, "snowfall", i, 0, 500),
                    CloudCover = At(block.Owner, "cloud_cover", i, 0, 100),
                    Sunshine = At(block.Owner, "sunshine_duration", i, 0, 3600) is { } seconds ? seconds / 60 : null,
                };
            }

            if (hours.Count == 0)
            {
                return WireResult<WeatherHours>.Nothing("no usable hour in the reply");
            }

            return WireResult<WeatherHours>.Ok(new WeatherHours([.. hours.Values], Daylight(root)) { Source = json! });
        }
    }

    /// <summary>
    /// [S-12701] Parse an air-quality reply asked with <c>timeformat=unixtime</c>: the same rules as
    /// <see cref="Parse"/>; an hour with neither index nor any pollutant is dropped, none left is Empty.
    /// </summary>
    public static WireResult<AirHours> ParseAir(string? json)
    {
        if (OpenMeteoJson.Open(json, out var document, out var problem) is not { } root)
        {
            return WireResult<AirHours>.Bad(problem);
        }

        using (document)
        {
            if (Block(root) is not { } block)
            {
                return WireResult<AirHours>.Bad("no hourly block with times");
            }

            var hours = new SortedDictionary<DateTimeOffset, AirHour>();
            for (var i = 0; i < block.Times.GetArrayLength(); i++)
            {
                if (OpenMeteoJson.Time(block.Times[i]) is not { } at || hours.ContainsKey(at))
                {
                    continue;
                }

                var hour = new AirHour(at)
                {
                    EuropeanAqi = At(block.Owner, "european_aqi", i, 0, 1000),
                    UsAqi = At(block.Owner, "us_aqi", i, 0, 1000),
                    Pm25 = At(block.Owner, "pm2_5", i, 0, 5000),
                    Pm10 = At(block.Owner, "pm10", i, 0, 10_000),
                    Ozone = At(block.Owner, "ozone", i, 0, 5000),
                    NitrogenDioxide = At(block.Owner, "nitrogen_dioxide", i, 0, 5000),
                    Pollen = Pollen(block.Owner, i),
                };

                if (hour.EuropeanAqi is not null || hour.UsAqi is not null || hour.Pm25 is not null
                    || hour.Pm10 is not null || hour.Ozone is not null || hour.NitrogenDioxide is not null || hour.Pollen.Count > 0)
                {
                    hours[at] = hour;
                }
            }

            return hours.Count == 0
                ? WireResult<AirHours>.Nothing("no usable hour in the reply")
                : WireResult<AirHours>.Ok(new AirHours([.. hours.Values]) { Source = json! });
        }
    }

    private readonly record struct HourlyBlock(JsonElement Owner, JsonElement Times);

    /// <summary>[S-16331] The hour's pollen by type: a type the reply has no number for is left out (outside Europe: none), never 0.</summary>
    private static Dictionary<PollenType, double> Pollen(JsonElement owner, int index)
    {
        var pollen = new Dictionary<PollenType, double>();
        foreach (var (field, type) in PollenFields)
        {
            if (At(owner, field, index, 0, 100_000) is { } grains)
            {
                pollen[type] = grains;
            }
        }

        return pollen;
    }

    private static HourlyBlock? Block(JsonElement root)
        => root.TryGetProperty("hourly", out var hourly) && hourly.ValueKind == JsonValueKind.Object
            && hourly.TryGetProperty("time", out var times) && times.ValueKind == JsonValueKind.Array
                ? new HourlyBlock(hourly, times)
                : null;

    /// <summary>The <paramref name="index"/>th figure of a field's array, in range, or null (no array, too short, null, a string, out of range).</summary>
    private static double? At(JsonElement owner, string field, int index, double min, double max)
        => owner.TryGetProperty(field, out var values) && values.ValueKind == JsonValueKind.Array && index < values.GetArrayLength()
            ? OpenMeteoJson.Number(values[index], min, max)
            : null;

    /// <summary>The daily block's sunrise and sunset pairs that make a day (rise before set, under a day apart).</summary>
    private static List<DaylightSpan> Daylight(JsonElement root)
    {
        var spans = new List<DaylightSpan>();
        if (!root.TryGetProperty("daily", out var daily) || daily.ValueKind != JsonValueKind.Object
            || !daily.TryGetProperty("sunrise", out var rises) || rises.ValueKind != JsonValueKind.Array
            || !daily.TryGetProperty("sunset", out var sets) || sets.ValueKind != JsonValueKind.Array)
        {
            return spans;
        }

        var count = Math.Min(rises.GetArrayLength(), sets.GetArrayLength());
        for (var i = 0; i < count; i++)
        {
            if (OpenMeteoJson.Time(rises[i]) is { } rise && OpenMeteoJson.Time(sets[i]) is { } set
                && set > rise && set - rise < TimeSpan.FromDays(1))
            {
                spans.Add(new DaylightSpan(rise, set));
            }
        }

        return spans;
    }
}
