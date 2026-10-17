using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("battery", typeof(Themia.App.Widgets.Modules.BatteryModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Battery widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class BatteryModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => BatteryConfig.Descriptor;
}
