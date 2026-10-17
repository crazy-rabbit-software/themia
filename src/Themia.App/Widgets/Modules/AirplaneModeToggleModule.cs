using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("airplaneModeToggle", typeof(Themia.App.Widgets.Modules.AirplaneModeToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The AirplaneModeToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class AirplaneModeToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => AirplaneModeToggleConfig.Descriptor;

    public FrameworkElement CreateView() => new QuickToggleView(AirplaneModeToggleConfig.Descriptor.Id);
}
