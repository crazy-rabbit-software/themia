using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Storage;

/// <summary>One remembered answer: the request it answers, when it was true, and what it said.</summary>
public readonly record struct CachedWeather<T>(string Key, DateTimeOffset At, T Value)
    where T : class;

/// <summary>Everything the weather widgets had on screen when the app last saved.</summary>
public sealed record WeatherCacheSnapshot(
    IReadOnlyList<CachedWeather<CurrentWeather>> Current,
    IReadOnlyList<CachedWeather<Forecast>> Forecasts)
{
    public static WeatherCacheSnapshot Empty { get; } = new([], []);
}

/// <summary>
/// [S-729] [S-2660] The weather widgets' last good answers as a document, kept across restarts in the
/// sealed web shelf of the offline store, so a cold launch draws the last known temperature at once
/// instead of "Loading" — with its real age in the header, in the caution colour, until a fetch
/// replaces it.
/// <para>
/// Never the state file: it is somebody else's data, it changes every quarter of an hour, and it
/// must never ride along in an export. Schema-versioned and validated on the way in: a document that
/// is not this schema is refused whole, and an entry that does not hold a usable reading is dropped.
/// Entries older than <see cref="MaxAge"/> are not restored: a week-old temperature is not worth
/// drawing even marked stale.
/// </para>
/// </summary>
public static class WeatherCacheStore
{
    public const int SchemaVersion = 1;

    /// <summary>The oldest answer worth restoring.</summary>
    public static TimeSpan MaxAge { get; } = TimeSpan.FromDays(3);

    /// <summary>A cache key is built by the app; anything longer was not.</summary>
    private const int MaxKeyLength = 200;

    /// <summary>
    /// Parse a cache, or null when the envelope is not one this build can trust (not JSON, another
    /// schema version, a required list missing). Entries that are not usable readings are dropped.
    /// </summary>
    public static WeatherCacheSnapshot? Parse(string json, DateTimeOffset now)
    {
        WeatherCacheFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, WeatherCacheJsonContext.Default.WeatherCacheFile);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }

        if (file is null || file.SchemaVersion != SchemaVersion || file.Current is null || file.Forecasts is null)
        {
            return null;
        }

        var current = new List<CachedWeather<CurrentWeather>>();

        foreach (var entry in file.Current)
        {
            if (Usable(entry?.Key, entry?.At, now) is { } at && ToReading(entry!.Reading) is { } reading)
            {
                current.Add(new CachedWeather<CurrentWeather>(entry.Key!, at, reading));
            }
        }

        var forecasts = new List<CachedWeather<Forecast>>();

        foreach (var entry in file.Forecasts)
        {
            if (Usable(entry?.Key, entry?.At, now) is { } at && ToDays(entry!.Days) is { Count: > 0 } days)
            {
                forecasts.Add(new CachedWeather<Forecast>(entry.Key!, at, new Forecast(days)));
            }
        }

        return new WeatherCacheSnapshot(current, forecasts);
    }

    public static string Serialize(WeatherCacheSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var file = new WeatherCacheFile
        {
            SchemaVersion = SchemaVersion,
            Current = [.. snapshot.Current.Select(entry => new CurrentEntry { Key = entry.Key, At = entry.At, Reading = FromReading(entry.Value) })],
            Forecasts = [.. snapshot.Forecasts.Select(entry => new ForecastEntry { Key = entry.Key, At = entry.At, Days = FromDays(entry.Value.Days) })],
        };

        return JsonSerializer.Serialize(file, WeatherCacheJsonContext.Default.WeatherCacheFile);
    }

    /// <summary>The entry's timestamp, if the entry has a key and an age worth restoring.</summary>
    private static DateTimeOffset? Usable(string? key, DateTimeOffset? at, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength || at is not { } when)
        {
            return null;
        }

        // From the future (a clock that jumped back) or from too long ago: not restored.
        return when > now.AddMinutes(5) || now - when > MaxAge ? null : when;
    }

    private static CurrentWeather? ToReading(CurrentDto? dto)
    {
        if (dto is null || Sane(dto.Temperature, -120, 100) is not { } temperature)
        {
            return null;
        }

        return new CurrentWeather(
            temperature,
            Sane(dto.FeelsLike, -150, 130),
            Sane(dto.Humidity, 0, 100),
            Sane(dto.Wind, 0, 500),
            Code(dto.Code))
        {
            Precipitation = Sane(dto.Precipitation, 0, 500),
            Pressure = Sane(dto.Pressure, 850, 1100),
            WindDirection = Sane(dto.WindDirection, 0, 360),
            WindGusts = Sane(dto.WindGusts, 0, 500),
            CloudCover = Sane(dto.CloudCover, 0, 100),
            UvIndex = Sane(dto.Uv, 0, 30),
            Days = ToDays(dto.Days) ?? [],
        };
    }

    private static List<ForecastDay>? ToDays(List<DayDto?>? list)
    {
        if (list is null)
        {
            return null;
        }

        var days = new List<ForecastDay>(list.Count);

        foreach (var dto in list)
        {
            if (dto?.Day is not { } text
                || !DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                continue;
            }

            days.Add(new ForecastDay(day, Code(dto.Code), Sane(dto.Low, -120, 100), Sane(dto.High, -120, 100), Sane(dto.Precipitation, 0, 2000))
            {
                PrecipitationChance = Sane(dto.PrecipitationChance, 0, 100),
                WindMax = Sane(dto.WindMax, 0, 500),
                UvMax = Sane(dto.UvMax, 0, 30),
                Sunrise = Clock(dto.Sunrise),
                Sunset = Clock(dto.Sunset),
            });
        }

        return days;
    }

    private static CurrentDto FromReading(CurrentWeather reading) => new()
    {
        Temperature = reading.Temperature,
        FeelsLike = reading.FeelsLike,
        Humidity = reading.Humidity,
        Wind = reading.WindKilometresPerHour,
        Code = reading.Code,
        Precipitation = reading.Precipitation,
        Pressure = reading.Pressure,
        WindDirection = reading.WindDirection,
        WindGusts = reading.WindGusts,
        CloudCover = reading.CloudCover,
        Uv = reading.UvIndex,
        Days = FromDays(reading.Days),
    };

    private static List<DayDto?> FromDays(IReadOnlyList<ForecastDay> days)
        => [.. days.Select(day => (DayDto?)new DayDto
        {
            Day = day.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Code = day.Code,
            Low = day.Low,
            High = day.High,
            Precipitation = day.Precipitation,
            PrecipitationChance = day.PrecipitationChance,
            WindMax = day.WindMax,
            UvMax = day.UvMax,
            Sunrise = day.Sunrise?.ToString("HH:mm", CultureInfo.InvariantCulture),
            Sunset = day.Sunset?.ToString("HH:mm", CultureInfo.InvariantCulture),
        })];

    private static TimeOnly? Clock(string? text)
        => text is not null && TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;

    private static double? Sane(double? value, double min, double max)
        => value is { } number && double.IsFinite(number) && number >= min && number <= max ? number : null;

    private static int? Code(int? value) => value is >= 0 and <= 99 ? value : null;
}

// ---- on-disk shape ----------------------------------------------------------------------------
// Every member nullable, so a field missing on disk is told apart from a zero.

internal sealed class WeatherCacheFile
{
    public int SchemaVersion { get; set; }

    public List<CurrentEntry?>? Current { get; set; }

    public List<ForecastEntry?>? Forecasts { get; set; }
}

internal sealed class CurrentEntry
{
    public string? Key { get; set; }

    public DateTimeOffset? At { get; set; }

    public CurrentDto? Reading { get; set; }
}

internal sealed class ForecastEntry
{
    public string? Key { get; set; }

    public DateTimeOffset? At { get; set; }

    public List<DayDto?>? Days { get; set; }
}

internal sealed class CurrentDto
{
    public double? Temperature { get; set; }

    public double? FeelsLike { get; set; }

    public double? Humidity { get; set; }

    public double? Wind { get; set; }

    public int? Code { get; set; }

    public double? Precipitation { get; set; }

    public double? Pressure { get; set; }

    public double? WindDirection { get; set; }

    public double? WindGusts { get; set; }

    public double? CloudCover { get; set; }

    public double? Uv { get; set; }

    public List<DayDto?>? Days { get; set; }
}

internal sealed class DayDto
{
    public string? Day { get; set; }

    public int? Code { get; set; }

    public double? Low { get; set; }

    public double? High { get; set; }

    public double? Precipitation { get; set; }

    public double? PrecipitationChance { get; set; }

    public double? WindMax { get; set; }

    public double? UvMax { get; set; }

    public string? Sunrise { get; set; }

    public string? Sunset { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeatherCacheFile))]
internal sealed partial class WeatherCacheJsonContext : JsonSerializerContext;
