using System.Text.Json;
using System.Text.Json.Serialization;
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

/// <summary>
/// [S-1300] [S-2660] The feed and stock widgets' last good answers as a document, kept across
/// restarts in the sealed web shelf of the offline store (the original's localStorage cache,
/// S-23/S-29), so a cold launch draws the last headlines and price at once instead of "Loading" —
/// marked restored, so the header shows their real age in the caution colour until a fetch replaces them.
/// <para>
/// Never the state file: it is somebody else's data and must never ride along in an export.
/// Schema-versioned and validated on the way in, because every string in it came from a stranger
/// first: a document that is not this schema is refused whole; an entry that does not hold a usable
/// feed or quote is dropped, as is anything older than <see cref="MaxAge"/>.
/// </para>
/// </summary>
public static class NetworkCacheStore
{
    public const int SchemaVersion = 1;

    /// <summary>The oldest answer worth restoring.</summary>
    public static TimeSpan MaxAge { get; } = TimeSpan.FromDays(7);

    private const int MaxKeyLength = 2200;
    private const int MaxEntries = 64;
    private const int MaxItems = 200;
    private const int MaxBars = 4000;
    private const int MaxText = 1000;

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
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NetworkCacheFile))]
internal sealed partial class NetworkCacheJsonContext : JsonSerializerContext;
