using System.Text.Json;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// What a service reply parsed into: a value with <see cref="FetchStatus.Ok"/>, or the named reason
/// there is none (<see cref="FetchStatus.BadResponse"/> for a document that is not the schema,
/// <see cref="FetchStatus.Empty"/> for a correct one with nothing usable in it) and one line for the log.
/// </summary>
public readonly record struct WireResult<T>(T? Value, FetchStatus Status, string? Detail)
    where T : class
{
    public static WireResult<T> Ok(T value) => new(value, FetchStatus.Ok, null);

    public static WireResult<T> Bad(string detail) => new(null, FetchStatus.BadResponse, detail);

    public static WireResult<T> Nothing(string detail) => new(null, FetchStatus.Empty, detail);
}

/// <summary>The six pollen types open-meteo forecasts for Europe ([S-5106]).</summary>
public enum PollenType
{
    Alder = 0,
    Birch = 1,
    Grass = 2,
    Mugwort = 3,
    Olive = 4,
    Ragweed = 5,
}

/// <summary>
/// [S-5102] The air at a place now, from open-meteo's air-quality <c>current</c> block. Every figure
/// is optional: a field the reply did not carry, carried as null, or carried out of its physical range
/// is null here, never 0 (zero pollen and "no pollen forecast here" are different facts).
/// </summary>
/// <param name="At">The hour the reading is for, as the service stamped it.</param>
public sealed record AirReading(DateTimeOffset At)
{
    public double? EuropeanAqi { get; init; }

    public double? UsAqi { get; init; }

    public double? Pm25 { get; init; }

    public double? Pm10 { get; init; }

    public double? Ozone { get; init; }

    public double? Uv { get; init; }

    /// <summary>Grains per cubic metre, for the types the reply carried a number for.</summary>
    public IReadOnlyDictionary<PollenType, double> Pollen { get; init; } = new Dictionary<PollenType, double>();

    /// <summary>The reply this was parsed from, so the offline store can save and re-parse it ([S-5161]).</summary>
    public string Source { get; init; } = string.Empty;
}

/// <summary>[S-5102] The air-quality reply's parser: the trust boundary for that service.</summary>
public static class AirQualityWire
{
    /// <summary>What the widget asks for, in the order of <see cref="PollenType"/> for the pollen.</summary>
    public const string CurrentFields =
        "european_aqi,us_aqi,pm2_5,pm10,ozone,uv_index"
        + ",alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,olive_pollen,ragweed_pollen";

    private static readonly (string Field, PollenType Type)[] PollenFields =
    [
        ("alder_pollen", PollenType.Alder),
        ("birch_pollen", PollenType.Birch),
        ("grass_pollen", PollenType.Grass),
        ("mugwort_pollen", PollenType.Mugwort),
        ("olive_pollen", PollenType.Olive),
        ("ragweed_pollen", PollenType.Ragweed),
    ];

    /// <summary>
    /// Parse a reply asked with <c>timeformat=unixtime</c>. Not JSON, not an object, an error
    /// document or a missing/untimed <c>current</c> block is rejected; a block with neither index in
    /// it is <see cref="FetchStatus.Empty"/>. Each figure out of range is dropped on its own.
    /// </summary>
    public static WireResult<AirReading> Parse(string? json)
    {
        if (OpenMeteoJson.Open(json, out var document, out var problem) is not { } root)
        {
            return WireResult<AirReading>.Bad(problem);
        }

        using (document)
        {
            if (!root.TryGetProperty("current", out var current) || current.ValueKind != JsonValueKind.Object)
            {
                return WireResult<AirReading>.Bad("no current block");
            }

            if (OpenMeteoJson.Time(current, "time") is not { } at)
            {
                return WireResult<AirReading>.Bad("the current block has no time");
            }

            var pollen = new Dictionary<PollenType, double>();
            foreach (var (field, type) in PollenFields)
            {
                if (OpenMeteoJson.Number(current, field, 0, 100_000) is { } grains)
                {
                    pollen[type] = grains;
                }
            }

            var reading = new AirReading(at)
            {
                EuropeanAqi = OpenMeteoJson.Number(current, "european_aqi", 0, 1000),
                UsAqi = OpenMeteoJson.Number(current, "us_aqi", 0, 1000),
                Pm25 = OpenMeteoJson.Number(current, "pm2_5", 0, 5000),
                Pm10 = OpenMeteoJson.Number(current, "pm10", 0, 10_000),
                Ozone = OpenMeteoJson.Number(current, "ozone", 0, 5000),
                Uv = OpenMeteoJson.Number(current, "uv_index", 0, 30),
                Pollen = pollen,
                Source = json!,
            };

            return reading.EuropeanAqi is null && reading.UsAqi is null
                ? WireResult<AirReading>.Nothing("no air quality index in the reply")
                : WireResult<AirReading>.Ok(reading);
        }
    }
}

/// <summary>The JSON reading every open-meteo parser shares: guarded, typed, never throwing.</summary>
internal static class OpenMeteoJson
{
    /// <summary>A size no answer to these requests comes near; a larger body is not the document asked for.</summary>
    private const int MaxLength = 512 * 1024;

    /// <summary>
    /// The root object of a reply, or null with the reason: not JSON, not an object, too large, or
    /// open-meteo's <c>{"error":true,"reason":"..."}</c>. The caller disposes the document.
    /// </summary>
    public static JsonElement? Open(string? json, out JsonDocument? document, out string problem)
    {
        document = null;
        problem = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            problem = "an empty reply";
            return null;
        }

        if (json.Length > MaxLength)
        {
            problem = "the reply is too large";
            return null;
        }

        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        }
        catch (JsonException ex)
        {
            problem = $"not JSON: {ex.Message}";
            return null;
        }

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            document = null;
            problem = "the reply is not an object";
            return null;
        }

        if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.True)
        {
            problem = root.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                ? $"the service refused: {reason.GetString()}"
                : "the service refused";
            document.Dispose();
            document = null;
            return null;
        }

        return root;
    }

    /// <summary>A finite number inside a physical range, or null (absent, null, a string, out of range).</summary>
    public static double? Number(JsonElement owner, string name, double min, double max)
        => owner.TryGetProperty(name, out var value) ? Number(value, min, max) : null;

    public static double? Number(JsonElement value, double min, double max)
        => value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var number)
            && double.IsFinite(number)
            && number >= min
            && number <= max
                ? number
                : null;

    /// <summary>A unix-seconds stamp in a sane window (1970 to 2200), as UTC.</summary>
    public static DateTimeOffset? Time(JsonElement owner, string name)
        => owner.TryGetProperty(name, out var value) ? Time(value) : null;

    public static DateTimeOffset? Time(JsonElement value)
        => value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var seconds)
            && seconds is > 0 and < 7_258_118_400
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
}
