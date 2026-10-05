using Themia.Core.Diagnostics;
using Themia.Core.Storage;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// [S-1300] Keeps the feed and quote caches across restarts (<see cref="NetworkCacheStore"/>), as
/// weather keeps its own ([S-729]): read back once at launch, marked restored so they draw at once
/// as stale and are fetched again, and saved after every fetch that brings a new answer.
/// <para>
/// One writer: each save runs after the one before it, off the UI thread, with the snapshot taken
/// when it was queued. Only the app calls <see cref="Persist"/>; a debug render never does, so a
/// render neither reads nor writes the user's file.
/// </para>
/// </summary>
internal static class NetworkCache
{
    private const string Scope = "network-cache";

    private static readonly Lock Gate = new();
    private static string? _path;
    private static Task _writes = Task.CompletedTask;

    public static void Persist(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var (snapshot, outcome) = NetworkCacheStore.Load(path, DateTimeOffset.Now);

        foreach (var entry in snapshot.Feeds)
        {
            FeedService.Cache.Restore(entry.Key, entry.Value, entry.At);
        }

        foreach (var entry in snapshot.Quotes)
        {
            MarketService.Cache.Restore(entry.Key, entry.Value, entry.At);
        }

        if (outcome == NetworkCacheLoadOutcome.Loaded)
        {
            Log.Info(Scope, $"restored {snapshot.Feeds.Count} feeds and {snapshot.Quotes.Count} quotes");
        }

        lock (Gate)
        {
            _path = path;
        }
    }

    /// <summary>Queue a save of both caches. Does nothing until <see cref="Persist"/> has run.</summary>
    public static void SaveSoon()
    {
        lock (Gate)
        {
            if (_path is not { } path)
            {
                return;
            }

            var snapshot = new NetworkCacheSnapshot(
                [.. FeedService.Cache.Snapshot().Select(pair => new CachedAnswer<FeedDocument>(pair.Key, pair.Entry.At, pair.Entry.Value!))],
                [.. MarketService.Cache.Snapshot().Select(pair => new CachedAnswer<TickerQuote>(pair.Key, pair.Entry.At, pair.Entry.Value!))]);

            _writes = _writes.ContinueWith(
                _ =>
                {
                    try
                    {
                        NetworkCacheStore.Save(path, snapshot);
                    }
                    catch (Exception ex)
                    {
                        // Save handles the I/O failures it expects; anything here is a bug, logged
                        // rather than left to fault a task nobody observes.
                        Log.Error(Scope, "saving the feed and quote cache threw", ex);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }
}
