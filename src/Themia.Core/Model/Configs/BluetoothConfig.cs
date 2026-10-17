using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// Paired Bluetooth devices, their connection state, and their battery where they report one.
/// </summary>
public sealed class BluetoothConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Bluetooth;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("bluetooth"),
        Kind = WidgetKind.Bluetooth,
        ConfigType = typeof(BluetoothConfig),
        CreateDefault = () => new BluetoothConfig(),
        Json = WidgetConfigJsonContext.Default,
        LabelId = Str.Widget_Bluetooth_Label,
        DescriptionId = Str.WidgetsA_Catalog_Bluetooth_Description,
        Glyph = Glyphs.Bluetooth,
        DefaultWidth = 300,
        DefaultHeight = 240,
    };

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
    /// [S-1254] [S-3794] The most devices the list holds: a ceiling, not a target, and not a
    /// setting. The tile shows as many whole rows as it has room for, and scrolls to the rest.
    /// </summary>
    public const int MaxDevices = 30;

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
    };
}
