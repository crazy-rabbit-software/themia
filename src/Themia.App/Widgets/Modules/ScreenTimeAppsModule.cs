using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("screenTimeApps", typeof(Themia.App.Widgets.Modules.ScreenTimeAppsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The ScreenTimeApps widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ScreenTimeAppsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ScreenTimeAppsConfig.Descriptor;
}
