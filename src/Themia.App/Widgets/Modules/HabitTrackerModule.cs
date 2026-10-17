using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("habitTracker", typeof(Themia.App.Widgets.Modules.HabitTrackerModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The HabitTracker widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class HabitTrackerModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => HabitTrackerConfig.Descriptor;
}
