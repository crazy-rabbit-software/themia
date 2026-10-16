using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Drives;

/// <summary>
/// [S-4655] The drives every drive widget shows — the disk meter's volumes, Drive,
/// Removable drives — read once for all of them, the one place the drives are walked. Drive and
/// Removable drives watch it themselves; the system hub's disk source holds a watch for its own, so the
/// meter is read from app start as every hub source is ([S-2513]). One walk (<see cref="DriveCatalog"/>) on the pool, single-flight, whenever Windows
/// says a volume came or went (<see cref="DevicesChanged"/>, the command window's
/// <c>WM_DEVICECHANGE</c>), a network drive answered, or the space may have moved (every 30 s, on
/// the shared ticker, only while a widget watches). A walk that changes nothing a widget shows
/// (<see cref="DriveRules.SameAs"/>) tells nobody. UI thread only, except the walk itself.
/// </summary>
internal sealed class DriveService
{
    private const string Scope = "drives";

    /// <summary>How often space is read again while a widget shows it: free space moves slowly, and arrivals are events.</summary>
    private static readonly TimeSpan Cadence = TimeSpan.FromSeconds(30);

    private static DriveService? _shared;

    private readonly DispatcherQueue _dispatcher;
    private readonly List<Watcher> _watchers = [];
    private IDisposable? _tick;
    private bool _walking;
    private bool _again;

    private DriveService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>The one service, made on first use from the UI thread.</summary>
    public static DriveService Shared => _shared ??= new DriveService(
        DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("the drive service is made on the UI thread"));

    /// <summary>The last walk; empty until the first one is in (<see cref="HasValue"/>).</summary>
    public IReadOnlyList<DriveEntry> Drives { get; private set; } = [];

    public bool HasValue { get; private set; }

    /// <summary>
    /// The drives for a settings page: the last walk while something watches, else one walk now
    /// (local drives at once, a share pending) — a page opened with no drive widget anywhere.
    /// </summary>
    public IReadOnlyList<DriveEntry> Now() => HasValue ? Drives : DriveCatalog.Walk(network: true);

    /// <summary>A volume arrived or left (or a network drive was mapped or dropped): walk now. Any caller on the UI thread.</summary>
    public static void DevicesChanged() => _shared?.Refresh();

    /// <summary>Watch the drives; <paramref name="onChanged"/> runs on the UI thread after each walk that changed something. Dispose to let go.</summary>
    public IDisposable Watch(Ticker ticker, Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(onChanged);

        var watcher = new Watcher(this, onChanged);
        _watchers.Add(watcher);

        if (_watchers.Count == 1)
        {
            DriveCatalog.NetworkAnswered += OnNetworkAnswered;

            // The ticker fires at once on subscribe: that is the first walk.
            _tick = ticker.SubscribeUnscoped(Cadence, Refresh);
        }

        return watcher;
    }

    /// <summary>Walk again now (Refresh on a card's menu, a device change). A walk already out is followed by one more.</summary>
    public void Refresh()
    {
        if (_watchers.Count == 0)
        {
            return;
        }

        if (_walking)
        {
            _again = true;
            return;
        }

        _walking = true;
        _ = WalkAsync();
    }

    private async Task WalkAsync()
    {
        IReadOnlyList<DriveEntry>? walked = null;

        try
        {
            walked = await Task.Run(() => DriveCatalog.Walk(network: true)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the drive walk failed", ex);
        }

        _walking = false;

        if (walked is not null && (!HasValue || !DriveRules.SameAs(Drives, walked)))
        {
            Drives = walked;
            HasValue = true;

            foreach (var watcher in _watchers.ToArray())
            {
                watcher.Tell();
            }
        }

        if (_again)
        {
            _again = false;
            Refresh();
        }
    }

    // A pool thread: only a hand-off.
    private void OnNetworkAnswered()
    {
        if (!_dispatcher.TryEnqueue(Refresh))
        {
            Log.Debug(Scope, "a network drive answered while the dispatcher was shutting down");
        }
    }

    private void Leave(Watcher watcher)
    {
        _watchers.Remove(watcher);

        if (_watchers.Count == 0)
        {
            DriveCatalog.NetworkAnswered -= OnNetworkAnswered;
            _tick?.Dispose();
            _tick = null;
        }
    }

    private sealed class Watcher(DriveService owner, Action onChanged) : IDisposable
    {
        private bool _disposed;

        public void Tell()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                onChanged();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a drive widget failed to take a new walk", ex);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                owner.Leave(this);
            }
        }
    }
}
