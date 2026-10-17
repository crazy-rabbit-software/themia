using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("wifiToggle", typeof(Themia.App.Widgets.Modules.WifiToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The WifiToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class WifiToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => WifiToggleConfig.Descriptor;

    public FrameworkElement CreateView() => new QuickToggleView(WifiToggleConfig.Descriptor.Id);
}
