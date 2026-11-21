using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace Themia.App.Services.Devices;

/// <summary>
/// The paired Bluetooth devices, from the WinRT device enumeration.
/// <para>
/// Every call here is slow in the way a widget cannot afford on the UI thread: enumerating the
/// radios is an RPC, and the signal scan listens for seconds. So this is <c>async</c> end to end and is called from a background load with a
/// cancellation token, exactly as the folder listing is.
/// </para>
/// <para>
/// The distinction it exists to preserve is between <i>nothing found</i> and <i>nowhere to look</i>.
/// A desktop with no radio, a laptop with the radio switched off, and a machine with a radio and no
/// pairings are three different facts about the PC, and a widget that renders all three as an empty
/// list has thrown away the only useful thing it knew.
/// </para>
/// <para>
/// [S-4470] It reads no battery of its own: a connected device's battery is the host's one battery inventory's
/// (<see cref="DeviceBatteryInventory"/>), joined by device container (<see cref="BluetoothReadings.WithBattery"/>); a
/// pass hands back each entry's container for that.
/// </para>
/// </summary>
internal static class BluetoothInventory
{
    /// <summary>One pass: what it found, and each entry's device container, by index (null where unknown).</summary>
    public sealed record Pass(BluetoothSnapshot Snapshot, IReadOnlyList<Guid?> Containers)
    {
        public static Pass Of(BluetoothStatus status) => new(BluetoothSnapshot.Of(status), []);
    }

    private const string Scope = "bluetooth";

    /// <summary>The Aep property carrying whether a paired device is connected right now.</summary>
    private const string ConnectedProperty = "System.Devices.Aep.IsConnected";

    /// <summary>[S-1256] The association endpoint's own signal reading, in dBm, where it has one.</summary>
    private const string SignalProperty = "System.Devices.Aep.SignalStrength";

    /// <summary>[S-1256] The device's address, to match an LE advertisement to a paired device.</summary>
    private const string AddressProperty = "System.Devices.Aep.DeviceAddress";

    /// <summary>[S-3252] The association endpoint's device container: where its battery is found.</summary>
    private const string AepContainerProperty = "System.Devices.Aep.ContainerId";

    private static readonly string[] RequestedProperties = [ConnectedProperty, AepContainerProperty];

    private static readonly string[] SignalProperties = [ConnectedProperty, AepContainerProperty, SignalProperty, AddressProperty];

    /// <summary>
    /// [S-1256] How long the LE scan for signal readings listens. Bounded, and run only while the
    /// signal column is switched on: a scan keeps the radio busy, and an LE device that advertises
    /// at all does so several times a second, so a few seconds hear every one that will.
    /// </summary>
    private static readonly TimeSpan SignalScan = TimeSpan.FromSeconds(3);

    public static async Task<Pass> ReadAsync(bool withSignal, CancellationToken token)
    {
        var radio = await RadioStateAsync(token).ConfigureAwait(false);

        if (radio != BluetoothStatus.Ok)
        {
            return Pass.Of(radio);
        }

        try
        {
            var found = new List<BluetoothDeviceEntry>();

            // [S-3252] Each entry's device container, by index, for the battery pass.
            var containers = new List<Guid?>();

            var addresses = new Dictionary<int, ulong>();

            found.AddRange(await EnumerateAsync(
                BluetoothDevice.GetDeviceSelectorFromPairingState(true), withSignal, null, containers, token).ConfigureAwait(false));

            found.AddRange(await EnumerateAsync(
                BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), withSignal, (found.Count, addresses), containers, token).ConfigureAwait(false));

            if (withSignal && addresses.Count > 0)
            {
                await AddAdvertisedSignalAsync(found, addresses, token).ConfigureAwait(false);
            }

            return new Pass(new BluetoothSnapshot(BluetoothStatus.Ok, found), containers);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not enumerate paired devices", ex);
            return Pass.Of(BluetoothStatus.Failed);
        }
    }

    /// <summary>
    /// Whether there is a radio to ask at all, and whether it is on.
    /// <para>
    /// Asked before the enumeration rather than inferred from it: <c>FindAllAsync</c> returns an
    /// empty list for a machine with no radio, a machine with the radio off and a machine with
    /// nothing paired, and those need three different sentences.
    /// </para>
    /// </summary>
    private static async Task<BluetoothStatus> RadioStateAsync(CancellationToken token)
    {
        try
        {
            var access = await Radio.RequestAccessAsync().AsTask(token).ConfigureAwait(false);

            if (access is RadioAccessStatus.DeniedByUser or RadioAccessStatus.DeniedBySystem)
            {
                return BluetoothStatus.PermissionDenied;
            }

            var radios = await Radio.GetRadiosAsync().AsTask(token).ConfigureAwait(false);

            var bluetooth = radios.Where(radio => radio.Kind == RadioKind.Bluetooth).ToList();

            if (bluetooth.Count == 0)
            {
                return BluetoothStatus.NoRadio;
            }

            // Any radio being on is enough: a machine with two adapters can pair on either.
            return bluetooth.Any(radio => radio.State == RadioState.On)
                ? BluetoothStatus.Ok
                : BluetoothStatus.RadioOff;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The radio API is the part most likely to be unavailable on an older or locked-down
            // build. Falling through to the enumeration is better than refusing to show devices
            // that are plainly there; the enumeration's own result then names the state.
            Log.Warn(Scope, "could not read the radio state; enumerating anyway", ex);
            return BluetoothStatus.Ok;
        }
    }

    /// <summary>
    /// One selector's worth of devices, with no battery levels yet. With <paramref name="le"/>, the
    /// LE entries' addresses are noted by their index in the combined list (offset by the entries
    /// already found) for the advertisement scan. Each entry's device container is appended to
    /// <paramref name="containers"/>, in step with the entries.
    /// </summary>
    private static async Task<List<BluetoothDeviceEntry>> EnumerateAsync(
        string selector,
        bool withSignal,
        (int Offset, Dictionary<int, ulong> Addresses)? le,
        List<Guid?> containers,
        CancellationToken token)
    {
        var devices = new List<BluetoothDeviceEntry>();

        var found = await DeviceInformation
            .FindAllAsync(selector, withSignal ? SignalProperties : RequestedProperties)
            .AsTask(token)
            .ConfigureAwait(false);

        foreach (var info in found)
        {
            token.ThrowIfCancellationRequested();

            var connected = info.Properties.TryGetValue(ConnectedProperty, out var value)
                && value is bool flag
                && flag;

            containers.Add(info.Properties.TryGetValue(AepContainerProperty, out var box) && box is Guid container ? container : null);

            int? signal = null;

            if (withSignal)
            {
                // A boxed int when present; anything else is no reading.
                signal = info.Properties.TryGetValue(SignalProperty, out var raw) && raw is int dbm ? dbm : null;

                if (le is { } target)
                {
                    var address = info.Properties.TryGetValue(AddressProperty, out var text) && text is string written
                        ? BluetoothReadings.ParseAddress(written)
                        : null;

                    if ((address ?? BluetoothReadings.AddressFromId(info.Id)) is { } known)
                    {
                        target.Addresses[target.Offset + devices.Count] = known;
                    }
                }
            }

            // The name is the device's own advertisement, as it came: the widget's display rule
            // normalises it, and it is only ever drawn.
            devices.Add(new BluetoothDeviceEntry(
                info.Name ?? string.Empty,
                info.Id,
                connected,
                BluetoothDeviceKind.Unknown,
                null,
                signal));
        }

        return devices;
    }

    /// <summary>
    /// [S-1256] Listen to LE advertisements for <see cref="SignalScan"/> and give each paired LE
    /// device that was heard its strongest reading. Passive: nothing is sent, nothing connects. A
    /// radio that cannot scan (or refuses this app) leaves the readings as they were.
    /// </summary>
    private static async Task AddAdvertisedSignalAsync(List<BluetoothDeviceEntry> devices, Dictionary<int, ulong> addresses, CancellationToken token)
    {
        var heard = new Dictionary<ulong, short>();
        var gate = new object();

        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Passive };

        void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            lock (gate)
            {
                if (!heard.TryGetValue(args.BluetoothAddress, out var best) || args.RawSignalStrengthInDBm > best)
                {
                    heard[args.BluetoothAddress] = args.RawSignalStrengthInDBm;
                }
            }
        }

        watcher.Received += OnReceived;

        try
        {
            watcher.Start();
            await Task.Delay(SignalScan, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Debug(Scope, $"no LE scan for signal readings: {ex.GetType().Name}");
        }
        finally
        {
            watcher.Received -= OnReceived;

            try
            {
                watcher.Stop();
            }
            catch (Exception ex)
            {
                // ignore: a watcher that never started cannot stop, and nothing is left running.
                Log.Debug(Scope, $"LE scan stop: {ex.GetType().Name}");
            }
        }

        lock (gate)
        {
            foreach (var (index, address) in addresses)
            {
                if (index < devices.Count && heard.TryGetValue(address, out var dbm))
                {
                    devices[index] = devices[index] with { SignalDbm = dbm };
                }
            }
        }
    }
}
