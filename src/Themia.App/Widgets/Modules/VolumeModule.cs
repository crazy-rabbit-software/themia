using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("volume", typeof(Themia.App.Widgets.Modules.VolumeModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Volume widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class VolumeModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => VolumeConfig.Descriptor;
}
