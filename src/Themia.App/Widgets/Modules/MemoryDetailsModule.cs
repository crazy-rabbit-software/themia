using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("memoryDetails", typeof(Themia.App.Widgets.Modules.MemoryDetailsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The MemoryDetails widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MemoryDetailsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MemoryDetailsConfig.Descriptor;
}
