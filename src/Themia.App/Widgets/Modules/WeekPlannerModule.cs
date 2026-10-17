using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("weekPlanner", typeof(Themia.App.Widgets.Modules.WeekPlannerModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The WeekPlanner widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class WeekPlannerModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => WeekPlannerConfig.Descriptor;
}
