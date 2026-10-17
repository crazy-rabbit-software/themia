using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("screenTimeChart", typeof(Themia.App.Widgets.Modules.ScreenTimeChartModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The ScreenTimeChart widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ScreenTimeChartModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ScreenTimeChartConfig.Descriptor;
}
