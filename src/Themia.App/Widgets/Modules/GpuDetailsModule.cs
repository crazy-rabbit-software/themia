using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("gpuDetails", typeof(Themia.App.Widgets.Modules.GpuDetailsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The GpuDetails widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class GpuDetailsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => GpuDetailsConfig.Descriptor;
}
