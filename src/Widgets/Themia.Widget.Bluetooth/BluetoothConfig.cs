using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>[S-3256] The Bluetooth tile's options that only some states use ([S-6070]).</summary>
public enum BluetoothSetting
{
    /// <summary>[S-1257] The connection word, which tells nothing while only connected devices are listed.</summary>
    Connection,
}

/// <summary>
/// Paired Bluetooth devices, their connection state, and their battery where they report one, as device items ([S-16660]).
/// </summary>
public sealed class BluetoothConfig : WidgetConfig, IShownSettings<BluetoothSetting>, IKitRows, IDeviceListConfig
{
    /// <summary>[S-11310] [S-16660] How many devices a new card stands (its natural height), 1 to 20; a new list three.</summary>
    public int Rows { get; set; } = DeviceLayout.DefaultRows;

    /// <summary>[S-16660] A device item has its one face: no lines to choose.</summary>
    [JsonIgnore]
    bool IKitRows.OffersLines => false;

    [JsonIgnore]
    public int Lines
    {
        get => 1;
        set
        {
            // ignore: a device item has its one face; the menu offers no lines.
        }
    }

    /// <summary>
    /// [S-3252] Show each connected device's battery: the level Windows holds for it, else a GATT read.
    /// <para>
    /// Off-by-choice is a real option: the GATT read opens a BLE connection per device and is by far the
    /// slowest part of a refresh, so a desktop that only wants to know what is connected should not
    /// have to pay for it.
    /// </para>
    /// </summary>
    public bool ShowBattery { get; set; } = true;

    /// <summary>[S-1257] Draw the connection word (the original's "connection" metric) in the item's line.</summary>
    public bool ShowConnection { get; set; } = true;

    /// <summary>
    /// [S-1256] Show signal strength (RSSI) where the radio reports it. Off by default: it needs a
    /// short Bluetooth LE scan on every refresh, and most paired Classic devices never report one.
    /// </summary>
    public bool ShowSignal { get; set; }

    /// <summary>List only what is connected, rather than everything ever paired.</summary>
    public bool ConnectedOnly { get; set; }

    /// <summary>[S-16661] One grid cell between two devices, so each stands as a card of its own.</summary>
    public bool Gap { get; set; }

    /// <summary>[S-16661] The devices hidden by their endpoint ids: the checklist's off ones.</summary>
    public List<string> Hidden { get; set; } = [];

    /// <summary>[S-16661] The user's order by endpoint ids: these first, every other after them (connected first, by name).</summary>
    public List<string> Order { get; set; } = [];

    /// <summary>[S-16660] What an item says besides its name.</summary>
    [JsonIgnore]
    public Themia.Widget.Bluetooth.BluetoothShows Shows => new(ShowBattery, DrawsConnection, ShowSignal);

    /// <summary>
    /// [S-3256] Whether an item says its connection: never while only connected devices are listed,
    /// where every item would say "Connected" (the icon's colour still does, [S-3961]).
    /// </summary>
    [JsonIgnore]
    public bool DrawsConnection => ShowConnection && !ConnectedOnly;

    /// <summary>[S-3256] The Connection switch only where the list can hold a device that is not connected.</summary>
    [JsonIgnore]
    public IReadOnlyList<BluetoothSetting> ShownSettings => ConnectedOnly ? [] : [BluetoothSetting.Connection];

    /// <summary>
    /// [S-3794] How often the list is redrawn: every 30 seconds, not a setting. The radios are the host's to read, on its
    /// own cadence ([S-4474]), and a headset does not connect twice a minute.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromSeconds(30);

    public override WidgetConfig Clone() => new BluetoothConfig
    {
        ShowBattery = ShowBattery,
        ShowConnection = ShowConnection,
        ShowSignal = ShowSignal,
        ConnectedOnly = ConnectedOnly,
        Gap = Gap,
        Hidden = [.. Hidden],
        Order = [.. Order],
        Rows = Rows,
    };

    /// <summary>[S-11310] Rows out of range are held to them; ids empty or twice dropped.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();
        Hidden = DeviceOrder.Repaired(Hidden, "bluetooth", "hidden", notes);
        Order = DeviceOrder.Repaired(Order, "bluetooth", "order", notes);
        if (Rows != KitSizing.ClampRows(Rows))
        {
            notes.Add($"bluetooth rows {Rows} out of range");
            Rows = KitSizing.ClampRows(Rows);
        }

        return notes;
    }
}

/// <summary>[S-16662] One paired Bluetooth device's widget: which device and what its face says besides its battery.</summary>
public sealed class BluetoothDeviceConfig : WidgetConfig
{
    /// <summary>[S-16662] The device's endpoint id; empty for the first connected one (else the first paired).</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>[S-3252] Its battery as the reading (off: the bar stays empty, no GATT read is asked for it).</summary>
    public bool ShowBattery { get; set; } = true;

    /// <summary>[S-1257] Its connection word in the line.</summary>
    public bool ShowConnection { get; set; } = true;

    /// <summary>[S-1256] Its signal in the line (a short LE scan a refresh).</summary>
    public bool ShowSignal { get; set; }

    /// <summary>[S-16662] What its face says besides its name.</summary>
    [JsonIgnore]
    public Themia.Widget.Bluetooth.BluetoothShows Shows => new(ShowBattery, ShowConnection, ShowSignal);

    public override WidgetConfig Clone() => new BluetoothDeviceConfig
    {
        Device = Device,
        ShowBattery = ShowBattery,
        ShowConnection = ShowConnection,
        ShowSignal = ShowSignal,
    };
}
