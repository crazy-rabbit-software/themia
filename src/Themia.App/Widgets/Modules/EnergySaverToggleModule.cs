using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("energySaverToggle", typeof(Themia.App.Widgets.Modules.EnergySaverToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The EnergySaverToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class EnergySaverToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => EnergySaverToggleConfig.Descriptor;

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(EnergySaverToggleConfig.Descriptor.Id);
}
