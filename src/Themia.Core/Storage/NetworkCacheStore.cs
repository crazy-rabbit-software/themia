using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.Core.Storage;

/// <summary>One remembered answer: the request it answers, when it was true, and what it said.</summary>
public readonly record struct CachedAnswer<T>(string Key, DateTimeOffset At, T Value)
    where T : class;

/// <summary>Everything the feed and stock widgets had on screen when the app last saved.</summary>
public sealed record NetworkCacheSnapshot(
    IReadOnlyList<CachedAnswer<FeedDocument>> Feeds,
    IReadOnlyList<CachedAnswer<TickerQuote>> Quotes)
{
    public static NetworkCacheSnapshot Empty { get; } = new([], []);
}

/// <summary>How the cache came off disk.</summary>
public enum NetworkCacheLoadOutcome
{
    /// <summary>No file yet.</summary>
    Fresh = 0,

    Loaded = 1,

    /// <summary>The file could not be used; set aside when corrupt, and the cache starts empty.</summary>
    Recovered = 2,
}

/// <summary>
/// [S-1300] The feed and stock widgets' last good answers, kept across restarts in
/// <c>network_cache.json</c> beside the app state (the original's localStorage cache, S-23/S-29),
/// so a cold launch draws the last headlines and price at once instead of "Loading" — marked
/// restored, so the header shows their real age in the caution colour until a fetch replaces them.
/// <para>
/// Its own file, never the state file: it is somebody else's data and must never ride along in an
/// export. Schema-versioned and validated on the way in, because every string in it came from a
/// stranger first: a file that is not this schema is set aside as <c>.corrupt</c>; an entry that does
/// not hold a usable feed or quote is dropped, as is anything older than <see cref="MaxAge"/>.
/// </para>
/// </summary>
public static class NetworkCacheStore
{
    public const int SchemaVersion = 1;

    public const string FileName = "network_cache.json";

    /// <summary>The oldest answer worth restoring.</summary>
    public static TimeSpan MaxAge { get; } = TimeSpan.FromDays(7);

    private const int MaxKeyLength = 2200;
    private const int MaxEntries = 64;
    private const int MaxItems = 200;
    private const int MaxBars = 4000;
    private const int MaxText = 1000;

    private const string Scope = "network-cache";

    private static long _tempSequence;

    /// <summary>Read the cache. Never throws for bad data: absent is empty; locked is empty and a warning; corrupt is set aside.</summary>
    public static (NetworkCacheSnapshot Snapshot, NetworkCacheLoadOutcome Outcome) Load(string path, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string json;
        try
        {
            if (!File.Exists(path))
            {
                return (NetworkCacheSnapshot.Empty, NetworkCacheLoadOutcome.Fresh);
            }

            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, $"could not read {path}; starting without saved feeds and quotes", ex);
            return (NetworkCacheSnapshot.Empty, NetworkCacheLoadOutcome.Recovered);
        }

        if (Parse(json, now) is { } snapshot)
        {
            return (snapshot, NetworkCacheLoadOutcome.Loaded);
        }

        Log.Warn(Scope, $"{path} is corrupt or from another version; starting without saved feeds and quotes");

        try
        {
            File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "could not set the unreadable cache aside", ex);
        }

        return (NetworkCacheSnapshot.Empty, NetworkCacheLoadOutcome.Recovered);
    }

    /// <summary>Parse a cache, or null when the envelope is not one this build trusts. Unusable entries are dropped.</summary>
    public static NetworkCacheSnapshot? Parse(string json, DateTimeOffset now)
    {
        NetworkCacheFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, NetworkCacheJsonContext.Default.NetworkCacheFile);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }

        if (file is null || file.SchemaVersion != SchemaVersion || file.Feeds is null || file.Quotes is null)
        {
            return null;
        }

        var feeds = new List<CachedAnswer<FeedDocument>>();

        foreach (var entry in file.Feeds.Take(MaxEntries))
        {
            if (Usable(entry?.Key, entry?.At, now) is { } at && ToFeed(entry!) is { } feed)
            {
                feeds.Add(new CachedAnswer<FeedDocument>(entry!.Key!, at, feed));
            }
        }

        var quotes = new List<CachedAnswer<TickerQuote>>();

        foreach (var entry in file.Quotes.Take(MaxEntries))
        {
            if (Usable(entry?.Key, entry?.At, now) is { } at && ToQuote(entry!) is { } quote)
            {
                quotes.Add(new CachedAnswer<TickerQuote>(entry!.Key!, at, quote));
            }
        }

        return new NetworkCacheSnapshot(feeds, quotes);
    }

    public static string Serialize(NetworkCacheSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var file = new NetworkCacheFile
        {
            SchemaVersion = SchemaVersion,
            Feeds = [.. snapshot.Feeds.Take(MaxEntries).Select(entry => (FeedEntryDto?)new FeedEntryDto
            {
                Key = entry.Key,
                At = entry.At,
                Title = entry.Value.Title,
                Items = [.. entry.Value.Entries.Take(MaxItems).Select(item => (ItemDto?)new ItemDto
                {
                    Title = item.Title,
                    Link = item.Link,
                    Published = item.Published,
                    Summary = item.Summary,
                })],
            })],
            Quotes = [.. snapshot.Quotes.Take(MaxEntries).Select(entry => (QuoteEntryDto?)FromQuote(entry))],
        };

        return JsonSerializer.Serialize(file, NetworkCacheJsonContext.Default.NetworkCacheFile);
    }

    /// <summary>Write atomically (unique temp file, then rename). The caller is the single writer.</summary>
    /// <returns>False when the write failed; logged, and the old file is untouched.</returns>
    public static bool Save(string path, NetworkCacheSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var json = Serialize(snapshot);
        var temp = $"{path}.tmp.{Environment.ProcessId}.{Interlocked.Increment(ref _tempSequence)}";

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(Scope, $"could not save {path}", ex);

            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                Log.Warn(Scope, "could not remove the temp cache file", cleanup);
            }

            return false;
        }
    }

    private static DateTimeOffset? Usable(string? key, DateTimeOffset? at, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength || at is not { } when)
        {
            return null;
        }

        return when > now.AddMinutes(5) || now - when > MaxAge ? null : when;
    }

    private static FeedDocument? ToFeed(FeedEntryDto dto)
    {
        if (dto.Items is null)
        {
            return null;
        }

        var items = new List<FeedEntry>();

        foreach (var item in dto.Items.Take(MaxItems))
        {
            if (item is null)
            {
                continue;
            }

            // The link was checked when it was parsed; checked again here, since this file is
            // just as editable as the state file.
            var link = item.Link is { Length: > 0 } raw && WebInput.Url(raw) is { IsValid: true } url ? url.Url : string.Empty;
            items.Add(new FeedEntry(Clip(item.Title), link, item.Published, Clip(item.Summary)));
        }

        return items.Count == 0 ? null : new FeedDocument(Clip(dto.Title), items);
    }

    private static TickerQuote? ToQuote(QuoteEntryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Symbol) || !WebInput.IsPlausibleSymbol(dto.Symbol) || Finite(dto.Price) is not { } price)
        {
            return null;
        }

        var bars = new List<QuoteBar>();

        foreach (var bar in (dto.Bars ?? []).Take(MaxBars))
        {
            if (bar is { Count: 2 } && double.IsFinite(bar[0]) && double.IsFinite(bar[1]) && bar[0] is > 0 and < 4_102_444_800)
            {
                bars.Add(new QuoteBar((long)bar[0], bar[1]));
            }
        }

        var sessions = new TradingSessions(
            MarketQuote.Window(dto.PreStart, dto.PreEnd),
            MarketQuote.Window(dto.RegularStart, dto.RegularEnd),
            MarketQuote.Window(dto.PostStart, dto.PostEnd));

        var currency = (dto.Currency ?? string.Empty).Trim().ToUpperInvariant();

        return new TickerQuote(
            dto.Symbol.Trim(),
            Clip(dto.Name),
            price,
            Finite(dto.ChangePercent),
            currency.Length is >= 2 and <= 5 ? currency : "USD",
            dto.PriceHint is >= 0 and <= 8 ? dto.PriceHint : null,
            bars,
            sessions)
        {
            ExchangeOffset = ExchangeTime.Offset(dto.OffsetSeconds),
            ZoneName = ExchangeTime.Zone(dto.Zone),
            QuoteType = Clip(dto.QuoteType),
            HasPrePost = dto.HasPrePost == true,
            Stats = new QuoteStats(
                Finite(dto.PreviousClose),
                Finite(dto.DayHigh),
                Finite(dto.DayLow),
                Finite(dto.YearHigh),
                Finite(dto.YearLow),
                dto.Volume is >= 0 ? dto.Volume : null,
                Clip(dto.Exchange)),
        };
    }

    private static QuoteEntryDto FromQuote(CachedAnswer<TickerQuote> entry)
    {
        var quote = entry.Value;

        return new QuoteEntryDto
        {
            Key = entry.Key,
            At = entry.At,
            Symbol = quote.Symbol,
            Name = quote.Name,
            Price = quote.Price,
            ChangePercent = quote.ChangePercent,
            Currency = quote.Currency,
            PriceHint = quote.PriceHint,
            Bars = [.. quote.Bars.Take(MaxBars).Select(bar => new List<double> { bar.Time, bar.Close })],
            PreStart = quote.Sessions.Pre?.Start,
            PreEnd = quote.Sessions.Pre?.End,
            RegularStart = quote.Sessions.Regular?.Start,
            RegularEnd = quote.Sessions.Regular?.End,
            PostStart = quote.Sessions.Post?.Start,
            PostEnd = quote.Sessions.Post?.End,
            OffsetSeconds = quote.ExchangeOffset is { } offset ? (long)offset.TotalSeconds : null,
            Zone = quote.ZoneName,
            QuoteType = quote.QuoteType,
            HasPrePost = quote.HasPrePost,
            PreviousClose = quote.Stats.PreviousClose,
            DayHigh = quote.Stats.DayHigh,
            DayLow = quote.Stats.DayLow,
            YearHigh = quote.Stats.YearHigh,
            YearLow = quote.Stats.YearLow,
            Volume = quote.Stats.Volume,
            Exchange = quote.Stats.Exchange,
        };
    }

    private static string Clip(string? text)
    {
        var value = text ?? string.Empty;
        return value.Length > MaxText ? value[..MaxText] : value;
    }

    private static double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;
}

// ---- on-disk shape ----------------------------------------------------------------------------
// Every member nullable, so a field missing on disk is told apart from a zero.

internal sealed class NetworkCacheFile
{
    public int SchemaVersion { get; set; }

    public List<FeedEntryDto?>? Feeds { get; set; }

    public List<QuoteEntryDto?>? Quotes { get; set; }
}

internal sealed class FeedEntryDto
{
    public string? Key { get; set; }

    public DateTimeOffset? At { get; set; }

    public string? Title { get; set; }

    public List<ItemDto?>? Items { get; set; }
}

internal sealed class ItemDto
{
    public string? Title { get; set; }

    public string? Link { get; set; }

    public DateTimeOffset? Published { get; set; }

    public string? Summary { get; set; }
}

internal sealed class QuoteEntryDto
{
    public string? Key { get; set; }

    public DateTimeOffset? At { get; set; }

    public string? Symbol { get; set; }

    public string? Name { get; set; }

    public double? Price { get; set; }

    public double? ChangePercent { get; set; }

    public string? Currency { get; set; }

    public int? PriceHint { get; set; }

    /// <summary>[time, close] pairs.</summary>
    public List<List<double>?>? Bars { get; set; }

    public double? PreStart { get; set; }

    public double? PreEnd { get; set; }

    public double? RegularStart { get; set; }

    public double? RegularEnd { get; set; }

    public double? PostStart { get; set; }

    public double? PostEnd { get; set; }

    public long? OffsetSeconds { get; set; }

    public string? Zone { get; set; }

    public string? QuoteType { get; set; }

    public bool? HasPrePost { get; set; }

    public double? PreviousClose { get; set; }

    public double? DayHigh { get; set; }

    public double? DayLow { get; set; }

    public double? YearHigh { get; set; }

    public double? YearLow { get; set; }

    public long? Volume { get; set; }

    public string? Exchange { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NetworkCacheFile))]
internal sealed partial class NetworkCacheJsonContext : JsonSerializerContext;
