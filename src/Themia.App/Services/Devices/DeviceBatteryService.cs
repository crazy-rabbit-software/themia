using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Sdk.Devices;
using Themia.Sdk.WinUI;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Gaming.Input;

namespace Themia.App.Services.Devices;

/// <summary>
/// [S-4470] [S-4472] [S-4473] The peripherals' batteries every battery widget shows, read once for all
/// of them (<see cref="DeviceBatteryInventory"/>): single-flight on the pool, only while a widget watches.
/// <para>
/// Events where Windows gives them: device watchers on the paired Bluetooth devices (a connection
/// coming or going), on the device containers that carry a level, and on the device nodes of the
/// connected Bluetooth containers (their level changing, rebuilt as that set changes), and the game
/// controllers arriving and leaving. Those reads take what Windows holds and keep the GATT levels of
/// the last pass. What Windows sends no event for (a GATT level, a controller's report) is read again
/// on the shared ticker every <see cref="Cadence"/>. UI thread only, except the read itself.
/// </para>
/// </summary>
internal sealed class DeviceBatteryService : IDeviceBatteries
{
    private const string Scope = "device-batteries";

    /// <summary>
    /// [S-4473] How often what has no event is read again: a battery moves a percent in minutes, and a
    /// GATT read wakes the device's radio, so not faster.
    /// </summary>
    internal static readonly TimeSpan Cadence = TimeSpan.FromMinutes(2);

    private static DeviceBatteryService? _shared;

    private readonly DispatcherQueue _dispatcher;
    private readonly List<Watcher> _watchers = [];
    private readonly List<DeviceWatch> _devices = [];
    private DeviceWatch? _nodes;
    private HashSet<Guid> _nodeContainers = [];
    private IReadOnlyDictionary<string, int> _gatt = new Dictionary<string, int>();
    private IDisposable? _tick;
    private CancellationTokenSource? _cancel;
    private bool _again;
    private bool _againFull;

    private DeviceBatteryService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>The one service, made on first use from the UI thread.</summary>
    public static DeviceBatteryService Shared => _shared ??= new DeviceBatteryService(
        DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("the device battery service is made on the UI thread"));

    public DeviceBatterySnapshot Last { get; private set; } = DeviceBatterySnapshot.Loading;

    public bool Busy { get; private set; }

    public IDisposable Watch(Ticker ticker, Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(onChanged);

        var watcher = new Watcher(this, onChanged);
        _watchers.Add(watcher);

        if (_watchers.Count == 1)
        {
            Start();

            // The ticker fires at once on subscribe: that is the first read.
            _tick = ticker.SubscribeUnscoped(Cadence, Refresh);
        }

        return watcher;
    }

    public void Refresh() => Read(full: true);

    /// <summary>Read now; a read already out is followed by one more (full if either asked for it).</summary>
    private void Read(bool full)
    {
        if (_watchers.Count == 0)
        {
            return;
        }

        if (Busy)
        {
            _again = true;
            _againFull |= full;
            return;
        }

        Busy = true;
        Tell();
        _ = ReadAsync(full);
    }

    private async Task ReadAsync(bool full)
    {
        _cancel ??= new CancellationTokenSource();
        var token = _cancel.Token;
        DeviceBatteryInventory.Pass? pass = null;
        var failed = false;

        try
        {
            var known = _gatt;
            pass = await Task.Run(() => DeviceBatteryInventory.ReadAsync(known, full, token), token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // ignore: the last widget let go. A widget that came back meanwhile asked again.
            Busy = false;
            if (_watchers.Count > 0 && _again)
            {
                _again = false;
                _againFull = false;
                Read(full: true);
            }

            return;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the device battery read failed", ex);
            failed = true;
        }

        Busy = false;

        if (_watchers.Count == 0)
        {
            return;
        }

        if (pass is not null)
        {
            _gatt = pass.Gatt;
            WatchNodes(pass.BluetoothContainers);
        }

        var next = failed
            ? new DeviceBatterySnapshot(DeviceBatteryStatus.Failed, [])
            : new DeviceBatterySnapshot(DeviceBatteryStatus.Ok, [.. pass!.Readings.OrderBy(r => r.Id, StringComparer.Ordinal)]);

        if (next.Status != Last.Status || !next.Readings.SequenceEqual(Last.Readings))
        {
            Last = next;
        }

        // Told either way: Busy went back.
        Tell();

        if (_again)
        {
            var againFull = _againFull;
            _again = false;
            _againFull = false;
            Read(againFull);
        }
    }

    private void Tell()
    {
        foreach (var watcher in _watchers.ToArray())
        {
            watcher.Tell();
        }
    }

    // ---- what Windows tells ([S-4472]) -----------------------------------------------------------

    private void Start()
    {
        _devices.Add(DeviceWatch.Of(this, BluetoothDevice.GetDeviceSelectorFromPairingState(true), DeviceInformationInventoryProps.Aep, null));
        _devices.Add(DeviceWatch.Of(this, BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), DeviceInformationInventoryProps.Aep, null));
        _devices.Add(DeviceWatch.Of(this, DeviceBatteryInventory.BatteryContainers, DeviceInformationInventoryProps.Container, DeviceInformationKind.DeviceContainer));

        try
        {
            RawGameController.RawGameControllerAdded += OnControllersChanged;
            RawGameController.RawGameControllerRemoved += OnControllersChanged;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "no game controller events; controllers are read on the cadence", ex);
        }
    }

    private void Stop()
    {
        foreach (var watch in _devices)
        {
            watch.Dispose();
        }

        _devices.Clear();
        _nodes?.Dispose();
        _nodes = null;
        _nodeContainers = [];
        _gatt = new Dictionary<string, int>();

        try
        {
            RawGameController.RawGameControllerAdded -= OnControllersChanged;
            RawGameController.RawGameControllerRemoved -= OnControllersChanged;
        }
        catch (Exception ex)
        {
            // ignore: nothing was subscribed where the add failed.
            Log.Debug(Scope, $"controller events: {ex.GetType().Name}");
        }

        _cancel?.Cancel();
        _cancel?.Dispose();
        _cancel = null;
        _again = false;
        _againFull = false;
        Last = DeviceBatterySnapshot.Loading;
    }

    /// <summary>[S-4472] Follow the device nodes of the connected Bluetooth containers, whose battery property changes as the level does.</summary>
    private void WatchNodes(IReadOnlySet<Guid> containers)
    {
        if (_nodeContainers.SetEquals(containers))
        {
            return;
        }

        _nodes?.Dispose();
        _nodes = null;
        _nodeContainers = [.. containers];

        if (containers.Count > 0)
        {
            _nodes = DeviceWatch.Of(this, DeviceBatteryInventory.NodesOf(containers), DeviceInformationInventoryProps.Node, DeviceInformationKind.Device);
        }
    }

    // A pool thread (Windows.Gaming.Input's): only a hand-off.
    private void OnControllersChanged(object? sender, RawGameController controller) => Poke(full: true);

    /// <summary>Something Windows watches changed: read on the UI thread. Any thread.</summary>
    private void Poke(bool full)
    {
        if (!_dispatcher.TryEnqueue(() => Read(full)))
        {
            Log.Debug(Scope, "a device change arrived while the dispatcher was shutting down");
        }
    }

    private void Leave(Watcher watcher)
    {
        _watchers.Remove(watcher);

        if (_watchers.Count == 0)
        {
            _tick?.Dispose();
            _tick = null;
            Stop();
        }
    }

    /// <summary>The properties each watcher asks for: an update arrives when one of them changes.</summary>
    private static class DeviceInformationInventoryProps
    {
        public static readonly string[] Aep = DeviceBatteryInventory.AepProperties;
        public static readonly string[] Container = [DeviceBatteryInventory.BatteryLifeProperty, DeviceBatteryInventory.ChargingProperty, DeviceBatteryInventory.ContainerConnectedProperty];
        public static readonly string[] Node = DeviceBatteryInventory.NodeProperties;
    }

    /// <summary>
    /// One device watcher: its first enumeration is the read's own business, so only what comes after
    /// <c>EnumerationCompleted</c> pokes a read. Its handlers are COM callbacks: they catch, log and return.
    /// </summary>
    private sealed class DeviceWatch : IDisposable
    {
        private readonly DeviceBatteryService _owner;
        private readonly DeviceWatcher? _watcher;
        private volatile bool _completed;
        private volatile bool _disposed;

        private DeviceWatch(DeviceBatteryService owner, DeviceWatcher? watcher)
        {
            _owner = owner;
            _watcher = watcher;
        }

        public static DeviceWatch Of(DeviceBatteryService owner, string aqs, string[] properties, DeviceInformationKind? kind)
        {
            DeviceWatcher? watcher = null;

            try
            {
                watcher = kind is { } k
                    ? DeviceInformation.CreateWatcher(aqs, properties, k)
                    : DeviceInformation.CreateWatcher(aqs, properties);
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "could not create a device watcher; that path is read on the cadence only", ex);
                return new DeviceWatch(owner, null);
            }

            var watch = new DeviceWatch(owner, watcher);
            watcher.Added += watch.OnAdded;
            watcher.Updated += watch.OnUpdated;
            watcher.Removed += watch.OnUpdated;
            watcher.EnumerationCompleted += watch.OnCompleted;

            try
            {
                watcher.Start();
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "could not start a device watcher; that path is read on the cadence only", ex);
            }

            return watch;
        }

        private void OnAdded(DeviceWatcher sender, DeviceInformation info) => Changed();

        private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update) => Changed();

        private void OnCompleted(DeviceWatcher sender, object args) => _completed = true;

        private void Changed()
        {
            try
            {
                if (_completed && !_disposed)
                {
                    _owner.Poke(full: false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a device watcher's event failed", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed || _watcher is null)
            {
                _disposed = true;
                return;
            }

            _disposed = true;
            _watcher.Added -= OnAdded;
            _watcher.Updated -= OnUpdated;
            _watcher.Removed -= OnUpdated;
            _watcher.EnumerationCompleted -= OnCompleted;

            try
            {
                if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                {
                    _watcher.Stop();
                }
            }
            catch (Exception ex)
            {
                // ignore: a watcher that already stopped (or aborted) has nothing left running.
                Log.Debug(Scope, $"device watcher stop: {ex.GetType().Name}");
            }
        }
    }

    private sealed class Watcher(DeviceBatteryService owner, Action onChanged) : IDisposable
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
                Log.Error(Scope, "a battery widget failed to take a new read", ex);
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
