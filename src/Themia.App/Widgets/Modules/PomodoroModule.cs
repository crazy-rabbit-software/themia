using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("pomodoro", typeof(Themia.App.Widgets.Modules.PomodoroModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Pomodoro widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class PomodoroModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => PomodoroConfig.Descriptor;
}
