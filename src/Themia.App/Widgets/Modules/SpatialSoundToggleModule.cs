using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("spatialSoundToggle", typeof(Themia.App.Widgets.Modules.SpatialSoundToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The SpatialSoundToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class SpatialSoundToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => SpatialSoundToggleConfig.Descriptor;

    public FrameworkElement CreateView() => new QuickToggleView(SpatialSoundToggleConfig.Descriptor.Id);
}
