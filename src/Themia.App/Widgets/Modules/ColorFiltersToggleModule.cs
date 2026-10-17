using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("colorFiltersToggle", typeof(Themia.App.Widgets.Modules.ColorFiltersToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The ColorFiltersToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ColorFiltersToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ColorFiltersToggleConfig.Descriptor;

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(ColorFiltersToggleConfig.Descriptor.Id);
}
