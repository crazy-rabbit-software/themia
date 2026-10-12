using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Storage;

/// <summary>Which service an answer in the online cache came from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OnlineSource>))]
public enum OnlineSource
{
    Air = 0,
    Rain = 1,
    Rates = 2,
}

/// <summary>One remembered reply: the request it answers, when it was fetched, and the reply itself.</summary>
public readonly record struct CachedReply(OnlineSource Source, string Key, DateTimeOffset At, string Body);

/// <summary>What <see cref="OnlineCacheStore.Parse"/> gives back: every reply that still parses, as its model.</summary>
public sealed record OnlineCacheSnapshot(
    IReadOnlyList<(string Key, DateTimeOffset At, AirReading Value)> Air,
    IReadOnlyList<(string Key, DateTimeOffset At, RainNowcast Value)> Rain,
    IReadOnlyList<(string Key, DateTimeOffset At, RateTable Value)> Rates);

/// <summary>
/// [S-5161] The air, rain and exchange-rate answers kept across restarts, in their own sealed web
/// shelf, as weather keeps its own ([S-729]). The document holds each reply <b>as the service sent
/// it</b>, and a restore parses it again through the same parser as a fetch: one validation path,
/// so a stored copy can never be trusted more than a fresh one. Schema-versioned; a document that is
/// not this schema is refused whole, an entry that no longer parses (or is too old to be worth
/// drawing: air 2 days, rain 6 hours, rates 10 days) is dropped.
/// </summary>
public static class OnlineCacheStore
{
    public const int SchemaVersion = 1;

    private const int MaxKeyLength = 200;

    /// <summary>The newest this many entries per source are kept; a desktop has a handful of places.</summary>
    private const int MaxPerSource = 16;

    public static TimeSpan MaxAge(OnlineSource source) => source switch
    {
        OnlineSource.Air => TimeSpan.FromDays(2),
        OnlineSource.Rain => TimeSpan.FromHours(6),
        _ => TimeSpan.FromDays(10),
    };

    public static string Serialize(IEnumerable<CachedReply> replies)
    {
        ArgumentNullException.ThrowIfNull(replies);

        var file = new OnlineCacheFile
        {
            SchemaVersion = SchemaVersion,
            Entries = [.. replies
                .GroupBy(r => r.Source)
                .SelectMany(g => g.OrderByDescending(r => r.At).Take(MaxPerSource))
                .Select(r => new OnlineCacheEntry { Source = r.Source, Key = r.Key, At = r.At, Body = r.Body })],
        };

        return JsonSerializer.Serialize(file, OnlineCacheJsonContext.Default.OnlineCacheFile);
    }

    /// <summary>The stored replies that still parse and are young enough, or null for a document this build cannot trust.</summary>
    public static OnlineCacheSnapshot? Parse(string json, DateTimeOffset now)
    {
        OnlineCacheFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, OnlineCacheJsonContext.Default.OnlineCacheFile);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }

        if (file is null || file.SchemaVersion != SchemaVersion || file.Entries is null)
        {
            return null;
        }

        var air = new List<(string, DateTimeOffset, AirReading)>();
        var rain = new List<(string, DateTimeOffset, RainNowcast)>();
        var rates = new List<(string, DateTimeOffset, RateTable)>();

        foreach (var entry in file.Entries)
        {
            if (entry is null
                || !Enum.IsDefined(entry.Source)
                || string.IsNullOrWhiteSpace(entry.Key)
                || entry.Key.Length > MaxKeyLength
                || entry.At is not { } at
                || at > now.AddMinutes(5)
                || now - at > MaxAge(entry.Source)
                || string.IsNullOrEmpty(entry.Body))
            {
                continue;
            }

            switch (entry.Source)
            {
                case OnlineSource.Air when AirQualityWire.Parse(entry.Body).Value is { } reading:
                    air.Add((entry.Key, at, reading));
                    break;
                case OnlineSource.Rain when RainWire.Parse(entry.Body).Value is { } nowcast:
                    rain.Add((entry.Key, at, nowcast));
                    break;
                case OnlineSource.Rates when EcbWire.Parse(entry.Body).Value is { } table:
                    rates.Add((entry.Key, at, table));
                    break;
            }
        }

        return new OnlineCacheSnapshot(air, rain, rates);
    }
}

internal sealed class OnlineCacheFile
{
    public int SchemaVersion { get; set; }

    public List<OnlineCacheEntry?>? Entries { get; set; }
}

internal sealed class OnlineCacheEntry
{
    public OnlineSource Source { get; set; }

    public string? Key { get; set; }

    public DateTimeOffset? At { get; set; }

    public string? Body { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OnlineCacheFile))]
internal sealed partial class OnlineCacheJsonContext : JsonSerializerContext;
