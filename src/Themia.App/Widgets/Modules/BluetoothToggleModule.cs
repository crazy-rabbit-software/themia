using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("bluetoothToggle", typeof(Themia.App.Widgets.Modules.BluetoothToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The BluetoothToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class BluetoothToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => BluetoothToggleConfig.Descriptor;

    public FrameworkElement CreateView() => new QuickToggleView(BluetoothToggleConfig.Descriptor.Id);
}
