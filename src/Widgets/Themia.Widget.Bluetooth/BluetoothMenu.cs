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

        // Which devices it lists, then [S-16661] the device list's lines: the checklist in its order, what an item shows
        // (with the connection word and the signal, [S-1256]: a short radio scan), the gap, the rows.
        menu.Lines.Add(menu.Toggle(Glyphs.Link, BluetoothStr.WidgetsA_Bluetooth_Settings_ConnectedOnly_Label.Text(), config.ConnectedOnly, on => config.ConnectedOnly = on));
        menu.Lines.Add(SettingsLine.Separator);
        var locale = Strings.Current;
        var paired = BluetoothList.Now() is { Status: BluetoothStatus.Ok } pass ? BluetoothRules.Arrange(pass.Devices, config.ConnectedOnly, locale) : [];
        DeviceMenuLines.List(menu, config, [.. paired.Select(d => new DeviceChoice(d.Id, d.Name, DeviceItems.Glyph((DeviceKind)d.Kind)))], Own(menu, config.Offers(BluetoothSetting.Connection), config.ShowConnection, on => config.ShowConnection = on, config.ShowSignal, on => config.ShowSignal = on));
    }

    /// <summary>[S-16672] A Bluetooth item's own parts in the parts' section: its connection word (where it can say one), its signal.</summary>
    private static List<SettingsLine> Own(WidgetMenuLines menu, bool connection, bool showConnection, Action<bool> setConnection, bool showSignal, Action<bool> setSignal)
    {
        var lines = new List<SettingsLine>();
        if (connection)
        {
            lines.Add(menu.Toggle(null, BluetoothStr.WidgetsA_Bluetooth_Settings_Connection_Label.Text(), showConnection, setConnection));
        }

        lines.Add(menu.Toggle(null, BluetoothStr.WidgetsA_Bluetooth_Settings_Signal_Label.Text(), showSignal, setSignal));
        return lines;
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
        lines.Add(DeviceMenuLines.Parts(menu, config, Own(menu, true, config.ShowConnection, on => config.ShowConnection = on, config.ShowSignal, on => config.ShowSignal = on)));
    }
}
