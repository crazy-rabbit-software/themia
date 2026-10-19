using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Bluetooth;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3255] The Bluetooth tile's options, all on its properties menu ([S-3719]): Appearance, which
/// devices it lists, then what a device's row shows ([S-1257]: the name always; the connection
/// only where a listed device can be not connected, [S-3256]). It has no settings page.
/// </summary>
internal static class BluetoothMenu
{
    public static void Lines(WidgetMenuLines menu, BluetoothConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        var lines = menu.Lines;
        lines.Add(menu.Toggle(Glyphs.Link, BluetoothStr.WidgetsA_Bluetooth_Settings_ConnectedOnly_Label.Text(), config.ConnectedOnly, on => config.ConnectedOnly = on));
        lines.Add(ItemMenuEntry.Separator);
        lines.Add(menu.Toggle(Glyphs.Bluetooth, BluetoothStr.WidgetsA_Bluetooth_Settings_Icon_Label.Text(), config.ShowIcon, on => config.ShowIcon = on));
        if (config.Offers(BluetoothSetting.Connection))
        {
            lines.Add(menu.Toggle(Glyphs.Link, BluetoothStr.WidgetsA_Bluetooth_Settings_Connection_Label.Text(), config.ShowConnection, on => config.ShowConnection = on));
        }

        // The battery read opens a connection to each device (the slowest part of a refresh), and
        // [S-1256] the signal costs a short radio scan: both off the row unless asked for.
        lines.Add(menu.Toggle(Glyphs.Battery, BluetoothStr.Widget_Battery_Label.Text(), config.ShowBattery, on => config.ShowBattery = on));
        lines.Add(menu.Toggle(Glyphs.Signal, BluetoothStr.WidgetsA_Bluetooth_Settings_Signal_Label.Text(), config.ShowSignal, on => config.ShowSignal = on));
    }
}
