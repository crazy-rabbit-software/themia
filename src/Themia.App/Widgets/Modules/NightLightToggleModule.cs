using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("nightLightToggle", typeof(Themia.App.Widgets.Modules.NightLightToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The NightLightToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class NightLightToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => NightLightToggleConfig.Descriptor;

    public FrameworkElement CreateView() => new QuickToggleView(NightLightToggleConfig.Descriptor.Id);
}
