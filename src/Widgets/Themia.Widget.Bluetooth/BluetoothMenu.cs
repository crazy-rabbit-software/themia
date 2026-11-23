using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Bluetooth;
using Themia.Sdk.Devices;
using Themia.Sdk.WinUI.Devices;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3255] The Bluetooth tile's options, all on its page ([S-3719]): Appearance, which
/// devices it lists, then what a device's item shows ([S-1257]: the icon and the name always; the connection
/// only where a listed device can be not connected, [S-3256]), the gap between them, the rows. It has no settings page.
/// </summary>
internal static class BluetoothMenu
{
    public static void Lines(WidgetMenuLines menu, BluetoothConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        var lines = menu.Lines;
        lines.Add(menu.Toggle(Glyphs.Link, BluetoothStr.WidgetsA_Bluetooth_Settings_ConnectedOnly_Label.Text(), config.ConnectedOnly, on => config.ConnectedOnly = on));
        lines.Add(SettingsLine.Separator);
        if (config.Offers(BluetoothSetting.Connection))
        {
            lines.Add(menu.Toggle(Glyphs.Info, BluetoothStr.WidgetsA_Bluetooth_Settings_Connection_Label.Text(), config.ShowConnection, on => config.ShowConnection = on));
        }

        // The battery read opens a connection to each device (the slowest part of a refresh), and
        // [S-1256] the signal costs a short radio scan: both off the row unless asked for.
        lines.Add(menu.Toggle(Glyphs.Battery, BluetoothStr.Widget_Battery_Label.Text(), config.ShowBattery, on => config.ShowBattery = on));
        lines.Add(menu.Toggle(Glyphs.Signal, BluetoothStr.WidgetsA_Bluetooth_Settings_Signal_Label.Text(), config.ShowSignal, on => config.ShowSignal = on));

        // [S-16661] The device list's lines: which devices show, their order, the gap, how many a new card stands.
        lines.Add(SettingsLine.Separator);
        var locale = Strings.Current;
        var paired = BluetoothList.Now() is { Status: BluetoothStatus.Ok } pass ? BluetoothRules.Arrange(pass.Devices, config.ConnectedOnly, locale) : [];
        DeviceMenuLines.List(menu, config, [.. paired.Select(d => new DeviceChoice(d.Id, d.Name, DeviceItems.Glyph((DeviceKind)d.Kind)))]);
    }

    /// <summary>
    /// [S-16662] The Bluetooth device's options, all on its page: which device (the first connected, or one by name; a chosen
    /// one paired no more stays offered), then its battery, its connection word and its signal.
    /// </summary>
    public static void Lines(WidgetMenuLines menu, BluetoothDeviceConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        var locale = Strings.Current;
        var options = new List<(string Value, string Text)> { (string.Empty, BluetoothStr.Bluetooth_Device_First.Text()) };
        if (BluetoothList.Now() is { Status: BluetoothStatus.Ok } pass)
        {
            options.AddRange(BluetoothRules.Arrange(pass.Devices, connectedOnly: false, locale).Select(d => (d.Id, d.Name)));
        }

        if (!options.Any(o => string.Equals(o.Value, config.Device, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add((config.Device, Str.Details_Disk_NotConnected.Text()));
        }

        var held = options.First(o => string.Equals(o.Value, config.Device, StringComparison.OrdinalIgnoreCase)).Value;
        var lines = menu.Lines;
        lines.Add(menu.Choice(Glyphs.Bluetooth, BluetoothStr.Bluetooth_Device_Which.Text(), options, held, value => config.Device = value));
        lines.Add(SettingsLine.Separator);
        lines.Add(menu.Toggle(Glyphs.Battery, BluetoothStr.Widget_Battery_Label.Text(), config.ShowBattery, on => config.ShowBattery = on));
        lines.Add(menu.Toggle(Glyphs.Info, BluetoothStr.WidgetsA_Bluetooth_Settings_Connection_Label.Text(), config.ShowConnection, on => config.ShowConnection = on));
        lines.Add(menu.Toggle(Glyphs.Signal, BluetoothStr.WidgetsA_Bluetooth_Settings_Signal_Label.Text(), config.ShowSignal, on => config.ShowSignal = on));
    }
}
