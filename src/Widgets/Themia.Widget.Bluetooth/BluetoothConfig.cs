using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>[S-3256] The Bluetooth tile's options that only some states use ([S-6070]).</summary>
public enum BluetoothSetting
{
    /// <summary>[S-1257] The connection word, which tells nothing while only connected devices are listed.</summary>
    Connection,
}

/// <summary>
/// Paired Bluetooth devices, their connection state, and their battery where they report one.
/// </summary>
public sealed class BluetoothConfig : WidgetConfig, IShownSettings<BluetoothSetting>, IKitRows
{
    /// <summary>[S-11310] A new list's rows: four.</summary>
    public const int DefaultRows = 4;

    /// <summary>[S-11310] How many rows it shows (1 to 20); more devices scroll inside them.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-11310] One line a device (its name, its battery) or two (its kind, connection and signal under it); a new one one.</summary>
    public int Lines { get; set; } = 1;

    /// <summary>
    /// [S-3252] Show each connected device's battery: the level Windows holds for it, else a GATT read.
    /// <para>
    /// Off-by-choice is a real option: the GATT read opens a BLE connection per device and is by far the
    /// slowest part of a refresh, so a desktop that only wants to know what is connected should not
    /// have to pay for it.
    /// </para>
    /// </summary>
    public bool ShowBattery { get; set; } = true;

    /// <summary>[S-1257] Draw each device's kind icon (the original's "type" metric). The name is always shown.</summary>
    public bool ShowIcon { get; set; } = true;

    /// <summary>[S-1257] Draw the connection dot and word (the original's "connection" metric).</summary>
    public bool ShowConnection { get; set; } = true;

    /// <summary>
    /// [S-1256] Show signal strength (RSSI) where the radio reports it. Off by default: it needs a
    /// short Bluetooth LE scan on every refresh, and most paired Classic devices never report one.
    /// </summary>
    public bool ShowSignal { get; set; }

    /// <summary>List only what is connected, rather than everything ever paired.</summary>
    public bool ConnectedOnly { get; set; }

    /// <summary>
    /// [S-3256] Whether a row says its connection: never while only connected devices are listed,
    /// where every row would say "Connected" (the icon's colour still does, [S-3961]).
    /// </summary>
    [JsonIgnore]
    public bool DrawsConnection => ShowConnection && !ConnectedOnly;

    /// <summary>[S-3256] The Connection switch only where the list can hold a device that is not connected.</summary>
    [JsonIgnore]
    public IReadOnlyList<BluetoothSetting> ShownSettings => ConnectedOnly ? [] : [BluetoothSetting.Connection];


    /// <summary>
    /// [S-3794] How often the radios are enumerated: every 30 seconds, not a setting. Enumerating
    /// and reading GATT batteries is seconds of work, and a headset does not connect twice a minute.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromSeconds(30);

    public override WidgetConfig Clone() => new BluetoothConfig
    {
        ShowBattery = ShowBattery,
        ShowIcon = ShowIcon,
        ShowConnection = ShowConnection,
        ShowSignal = ShowSignal,
        ConnectedOnly = ConnectedOnly,
        Rows = Rows,
        Lines = Lines,
    };

    /// <summary>[S-11310] Rows and lines out of range are held to them.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (Rows == KitSizing.ClampRows(Rows) && Lines == KitSizing.ClampLines(Lines))
        {
            return [];
        }

        var said = $"bluetooth rows {Rows} or lines {Lines} out of range";
        Rows = KitSizing.ClampRows(Rows);
        Lines = KitSizing.ClampLines(Lines);
        return [said];
    }
}
