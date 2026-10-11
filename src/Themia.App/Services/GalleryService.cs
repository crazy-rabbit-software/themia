using Microsoft.UI.Dispatching;
using Themia.App.Services.Files;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-3410] Where every gallery widget gets its pictures: one shared source per folder, file or photo
/// of the day, owned by the host for the app's lifetime (<see cref="AppServices.Gallery"/>). A view
/// watches its source in Attach and lets go in Detach, and never walks a folder, fetches or decodes
/// itself.
/// <para>
/// A source is read once when its first widget joins, again when its folder watcher sees a picture
/// come or go, on the shortest refresh interval its widgets ask for, and at once on "Refresh images";
/// never because a widget was built again. A view switch and back therefore finds the list in hand,
/// the pictures decoded (<see cref="Pictures"/>, [S-3411]) and its own place
/// (<see cref="Positions"/>, [S-3412]): the same picture, at once. [S-3413] A source nobody shows
/// lingers, then is released with its watcher and its decoded pictures.
/// </para>
/// <para>UI thread only: reads run on the pool and come back to the dispatcher.</para>
/// </summary>
internal sealed class GalleryService : IDisposable
{
    private const string Scope = "gallery-service";

    private readonly Ticker _ticker;
    private readonly DispatcherQueue _dispatcher;
    private readonly LingerTable<string, Source> _sources;
    private readonly DispatcherQueueTimer _expiry;
    private bool _disposed;

    public GalleryService(Ticker ticker, DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _ticker = ticker;
        _dispatcher = dispatcher;
        _sources = new LingerTable<string, Source>(_ => new Source(), comparer: StringComparer.Ordinal);

        // [S-3413] Releasing a lingering source is housekeeping, not a correctness path: a one-shot
        // timer, armed only while something lingers, rather than a ticker subscription that would
        // count as a live widget's.
        _expiry = dispatcher.CreateTimer();
        _expiry.IsRepeating = false;
        _expiry.Tick += OnExpiry;
    }

    /// <summary>[S-3411] The decoded pictures every gallery widget shares.</summary>
    public ImageLoader Pictures { get; } = new();

    /// <summary>[S-3412] Each widget's place, by widget id.</summary>
    public GalleryPositions Positions { get; } = new();

    /// <summary>Sources held or lingering (diagnostics: the switch check).</summary>
    public int SourceCount => _sources.Count;

    /// <summary>Sources nobody shows, kept until their linger runs out.</summary>
    public int LingeringCount => _sources.Lingering;

    /// <summary>
    /// Watch a source. The current read, when there is one, is <see cref="GalleryWatch.Scan"/> at once;
    /// every read after this is <paramref name="onUpdate"/>, on the UI thread, with why it was read.
    /// Never calls back from inside this call. Dispose to let go.
    /// </summary>
    /// <param name="refresh">The widget's refresh interval ([S-772]); the shortest of a source's widgets wins.</param>
    public GalleryWatch Watch(GallerySourceKey key, TimeSpan refresh, Action<GalleryRefresh> onUpdate)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(onUpdate);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var source = _sources.Join(key.Id, out var created);
        var watch = new GalleryWatch(this, source, refresh, onUpdate);
        source.Watches.Add(watch);

        if (created)
        {
            source.Key = key;
            Log.Debug(Scope, $"opened {key.Source} source");
        }

        // A new source is read by its subscription's first tick; a source that came back from its
        // linger, or gained a widget, is not read again: what it holds is current (its watcher kept
        // it so), which is the whole point of sharing it.
        Reschedule(source, readNow: created);
        return watch;
    }

    /// <summary>[S-773] "Refresh images": read the source now, past the web cache's freshness.</summary>
    internal void Refresh(Source source)
    {
        if (!_disposed && !source.Closed)
        {
            Read(source, GalleryRefresh.Forced);
        }
    }

    /// <summary>[S-3411] The desktop was suspended: decoded pictures nobody shows go now.</summary>
    public void Trim() => Pictures.Trim();

    /// <summary>Called by a watch's Dispose. The source lingers when this was its last widget.</summary>
    internal void Leave(GalleryWatch watch, Source source)
    {
        source.Watches.Remove(watch);

        if (_disposed || source.Closed)
        {
            return;
        }

        if (!_sources.Leave(source.Key!.Id, Environment.TickCount64))
        {
            Log.Warn(Scope, "a gallery watch let go of a source it did not hold");
            return;
        }

        Reschedule(source, readNow: false);
        ArmExpiry();
    }

    /// <summary>
    /// [S-772] Subscribe the source at the shortest refresh interval its widgets ask for, or not at
    /// all while it lingers (its watcher still keeps a folder current). The ticker fires a new
    /// subscription at once: that is the first read of a new source, and skipped otherwise.
    /// </summary>
    private void Reschedule(Source source, bool readNow)
    {
        TimeSpan? interval = source.Watches.Count == 0 ? null : source.Watches.Min(w => w.Refresh);

        if (interval == source.Interval && (source.Tick is not null || interval is null))
        {
            return;
        }

        source.Tick?.Dispose();
        source.Tick = null;
        source.Interval = interval;

        if (interval is { } every)
        {
            source.SkipTick = !readNow;
            source.Tick = _ticker.SubscribeUnscoped(every, () => OnTick(source));
            source.SkipTick = false;
        }
    }

    private void OnTick(Source source)
    {
        if (source.SkipTick)
        {
            return;
        }

        Read(source, source.Reads == 0 ? GalleryRefresh.Initial : GalleryRefresh.Scheduled);
    }

    /// <summary>
    /// Read a source on the pool. One read out at a time: an ask while one is out is served by a
    /// second read after it, with the stronger reason ("Refresh images" over the schedule over a
    /// folder change), so a burst of changes is one more read, not a queue of them.
    /// </summary>
    private void Read(Source source, GalleryRefresh reason)
    {
        if (_disposed || source.Closed || source.Key is not { } key)
        {
            return;
        }

        if (source.Reading)
        {
            source.Pending = source.Pending is { } pending && pending > reason ? pending : reason;
            return;
        }

        source.Reading = true;
        source.Reads++;
        var token = source.Cancel.Token;
        var freshFor = (source.Interval ?? GalleryConfig.RescanInterval) / 2;

        Task<GalleryScan> reading = key.Source switch
        {
            GallerySource.Bing => Task.Run(() => ImageGallery.ReadBingAsync(freshFor, reason == GalleryRefresh.Forced, token), token),
            GallerySource.File => Task.Run(() => ImageGallery.ReadFile(key.Path), token),
            _ => Task.Run(() => ImageGallery.Read(new GalleryRequest(key.Path, key.Recursive), token), token),
        };

        _ = CompleteAsync(source, reading, reason);
    }

    private async Task CompleteAsync(Source source, Task<GalleryScan> reading, GalleryRefresh reason)
    {
        GalleryScan? scan = null;

        try
        {
            scan = await reading.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // ignore: the source was released; nothing will look at this read.
        }
        catch (Exception ex)
        {
            // The readers turn every failure into a status; anything here is a bug in this app.
            Log.Error(Scope, "a gallery read threw", ex);
            scan = GalleryScan.Of(GalleryStatus.Failed);
        }

        source.Reading = false;

        if (_disposed || source.Closed || scan is null)
        {
            return;
        }

        var same = Same(source.Scan, scan);

        if (!same)
        {
            source.Scan = scan;
        }

        // [S-3410] The watcher starts once the folder has been read: a path being typed letter by
        // letter in the settings is a source per keystroke, and none of those is worth a watcher.
        if (source.Folder is null && scan.Status == GalleryStatus.Ok && source.Key is { Source: GallerySource.Folder } folder)
        {
            source.Folder = FolderWatch.Start(folder.Path, () => Read(source, GalleryRefresh.Changed), subfolders: folder.Recursive, namesOnly: true);
        }

        // A read that found what was there already changes nothing for a folder change or a first
        // read; the schedule and "Refresh images" are still told, since they may deal again ([S-3412]).
        if (!same || reason is GalleryRefresh.Scheduled or GalleryRefresh.Forced)
        {
            Deliver(source, reason);
        }

        if (source.Pending is { } next)
        {
            source.Pending = null;
            Read(source, next);
        }
    }

    private static bool Same(GalleryScan a, GalleryScan b)
        => a.Status == b.Status
            && a.Credit == b.Credit
            && a.Paths.SequenceEqual(b.Paths, StringComparer.Ordinal);

    private static void Deliver(Source source, GalleryRefresh reason)
    {
        foreach (var watch in source.Watches.ToArray())
        {
            try
            {
                watch.Deliver(reason);
            }
            catch (Exception ex)
            {
                // One widget's handler must not stop the others hearing it.
                Log.Error(Scope, "a gallery widget's update threw", ex);
            }
        }
    }

    private void ArmExpiry()
    {
        if (_disposed)
        {
            return;
        }

        _expiry.Stop();

        if (_sources.NextExpiry is { } at)
        {
            _expiry.Interval = TimeSpan.FromMilliseconds(Math.Max(1, at - Environment.TickCount64));
            _expiry.Start();
        }
    }

    private void OnExpiry(DispatcherQueueTimer sender, object args)
    {
        try
        {
            var expired = _sources.Expire(Environment.TickCount64);

            foreach (var (_, source) in expired)
            {
                Close(source);
            }

            if (expired.Count > 0)
            {
                // The pictures only the released sources showed, unless another source still has them.
                var released = expired.SelectMany(e => e.Value.Scan.Paths).ToHashSet(StringComparer.Ordinal);
                var kept = _sources.Pairs().SelectMany(e => e.Value.Scan.Paths).ToHashSet(StringComparer.Ordinal);
                Pictures.Forget(path => released.Contains(path) && !kept.Contains(path));
                Log.Debug(Scope, $"released {expired.Count} gallery sources after their linger");
            }

            ArmExpiry();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "releasing lingering gallery sources failed", ex);
        }
    }

    private static void Close(Source source)
    {
        source.Closed = true;
        source.Tick?.Dispose();
        source.Tick = null;
        source.Folder?.Dispose();
        source.Folder = null;
        source.Cancel.Cancel();
        source.Watches.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _expiry.Stop();
        _expiry.Tick -= OnExpiry;

        foreach (var (_, source) in _sources.Clear())
        {
            Close(source);
        }

        Pictures.Clear();
    }

    /// <summary>One shared source: what it read, who watches it, and what keeps it current.</summary>
    internal sealed class Source
    {
        public GallerySourceKey? Key { get; set; }

        public GalleryScan Scan { get; set; } = GalleryScan.Of(GalleryStatus.Loading);

        public List<GalleryWatch> Watches { get; } = [];

        public IDisposable? Tick { get; set; }

        public TimeSpan? Interval { get; set; }

        /// <summary>The next tick is a resubscription's immediate one, not a due refresh.</summary>
        public bool SkipTick { get; set; }

        public FolderWatch? Folder { get; set; }

        public CancellationTokenSource Cancel { get; } = new();

        public bool Reading { get; set; }

        public GalleryRefresh? Pending { get; set; }

        /// <summary>Reads started, so the first is told apart from a scheduled one.</summary>
        public int Reads { get; set; }

        public bool Closed { get; set; }
    }
}

/// <summary>
/// One widget's hold on a gallery source. <see cref="Scan"/> is what the source read last; updates
/// arrive through the callback given to <see cref="GalleryService.Watch"/>. Dispose to let go, once.
/// </summary>
internal sealed class GalleryWatch : IDisposable
{
    private readonly GalleryService _service;
    private readonly GalleryService.Source _source;
    private Action<GalleryRefresh>? _onUpdate;

    internal GalleryWatch(GalleryService service, GalleryService.Source source, TimeSpan refresh, Action<GalleryRefresh> onUpdate)
    {
        _service = service;
        _source = source;
        Refresh = refresh;
        _onUpdate = onUpdate;
    }

    /// <summary>The widget's refresh interval.</summary>
    public TimeSpan Refresh { get; }

    /// <summary>What the source read last (Loading before its first read is back).</summary>
    public GalleryScan Scan => _source.Scan;

    /// <summary>[S-773] Read the source now.</summary>
    public void RefreshNow() => _service.Refresh(_source);

    internal void Deliver(GalleryRefresh reason) => _onUpdate?.Invoke(reason);

    public void Dispose()
    {
        if (_onUpdate is null)
        {
            return;
        }

        _onUpdate = null;
        _service.Leave(this, _source);
    }
}
