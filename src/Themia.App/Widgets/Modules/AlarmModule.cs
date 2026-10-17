using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("alarm", typeof(Themia.App.Widgets.Modules.AlarmModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Alarm widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class AlarmModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => AlarmConfig.Descriptor;
}
