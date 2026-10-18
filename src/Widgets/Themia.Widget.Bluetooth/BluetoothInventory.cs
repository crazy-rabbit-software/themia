using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Storage.Streams;

namespace Themia.App.Services;

/// <summary>What one enumeration found, and why it found nothing when it found nothing.</summary>
/// <param name="Status">The state to draw. Only <see cref="BluetoothStatus.Ok"/> has rows.</param>
/// <param name="Devices">
/// What the two enumerations returned, as they returned it. Unmerged and unordered on purpose:
/// folding the dual-mode duplicates and choosing the order are pure decisions, so they live in
/// <see cref="BluetoothRules.Arrange"/> where they can be tested without a radio.
/// </param>
public readonly record struct BluetoothSnapshot(BluetoothStatus Status, IReadOnlyList<BluetoothDeviceEntry> Devices)
{
    public static BluetoothSnapshot Of(BluetoothStatus status) => new(status, []);
}

/// <summary>
/// The paired Bluetooth devices, from the WinRT device enumeration.
/// <para>
/// Every call here is slow in the way a widget cannot afford on the UI thread: enumerating the
/// radios is an RPC, and a GATT battery read opens a BLE connection and waits on the device to
/// answer. So this is <c>async</c> end to end and is called from a background load with a
/// cancellation token, exactly as the folder listing is.
/// </para>
/// <para>
/// The distinction it exists to preserve is between <i>nothing found</i> and <i>nowhere to look</i>.
/// A desktop with no radio, a laptop with the radio switched off, and a machine with a radio and no
/// pairings are three different facts about the PC, and a widget that renders all three as an empty
/// list has thrown away the only useful thing it knew.
/// </para>
/// </summary>
internal static class BluetoothInventory
{
    private const string Scope = "bluetooth";

    /// <summary>The Aep property carrying whether a paired device is connected right now.</summary>
    private const string ConnectedProperty = "System.Devices.Aep.IsConnected";

    /// <summary>[S-1256] The association endpoint's own signal reading, in dBm, where it has one.</summary>
    private const string SignalProperty = "System.Devices.Aep.SignalStrength";

    /// <summary>[S-1256] The device's address, to match an LE advertisement to a paired device.</summary>
    private const string AddressProperty = "System.Devices.Aep.DeviceAddress";

    /// <summary>[S-3252] The association endpoint's device container: where its battery is found.</summary>
    private const string AepContainerProperty = "System.Devices.Aep.ContainerId";

    /// <summary>[S-3252] A device node's container, to match it back to the paired device.</summary>
    private const string NodeContainerProperty = "System.Devices.ContainerId";

    /// <summary>
    /// [S-3252] <c>DEVPKEY_Bluetooth_Battery</c>: the level Windows itself shows in Settings, set on a
    /// device node of the container for hands-free (HFP) headsets and the LE devices Windows reads
    /// itself. Requested by GUID and property id: it has no canonical name.
    /// </summary>
    private const string BatteryProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    private static readonly string[] RequestedProperties = [ConnectedProperty, AepContainerProperty];

    private static readonly string[] SignalProperties = [ConnectedProperty, AepContainerProperty, SignalProperty, AddressProperty];

    private static readonly string[] NodeProperties = [BatteryProperty, NodeContainerProperty];

    /// <summary>
    /// [S-1256] How long the LE scan for signal readings listens. Bounded, and run only while the
    /// signal column is switched on: a scan keeps the radio busy, and an LE device that advertises
    /// at all does so several times a second, so a few seconds hear every one that will.
    /// </summary>
    private static readonly TimeSpan SignalScan = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long the battery pass may take in total.
    /// <para>
    /// A GATT read against a device that is connected but asleep sits until the stack gives up. One
    /// such device would otherwise hold the whole refresh, so the pass stops when the budget is
    /// gone and the devices it did not reach simply show no battery column — which is the same
    /// thing they show when they have no Battery Service, and is honest either way.
    /// </para>
    /// </summary>
    private static readonly TimeSpan BatteryBudget = TimeSpan.FromSeconds(8);

    /// <summary>Read the paired devices. Returns a named state rather than throwing.</summary>
    public static async Task<BluetoothSnapshot> ReadAsync(bool withBattery, bool withSignal, CancellationToken token)
    {
        var radio = await RadioStateAsync(token).ConfigureAwait(false);

        if (radio != BluetoothStatus.Ok)
        {
            return BluetoothSnapshot.Of(radio);
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

            if (withBattery)
            {
                found = await AddBatteryLevelsAsync(found, containers, token).ConfigureAwait(false);
            }

            if (withSignal && addresses.Count > 0)
            {
                await AddAdvertisedSignalAsync(found, addresses, token).ConfigureAwait(false);
            }

            return new BluetoothSnapshot(BluetoothStatus.Ok, found);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not enumerate paired devices", ex);
            return BluetoothSnapshot.Of(BluetoothStatus.Failed);
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
                        ? BluetoothRules.ParseAddress(written)
                        : null;

                    if ((address ?? BluetoothRules.AddressFromId(info.Id)) is { } known)
                    {
                        target.Addresses[target.Offset + devices.Count] = known;
                    }
                }
            }

            // The name is the device's own advertisement, so it is normalised by the display rule
            // and then only ever drawn.
            devices.Add(new BluetoothDeviceEntry(
                BluetoothRules.DisplayName(info.Name, Strings.Current),
                info.Id,
                connected,
                BluetoothRules.Classify(info.Name),
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

    /// <summary>
    /// [S-3252] Fill in the battery level for the connected devices: first the level Windows already
    /// holds (<see cref="BatteryProperty"/>, one local query, no radio traffic), then, within a
    /// budget, a GATT read for a connected LE device the property left without one.
    /// <para>
    /// Connected only. A disconnected device's GATT read does not fail — it waits for the radio to
    /// find the device, which takes the better part of ten seconds per device and would turn a
    /// refresh into a minute of blocked background work for a list nobody can act on anyway.
    /// </para>
    /// </summary>
    private static async Task<List<BluetoothDeviceEntry>> AddBatteryLevelsAsync(List<BluetoothDeviceEntry> found, List<Guid?> containers, CancellationToken token)
    {
        var wanted = new HashSet<Guid>();
        for (var i = 0; i < found.Count && i < containers.Count; i++)
        {
            if (found[i].Connected && containers[i] is { } container && container != Guid.Empty)
            {
                wanted.Add(container);
            }
        }

        var batteries = await BatteryByContainerAsync(wanted, token).ConfigureAwait(false);
        var devices = BluetoothRules.WithBattery(found, containers, batteries);

        var deadline = DateTimeOffset.UtcNow + BatteryBudget;

        for (var i = 0; i < devices.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            if (!devices[i].Connected || devices[i].BatteryPercent is not null)
            {
                continue;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                Log.Debug(Scope, "battery pass ran out of budget; the rest of the list shows no battery");
                return devices;
            }

            var percent = await BatteryPercentAsync(devices[i].Id, token).ConfigureAwait(false);

            if (BluetoothRules.ParseBattery(percent) is { } valid)
            {
                devices[i] = devices[i] with { BatteryPercent = valid };
            }
        }

        return devices;
    }

    /// <summary>
    /// [S-3252] The battery Windows holds for each of <paramref name="containers"/>, read from the
    /// device nodes of those containers in one query. A failed query is no batteries (logged), never
    /// a failed list: the devices are still there.
    /// </summary>
    private static async Task<Dictionary<Guid, int>> BatteryByContainerAsync(HashSet<Guid> containers, CancellationToken token)
    {
        if (containers.Count == 0)
        {
            return [];
        }

        // Ids come from Windows as Guids and are formatted here, so nothing outside reaches the query.
        var aqs = string.Join(" OR ", containers.Select(id => $"{NodeContainerProperty}:=\"{id:B}\""));

        try
        {
            var nodes = await DeviceInformation
                .FindAllAsync(aqs, NodeProperties, DeviceInformationKind.Device)
                .AsTask(token)
                .ConfigureAwait(false);

            return BluetoothRules.BatteryByContainer(nodes.Select(node => (
                node.Properties.TryGetValue(NodeContainerProperty, out var box) && box is Guid container ? container : (Guid?)null,
                node.Properties.TryGetValue(BatteryProperty, out var raw) ? raw : null)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read the battery property of the paired devices", ex);
            return [];
        }
    }

    /// <summary>
    /// The GATT Battery Service's level characteristic, if the device has one.
    /// <para>
    /// Both the device and the service hold OS handles and a live BLE connection, and this runs on
    /// a poll, so each is disposed on every path — an early return or a device that answers with a
    /// failure status leaks exactly as fast as one that succeeds.
    /// </para>
    /// </summary>
    private static async Task<int?> BatteryPercentAsync(string id, CancellationToken token)
    {
        BluetoothLEDevice? device = null;
        GattDeviceService? service = null;

        try
        {
            device = await BluetoothLEDevice.FromIdAsync(id).AsTask(token).ConfigureAwait(false);

            if (device is null)
            {
                // Not an LE device. A Classic device's battery (HFP) is the device property's, read
                // before this; there is no GATT to ask.
                return null;
            }

            var services = await device
                .GetGattServicesForUuidAsync(GattServiceUuids.Battery)
                .AsTask(token)
                .ConfigureAwait(false);

            if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
            {
                return null;
            }

            service = services.Services[0];

            var characteristics = await service
                .GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel)
                .AsTask(token)
                .ConfigureAwait(false);

            if (characteristics.Status != GattCommunicationStatus.Success
                || characteristics.Characteristics.Count == 0)
            {
                return null;
            }

            var read = await characteristics.Characteristics[0]
                .ReadValueAsync()
                .AsTask(token)
                .ConfigureAwait(false);

            if (read.Status != GattCommunicationStatus.Success || read.Value is null || read.Value.Length == 0)
            {
                return null;
            }

            using var reader = DataReader.FromBuffer(read.Value);
            return reader.ReadByte();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One device that will not answer must not cost the rest of the list its battery
            // column. Debug rather than warn: a sleeping peripheral is routine.
            Log.Debug(Scope, $"no battery from a paired device: {ex.GetType().Name}");
            return null;
        }
        finally
        {
            // Dispose is Close() through the projection: the BLE connection and the OS handle both
            // go back, on the success path and on every failure path.
            service?.Dispose();
            device?.Dispose();
        }
    }
}
