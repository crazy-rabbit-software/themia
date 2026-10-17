using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("memory", typeof(Themia.App.Widgets.Modules.MemoryModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Memory widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MemoryModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MemoryConfig.Descriptor;
}
