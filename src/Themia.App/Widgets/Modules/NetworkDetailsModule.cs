using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("networkDetails", typeof(Themia.App.Widgets.Modules.NetworkDetailsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The NetworkDetails widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class NetworkDetailsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => NetworkDetailsConfig.Descriptor;
}
