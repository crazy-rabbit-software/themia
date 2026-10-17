using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("notifications", typeof(Themia.App.Widgets.Modules.NotificationsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Notifications widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class NotificationsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => NotificationsConfig.Descriptor;
}
