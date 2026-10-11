using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>
/// Settings rows for the Bluetooth tile. Each control writes straight to the live config and saves,
/// so the widget behind the window follows along.
/// </summary>
internal static partial class WidgetSettings
{
    public static void Bluetooth(List<FrameworkElement> rows, BluetoothConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsA_Bluetooth_Settings_Devices.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Link, Str.WidgetsA_Bluetooth_Settings_ConnectedOnly_Label.Text(),
            Str.WidgetsA_Bluetooth_Settings_ConnectedOnly_Desc.Text(),
            FluentLayout.Toggle(config.ConnectedOnly, value => { config.ConnectedOnly = value; save(); })));

        // [S-1257] The original's per-device metrics. The name is always shown.
        rows.Add(FluentLayout.Header(Str.WidgetsA_Bluetooth_Settings_Show.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Bluetooth, Str.WidgetsA_Bluetooth_Settings_Icon_Label.Text(),
            null,
            FluentLayout.Toggle(config.ShowIcon, value => { config.ShowIcon = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Link, Str.WidgetsA_Bluetooth_Settings_Connection_Label.Text(),
            Str.WidgetsA_Bluetooth_Settings_Connection_Desc.Text(),
            FluentLayout.Toggle(config.ShowConnection, value => { config.ShowConnection = value; save(); })));

        // Said out loud, because it is the setting that decides how long a refresh takes: the GATT
        // read opens a connection to each device and is by far the slowest part of the pass.
        rows.Add(FluentLayout.Row(Glyphs.Battery, Str.Widget_Battery_Label.Text(),
            Str.WidgetsA_Bluetooth_Settings_Battery_Desc.Text(),
            FluentLayout.Toggle(config.ShowBattery, value => { config.ShowBattery = value; save(); })));

        // [S-1256] Also said out loud: it costs a short radio scan on every refresh.
        rows.Add(FluentLayout.Row(Glyphs.Signal, Str.WidgetsA_Bluetooth_Settings_Signal_Label.Text(),
            Str.WidgetsA_Bluetooth_Settings_Signal_Desc.Text(),
            FluentLayout.Toggle(config.ShowSignal, value => { config.ShowSignal = value; save(); })));
    }
}
