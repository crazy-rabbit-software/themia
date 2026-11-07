using Themia.Core.Diagnostics;
using Themia.Sdk.Devices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Gaming.Input;
using Windows.Storage.Streams;
using PowerStatus = Windows.System.Power.BatteryStatus;

namespace Themia.App.Services.Devices;

/// <summary>
/// [S-4470] [S-4472] One read of every peripheral battery Windows reports, path by path, unfolded: the
/// widget's rules fold and order them. Slow in the way the UI thread cannot afford (enumeration is an
/// RPC, a GATT read waits on the device), so it runs on the pool with a cancellation token.
/// <para>
/// The paths: the paired Bluetooth devices that are connected, with the level Windows holds for them
/// (<c>DEVPKEY_Bluetooth_Battery</c> on a node of their container) or, within a budget, their GATT
/// Battery Service; every device container that carries <c>System.Devices.BatteryLife</c> (Windows'
/// devices list, any bus); every game controller with a battery report. A USB receiver's mouse or
/// keyboard is listed only where its driver sets the container's level: Windows has no other battery
/// for HID devices (Logitech's Unifying and Bolt receivers speak HID++, which Windows does not read).
/// </para>
/// </summary>
internal static class DeviceBatteryInventory
{
    private const string Scope = "device-batteries";

    internal const string ConnectedProperty = "System.Devices.Aep.IsConnected";
    internal const string AepContainerProperty = "System.Devices.Aep.ContainerId";
    internal const string NodeContainerProperty = "System.Devices.ContainerId";
    internal const string BatteryLifeProperty = "System.Devices.BatteryLife";
    internal const string ChargingProperty = "System.Devices.ChargingState";
    internal const string CategoryProperty = "System.Devices.Category";
    internal const string ContainerConnectedProperty = "System.Devices.Connected";

    internal static readonly string[] AepProperties = [ConnectedProperty, AepContainerProperty];

    internal static readonly string[] NodeProperties = [DeviceBatteryRules.BluetoothBatteryProperty, NodeContainerProperty];

    internal static readonly string[] ContainerProperties = [BatteryLifeProperty, ChargingProperty, CategoryProperty, ContainerConnectedProperty];

    /// <summary>[S-4472] The device containers Windows holds a battery level for: what the container watcher follows.</summary>
    internal const string BatteryContainers = "System.Devices.BatteryLife:<>[]";

    /// <summary>
    /// [S-4472] How long the GATT reads of one pass may take together: one connected but sleeping LE
    /// device sits until the stack gives up, and must not hold the rest of the list.
    /// </summary>
    private static readonly TimeSpan GattBudget = TimeSpan.FromSeconds(8);

    /// <summary>What one pass found: the readings, and the connected Bluetooth containers whose nodes the service watches.</summary>
    public sealed record Pass(IReadOnlyList<DeviceBatteryReading> Readings, IReadOnlySet<Guid> BluetoothContainers, IReadOnlyDictionary<string, int> Gatt);

    /// <summary>An AQS over the device nodes of <paramref name="containers"/>. Ids come from Windows as Guids, so nothing outside reaches the query.</summary>
    internal static string NodesOf(IEnumerable<Guid> containers)
        => string.Join(" OR ", containers.Select(id => $"{NodeContainerProperty}:=\"{id:B}\""));

    /// <param name="gattKnown">The GATT levels of the last pass, by device id: kept for a device still connected unless <paramref name="full"/>.</param>
    /// <param name="full">Read the GATT levels and controllers again (the cadence or Refresh), not only what Windows holds.</param>
    public static async Task<Pass> ReadAsync(IReadOnlyDictionary<string, int> gattKnown, bool full, CancellationToken token)
    {
        var paired = new List<Paired>();
        paired.AddRange(await PairedAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true), le: false, token).ConfigureAwait(false));
        paired.AddRange(await PairedAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), le: true, token).ConfigureAwait(false));

        var connected = paired.Where(p => p.Connected).ToList();
        var btContainers = connected.Select(p => p.Container).OfType<Guid>().ToHashSet();

        var containers = await ContainersAsync(token).ConfigureAwait(false);
        var nodeLevels = await NodeLevelsAsync(btContainers, token).ConfigureAwait(false);

        var readings = new List<DeviceBatteryReading>();
        var gatt = new Dictionary<string, int>(StringComparer.Ordinal);
        var deadline = DateTimeOffset.UtcNow + GattBudget;

        foreach (var device in connected)
        {
            var kind = Kind(device.Container, containers, device.Name);

            if (device.Container is { } id && nodeLevels.TryGetValue(id, out var level))
            {
                readings.Add(new DeviceBatteryReading(device.Id, device.Name, kind, level, DeviceBatterySource.BluetoothProperty, id));
                continue;
            }

            if (!device.Le)
            {
                continue;
            }

            int? percent = !full && gattKnown.TryGetValue(device.Id, out var known) ? known : null;
            if (percent is null && DateTimeOffset.UtcNow < deadline)
            {
                percent = DeviceBatteryRules.ParsePercent(await GattLevelAsync(device.Id, token).ConfigureAwait(false));
            }

            if (percent is { } read)
            {
                gatt[device.Id] = read;
                readings.Add(new DeviceBatteryReading(device.Id, device.Name, kind, read, DeviceBatterySource.GattBatteryService, device.Container));
            }
        }

        foreach (var container in containers.Values)
        {
            // A container Windows says is disconnected keeps the last level it saw: not a reading.
            if (container.Percent is { } percent && container.Connected != false)
            {
                readings.Add(new DeviceBatteryReading(
                    $"container:{container.Id:D}", container.Name,
                    DeviceKinds.FromCategories(container.Categories) ?? DeviceKinds.FromName(container.Name),
                    percent, DeviceBatterySource.DeviceContainer, container.Id, container.Charging));
            }
        }

        readings.AddRange(Controllers());
        return new Pass(readings, btContainers, gatt);
    }

    private sealed record Paired(string Id, string Name, bool Connected, Guid? Container, bool Le);

    private sealed record Container(Guid Id, string Name, int? Percent, bool? Charging, bool? Connected, string?[] Categories);

    private static DeviceKind Kind(Guid? container, IReadOnlyDictionary<Guid, Container> containers, string name)
        => (container is { } id && containers.TryGetValue(id, out var found) ? DeviceKinds.FromCategories(found.Categories) : null)
            ?? DeviceKinds.FromName(name);

    /// <summary>The paired devices of one Bluetooth selector. A failed query is none (logged): the other paths still answer.</summary>
    private static async Task<List<Paired>> PairedAsync(string selector, bool le, CancellationToken token)
    {
        try
        {
            var found = await DeviceInformation.FindAllAsync(selector, AepProperties).AsTask(token).ConfigureAwait(false);
            return
            [
                .. found.Select(info => new Paired(
                    info.Id,
                    info.Name ?? string.Empty,
                    info.Properties.TryGetValue(ConnectedProperty, out var on) && on is true,
                    info.Properties.TryGetValue(AepContainerProperty, out var box) ? DeviceBatteryRules.ParseContainer(box) : null,
                    le)),
            ];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"could not enumerate the paired {(le ? "LE" : "Classic")} devices", ex);
            return [];
        }
    }

    /// <summary>
    /// Every device container, with its battery, charging state and categories: the containers that
    /// carry a level are readings, and every container names the kind of the Bluetooth device in it.
    /// One local query (no radio traffic).
    /// </summary>
    private static async Task<Dictionary<Guid, Container>> ContainersAsync(CancellationToken token)
    {
        var map = new Dictionary<Guid, Container>();

        try
        {
            var found = await DeviceInformation
                .FindAllAsync(string.Empty, ContainerProperties, DeviceInformationKind.DeviceContainer)
                .AsTask(token)
                .ConfigureAwait(false);

            foreach (var info in found)
            {
                if (DeviceBatteryRules.ParseContainer(info.Id) is not { } id)
                {
                    continue;
                }

                var props = info.Properties;
                map[id] = new Container(
                    id,
                    info.Name ?? string.Empty,
                    props.TryGetValue(BatteryLifeProperty, out var life) ? DeviceBatteryRules.ParsePercent(life) : null,
                    props.TryGetValue(ChargingProperty, out var charging) ? DeviceBatteryRules.ParseCharging(charging) : null,
                    props.TryGetValue(ContainerConnectedProperty, out var on) && on is bool flag ? flag : null,
                    props.TryGetValue(CategoryProperty, out var categories) && categories is string[] list ? list : []);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read the device containers", ex);
        }

        return map;
    }

    /// <summary>[S-3252] The Bluetooth battery Windows holds for each container, from its device nodes, in one query.</summary>
    private static async Task<Dictionary<Guid, int>> NodeLevelsAsync(HashSet<Guid> containers, CancellationToken token)
    {
        if (containers.Count == 0)
        {
            return [];
        }

        try
        {
            var nodes = await DeviceInformation
                .FindAllAsync(NodesOf(containers), NodeProperties, DeviceInformationKind.Device)
                .AsTask(token)
                .ConfigureAwait(false);

            return DeviceBatteryRules.BatteryByContainer(nodes.Select(node => (
                node.Properties.TryGetValue(NodeContainerProperty, out var box) ? DeviceBatteryRules.ParseContainer(box) : null,
                node.Properties.TryGetValue(DeviceBatteryRules.BluetoothBatteryProperty, out var raw) ? raw : null)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read the Bluetooth battery property", ex);
            return [];
        }
    }

    /// <summary>
    /// The GATT Battery Service's level, or null. The device and the service hold OS handles and a
    /// live BLE connection, so both are disposed on every path.
    /// </summary>
    private static async Task<int?> GattLevelAsync(string id, CancellationToken token)
    {
        BluetoothLEDevice? device = null;
        GattDeviceService? service = null;

        try
        {
            device = await BluetoothLEDevice.FromIdAsync(id).AsTask(token).ConfigureAwait(false);
            if (device is null)
            {
                return null;
            }

            var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery).AsTask(token).ConfigureAwait(false);
            if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
            {
                return null;
            }

            service = services.Services[0];

            var characteristics = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel).AsTask(token).ConfigureAwait(false);
            if (characteristics.Status != GattCommunicationStatus.Success || characteristics.Characteristics.Count == 0)
            {
                return null;
            }

            var read = await characteristics.Characteristics[0].ReadValueAsync().AsTask(token).ConfigureAwait(false);
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
            // A sleeping peripheral is routine: no reading, and the rest of the list goes on.
            Log.Debug(Scope, $"no GATT battery from a connected LE device: {ex.GetType().Name}");
            return null;
        }
        finally
        {
            service?.Dispose();
            device?.Dispose();
        }
    }

    /// <summary>
    /// Every game controller with a battery report (an Xbox controller, wired, on the wireless adapter
    /// or on Bluetooth). A controller with no battery (wired-only, most HID pads) reports none or
    /// "not present", and is not a reading.
    /// </summary>
    private static List<DeviceBatteryReading> Controllers()
    {
        var readings = new List<DeviceBatteryReading>();

        try
        {
            foreach (var controller in RawGameController.RawGameControllers)
            {
                var report = controller.TryGetBatteryReport();
                if (report is null || report.Status == PowerStatus.NotPresent)
                {
                    continue;
                }

                if (DeviceBatteryRules.Percent(report.RemainingCapacityInMilliwattHours, report.FullChargeCapacityInMilliwattHours) is not { } percent)
                {
                    continue;
                }

                readings.Add(new DeviceBatteryReading(
                    $"controller:{controller.NonRoamableId}",
                    controller.DisplayName ?? string.Empty,
                    DeviceKind.Gamepad,
                    percent,
                    DeviceBatterySource.GameController,
                    Charging: report.Status == PowerStatus.Charging));
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read the game controllers' batteries", ex);
        }

        return readings;
    }
}
