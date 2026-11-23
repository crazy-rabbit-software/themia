using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
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
public sealed class BluetoothConfig : WidgetConfig, IShownSettings<BluetoothSetting>, IItemList, IItemChoice, IStyled
{
    /// <summary>[S-11310] [S-16660] How many devices a new card stands (its natural height), 1 to 20; a new list three.</summary>
    public int Rows { get; set; } = ItemLayout.DefaultRows;

    /// <summary>[S-16940] How its devices are arranged: the item base's styles, the device list first.</summary>
    public ItemStyle Style { get; set; } = ItemStyle.List;

    [JsonIgnore]
    ItemStyle IItemList.Arrangement => Style;

    IReadOnlyList<StyleOption> IStyled.Styles => ItemStyles.All;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ItemStyle)value;
    }

    /// <summary>[S-16952] A table's column titles over its rows.</summary>
    public bool ColumnTitles { get; set; } = true;

    /// <summary>[S-16950] The parts' order: a table's columns.</summary>
    public List<ItemPart> PartOrder { get; set; } = [];

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

    /// <summary>[S-1257] Draw the connection word (the original's "connection" metric) in the item's line.</summary>
    public bool ShowConnection { get; set; } = true;

    /// <summary>
    /// [S-1256] Show signal strength (RSSI) where the radio reports it. Off by default: it needs a
    /// short Bluetooth LE scan on every refresh, and most paired Classic devices never report one.
    /// </summary>
    public bool ShowSignal { get; set; }

    /// <summary>List only what is connected, rather than everything ever paired.</summary>
    public bool ConnectedOnly { get; set; }

    /// <summary>[S-16673] The grid blocks between two devices (0..4), each then a card of its own.</summary>
    public int Gap { get; set; }

    /// <summary>
    /// [S-16672] The parts its items leave out. [S-3252] With both the percentage and the bar left out no battery is read:
    /// the GATT read opens a BLE connection per device, the slowest part of a refresh.
    /// </summary>
    public List<ItemPart> HiddenParts { get; set; } = [];

    /// <summary>[S-16661] The devices hidden by their endpoint ids: the checklist's off ones.</summary>
    public List<string> Hidden { get; set; } = [];

    /// <summary>[S-16661] The user's order by endpoint ids: these first, every other after them (connected first, by name).</summary>
    public List<string> Order { get; set; } = [];

    /// <summary>[S-16660] What an item says besides its name.</summary>
    [JsonIgnore]
    public Themia.Widget.Bluetooth.BluetoothShows Shows => new(Themia.Widget.Bluetooth.BluetoothItems.ReadsBattery(HiddenParts), DrawsConnection, ShowSignal);

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
        ShowConnection = ShowConnection,
        ShowSignal = ShowSignal,
        ConnectedOnly = ConnectedOnly,
        Gap = Gap,
        HiddenParts = [.. HiddenParts],
        PartOrder = [.. PartOrder],
        Hidden = [.. Hidden],
        Order = [.. Order],
        Rows = Rows,
        Style = Style,
        ColumnTitles = ColumnTitles,
    };

    /// <summary>[S-11310] Rows out of range are held to them; ids empty or twice dropped.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = ItemOrder.Repair(this, "bluetooth");
        Hidden = ItemOrder.Repaired(Hidden, "bluetooth", "hidden", notes);
        Order = ItemOrder.Repaired(Order, "bluetooth", "order", notes);
        return notes;
    }
}

/// <summary>[S-16662] One paired Bluetooth device's widget: which device and what its face says besides its battery.</summary>
public sealed class BluetoothDeviceConfig : WidgetConfig, IItemParts
{
    /// <summary>[S-16662] The device's endpoint id; empty for the first connected one (else the first paired).</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>[S-16672] The parts its face leaves out (its percentage and bar both: no battery is read).</summary>
    public List<ItemPart> HiddenParts { get; set; } = [];

    /// <summary>[S-16950] The parts' order (a face has no columns).</summary>
    public List<ItemPart> PartOrder { get; set; } = [];

    /// <summary>[S-1257] Its connection word in the line.</summary>
    public bool ShowConnection { get; set; } = true;

    /// <summary>[S-1256] Its signal in the line (a short LE scan a refresh).</summary>
    public bool ShowSignal { get; set; }

    /// <summary>[S-16662] What its face says besides its name.</summary>
    [JsonIgnore]
    public Themia.Widget.Bluetooth.BluetoothShows Shows => new(Themia.Widget.Bluetooth.BluetoothItems.ReadsBattery(HiddenParts), ShowConnection, ShowSignal);

    public override WidgetConfig Clone() => new BluetoothDeviceConfig
    {
        Device = Device,
        HiddenParts = [.. HiddenParts],
        PartOrder = [.. PartOrder],
        ShowConnection = ShowConnection,
        ShowSignal = ShowSignal,
    };
}
