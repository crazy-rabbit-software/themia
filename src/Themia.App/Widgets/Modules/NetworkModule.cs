using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("network", typeof(Themia.App.Widgets.Modules.NetworkModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Network widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class NetworkModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => NetworkConfig.Descriptor;
}
