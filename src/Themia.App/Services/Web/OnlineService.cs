using System.Globalization;
using Themia.Core.Diagnostics;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// [S-5102] [S-5122] [S-5141] The three newer online sources: open-meteo's air quality and 15-minute
/// precipitation, and the ECB's reference rates. All three are free and unauthenticated; none needs a
/// key, an account or a token, and nothing here substitutes a service that would.
/// <para>
/// Fetched only by <see cref="WebSync"/> ([S-2663]); a widget never calls this. Every reply is read as
/// text and handed to its Core parser, the one trust boundary for that service, and kept in a shared
/// cache keyed by exactly what changes the answer, so widgets on the same place share one request.
/// </para>
/// </summary>
internal static class OnlineService
{
    private const string Scope = "online";

    /// <summary>open-meteo's air quality API (CAMS). Free for non-commercial use with attribution.</summary>
    private const string AirEndpoint = "https://air-quality-api.open-meteo.com/v1/air-quality";

    /// <summary>open-meteo's forecast API, for <c>minutely_15</c>.</summary>
    private const string ForecastEndpoint = "https://api.open-meteo.com/v1/forecast";

    /// <summary>[S-5141] The ECB's last 90 days of reference rates: one document serves every pair.</summary>
    private static readonly Uri RatesUri = new("https://www.ecb.europa.eu/stats/eurofxref/eurofxref-hist-90d.xml");

    /// <summary>The cache key of the one rate table.</summary>
    public const string RatesKey = "ecb|90d";

    private static readonly WebCache<AirReading> AirCache = new(Scope);
    private static readonly WebCache<RainNowcast> RainCache = new(Scope);
    private static readonly WebCache<RateTable> RatesCache = new(Scope);

    public static string AirKey(double latitude, double longitude) => $"air|{Coordinate(latitude)},{Coordinate(longitude)}";

    public static string RainKey(double latitude, double longitude) => $"rain|{Coordinate(latitude)},{Coordinate(longitude)}";

    public static Fetched<AirReading>? PeekAir(double latitude, double longitude) => AirCache.Peek(AirKey(latitude, longitude));

    public static Fetched<RainNowcast>? PeekRain(double latitude, double longitude) => RainCache.Peek(RainKey(latitude, longitude));

    public static Fetched<RateTable>? PeekRates() => RatesCache.Peek(RatesKey);

    public static Task<Fetched<AirReading>> AirAsync(double latitude, double longitude, TimeSpan freshFor, bool force)
    {
        var uri = new Uri(
            $"{AirEndpoint}?latitude={Coordinate(latitude)}&longitude={Coordinate(longitude)}"
            + "&current=" + AirQualityWire.CurrentFields
            + "&timeformat=unixtime&timezone=auto");

        return GetAsync(AirCache, AirKey(latitude, longitude), uri, "application/json", AirQualityWire.Parse, freshFor, force);
    }

    public static Task<Fetched<RainNowcast>> RainAsync(double latitude, double longitude, TimeSpan freshFor, bool force)
    {
        // One step behind now (the quarter under way started there) and the window's eight ahead;
        // the hourly chance for the same three hours. Unix time: no zone arithmetic on our side.
        var uri = new Uri(
            $"{ForecastEndpoint}?latitude={Coordinate(latitude)}&longitude={Coordinate(longitude)}"
            + "&minutely_15=precipitation&past_minutely_15=1"
            + $"&forecast_minutely_15={(RainRules.WindowSteps + 1).ToString(CultureInfo.InvariantCulture)}"
            + "&hourly=precipitation_probability&past_hours=1&forecast_hours=3"
            + "&timeformat=unixtime&timezone=auto");

        return GetAsync(RainCache, RainKey(latitude, longitude), uri, "application/json", RainWire.Parse, freshFor, force);
    }

    public static Task<Fetched<RateTable>> RatesAsync(TimeSpan freshFor, bool force)
        => GetAsync(RatesCache, RatesKey, RatesUri, "application/xml", EcbWire.Parse, freshFor, force);

    private static async Task<Fetched<T>> GetAsync<T>(
        WebCache<T> cache,
        string key,
        Uri uri,
        string accept,
        Func<string?, WireResult<T>> parse,
        TimeSpan freshFor,
        bool force)
        where T : class
    {
        var loaded = false;
        var result = await cache.GetAsync(
            key,
            freshFor,
            async ct =>
            {
                loaded = true;
                var response = await WebFetch.GetAsync(uri, accept, ct).ConfigureAwait(false);
                var now = DateTimeOffset.Now;

                if (response.Status != FetchStatus.Ok)
                {
                    return Fetched<T>.Failed(response.Status, response.Detail, now);
                }

                var parsed = parse(response.Body);
                return parsed.Value is { } value
                    ? new Fetched<T>(FetchStatus.Ok, value, null, now)
                    : Fetched<T>.Failed(parsed.Status, parsed.Detail, now);
            },
            CancellationToken.None,
            force).ConfigureAwait(false);

        if (loaded && result.Status == FetchStatus.Ok)
        {
            SaveSoon();
        }

        return result;
    }

    // ---- [S-5161] persistence ----------------------------------------------------------------

    private static readonly Lock PersistGate = new();
    private static SyncVault? _vault;

    /// <summary>
    /// [S-5161] Restore the last answers from the sealed online shelf, and save there after each fetch
    /// that brings a new one. Only the app calls this; a detached store (a debug render) does nothing.
    /// </summary>
    public static void Persist(SyncVault vault)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var read = vault.Load(SyncKind.Web, WebShelf.OnlineOwner, SyncJsonContext.Default.WebShelf, WebShelf.Version);
        if (read.Value is { } shelf)
        {
            if (OnlineCacheStore.Parse(shelf.Json, DateTimeOffset.Now) is { } snapshot)
            {
                foreach (var (key, at, value) in snapshot.Air)
                {
                    AirCache.Restore(key, value, at);
                }

                foreach (var (key, at, value) in snapshot.Rain)
                {
                    RainCache.Restore(key, value, at);
                }

                foreach (var (key, at, value) in snapshot.Rates)
                {
                    RatesCache.Restore(key, value, at);
                }

                Log.Info(Scope, $"restored {snapshot.Air.Count} air readings, {snapshot.Rain.Count} rain nowcasts and {snapshot.Rates.Count} rate tables");
            }
            else
            {
                Log.Warn(Scope, "the stored air, rain and rates are from another version or damaged; starting without them");
            }
        }

        lock (PersistGate)
        {
            _vault = vault;
        }
    }

    private static void SaveSoon()
    {
        SyncVault? vault;
        lock (PersistGate)
        {
            vault = _vault;
        }

        if (vault is null)
        {
            return;
        }

        var replies = new List<CachedReply>();
        replies.AddRange(AirCache.Snapshot().Select(p => new CachedReply(OnlineSource.Air, p.Key, p.Entry.At, p.Entry.Value!.Source)));
        replies.AddRange(RainCache.Snapshot().Select(p => new CachedReply(OnlineSource.Rain, p.Key, p.Entry.At, p.Entry.Value!.Source)));
        replies.AddRange(RatesCache.Snapshot().Select(p => new CachedReply(OnlineSource.Rates, p.Key, p.Entry.At, p.Entry.Value!.Source)));

        vault.SaveSoon(SyncKind.Web, WebShelf.OnlineOwner, () => new WebShelf
        {
            Owner = WebShelf.OnlineOwner,
            Json = OnlineCacheStore.Serialize(replies),
        }, SyncJsonContext.Default.WebShelf);
    }

    /// <summary>Four decimals, invariant: about eleven metres, far finer than either model.</summary>
    private static string Coordinate(double value)
        => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
}
