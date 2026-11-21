using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Devices;

/// <summary>
/// [S-4474] [S-4470] The paired Bluetooth devices every Bluetooth widget shows, read once for all of them
/// (<see cref="BluetoothInventory"/>): single-flight on the pool, only while a widget watches, every
/// <see cref="Cadence"/> and on Refresh. It reads no battery: a connected device's is the host's one battery
/// inventory's (<see cref="DeviceBatteryService"/>, watched while this is), joined by device container, so the Bluetooth
/// list and Device batteries say one level. UI thread only, except the pass itself.
/// </summary>
internal sealed class BluetoothDeviceService : IBluetoothDevices
{
    private const string Scope = "bluetooth";

    /// <summary>[S-1259] How often the radios are read again: a pairing or a connection changes in seconds, not faster.</summary>
    internal static readonly TimeSpan Cadence = TimeSpan.FromSeconds(30);

    private static BluetoothDeviceService? _shared;

    private readonly DispatcherQueue _dispatcher;
    private readonly List<Watcher> _watchers = [];
    private BluetoothInventory.Pass _pass = BluetoothInventory.Pass.Of(BluetoothStatus.Loading);
    private IDisposable? _tick;
    private IDisposable? _batteries;
    private CancellationTokenSource? _cancel;
    private bool _again;

    private BluetoothDeviceService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>The one service, made on first use from the UI thread.</summary>
    public static BluetoothDeviceService Shared => _shared ??= new BluetoothDeviceService(
        DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("the Bluetooth service is made on the UI thread"));

    public BluetoothSnapshot Last
        => _pass.Snapshot.Status == BluetoothStatus.Ok
            ? _pass.Snapshot with { Devices = BluetoothReadings.WithBattery(_pass.Snapshot.Devices, _pass.Containers, BluetoothReadings.Batteries(DeviceBatteryService.Shared.Last.Readings)) }
            : _pass.Snapshot;

    public bool Busy { get; private set; }

    private bool Signal => _watchers.Any(w => w.Signal);

    public IDisposable Watch(Ticker ticker, bool signal, Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(onChanged);

        var watcher = new Watcher(this, signal, onChanged);
        var first = _watchers.Count == 0;
        _watchers.Add(watcher);

        if (first)
        {
            // [S-4470] The batteries are the one inventory's: it reads while a Bluetooth widget shows them.
            _batteries = DeviceBatteryService.Shared.Watch(ticker, Tell);

            // The ticker fires at once on subscribe: that is the first pass.
            _tick = ticker.SubscribeUnscoped(Cadence, Refresh);
        }
        else if (signal && _pass.Snapshot.Status == BluetoothStatus.Ok && !_pass.Snapshot.Devices.Any(d => d.SignalDbm is not null))
        {
            // A widget that wants the signal now: read with it, rather than wait for the cadence.
            Refresh();
        }

        return watcher;
    }

    public void Refresh()
    {
        if (_watchers.Count == 0)
        {
            return;
        }

        if (Busy)
        {
            _again = true;
            return;
        }

        Busy = true;
        Tell();
        _ = ReadAsync(Signal);
    }

    private async Task ReadAsync(bool signal)
    {
        _cancel ??= new CancellationTokenSource();
        var token = _cancel.Token;
        BluetoothInventory.Pass pass;

        try
        {
            pass = await Task.Run(() => BluetoothInventory.ReadAsync(signal, token), token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // ignore: the last widget let go. A widget that came back meanwhile asked again.
            Busy = false;
            if (_watchers.Count > 0 && _again)
            {
                _again = false;
                Refresh();
            }

            return;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the paired-device pass failed", ex);
            pass = BluetoothInventory.Pass.Of(BluetoothStatus.Failed);
        }

        Busy = false;
        if (_watchers.Count == 0)
        {
            return;
        }

        _pass = pass;
        Tell();

        if (_again)
        {
            _again = false;
            Refresh();
        }
    }

    private void Tell()
    {
        foreach (var watcher in _watchers.ToArray())
        {
            watcher.Tell();
        }
    }

    private void Leave(Watcher watcher)
    {
        _watchers.Remove(watcher);
        if (_watchers.Count > 0)
        {
            return;
        }

        _tick?.Dispose();
        _tick = null;
        _batteries?.Dispose();
        _batteries = null;
        _cancel?.Cancel();
        _cancel?.Dispose();
        _cancel = null;
        _again = false;
        _pass = BluetoothInventory.Pass.Of(BluetoothStatus.Loading);
    }

    private sealed class Watcher(BluetoothDeviceService owner, bool signal, Action onChanged) : IDisposable
    {
        private bool _disposed;

        public bool Signal => signal;

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
                Log.Error(Scope, "a Bluetooth widget failed to take a new pass", ex);
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
