using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("disk", typeof(Themia.App.Widgets.Modules.DiskModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Disk widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class DiskModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => DiskConfig.Descriptor;
}
