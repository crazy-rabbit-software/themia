using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("bluetooth", typeof(Themia.App.Widgets.Modules.BluetoothModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Bluetooth widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class BluetoothModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => BluetoothConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Bluetooth(rows.Rows, (BluetoothConfig)config, rows.Save);

    public FrameworkElement CreateView() => new BluetoothView();
}
