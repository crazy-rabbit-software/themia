using Themia.Core.Diagnostics;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// [S-1300] [S-2660] Keeps the feed and quote caches across restarts, in the offline store's sealed web
/// shelf (<see cref="NetworkCacheStore"/> is the document), as weather keeps its own ([S-729]): read
/// back once at launch, marked restored so they draw at once with their real age, and saved after
/// every fetch that brings a new answer.
/// <para>
/// One writer: the store's. The document is built on its thread from the caches' snapshot, which
/// holds immutable answers. A detached store (a debug render) reads and writes nothing.
/// </para>
/// </summary>
internal static class NetworkCache
{
    private const string Scope = "network-cache";

    private static readonly Lock Gate = new();
    private static SyncVault? _vault;

    public static void Persist(SyncVault vault)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var read = vault.Load(SyncKind.Web, WebShelf.NetworkOwner, SyncJsonContext.Default.WebShelf, WebShelf.Version);
        if (read.Value is { } shelf)
        {
            if (NetworkCacheStore.Parse(shelf.Json, DateTimeOffset.Now) is { } snapshot)
            {
                foreach (var entry in snapshot.Feeds)
                {
                    FeedService.Cache.Restore(entry.Key, entry.Value, entry.At);
                }

                foreach (var entry in snapshot.Quotes)
                {
                    MarketService.Cache.Restore(entry.Key, entry.Value, entry.At);
                }

                Log.Info(Scope, $"restored {snapshot.Feeds.Count} feeds and {snapshot.Quotes.Count} quotes");
            }
            else
            {
                Log.Warn(Scope, "the stored feeds and quotes are from another version or damaged; starting without them");
            }
        }

        lock (Gate)
        {
            _vault = vault;
        }
    }

    /// <summary>Queue a save of both caches. Does nothing until <see cref="Persist"/> has run.</summary>
    public static void SaveSoon()
    {
        SyncVault? vault;
        lock (Gate)
        {
            vault = _vault;
        }

        if (vault is null)
        {
            return;
        }

        var feeds = FeedService.Cache.Snapshot();
        var quotes = MarketService.Cache.Snapshot();

        vault.SaveSoon(SyncKind.Web, WebShelf.NetworkOwner, () => new WebShelf
        {
            Owner = WebShelf.NetworkOwner,
            Json = NetworkCacheStore.Serialize(new NetworkCacheSnapshot(
                [.. feeds.Select(pair => new CachedAnswer<FeedDocument>(pair.Key, pair.Entry.At, pair.Entry.Value!))],
                [.. quotes.Select(pair => new CachedAnswer<TickerQuote>(pair.Key, pair.Entry.At, pair.Entry.Value!))])),
        }, SyncJsonContext.Default.WebShelf);
    }
}
