using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("nextMeeting", typeof(Themia.App.Widgets.Modules.NextMeetingModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The NextMeeting widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class NextMeetingModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => NextMeetingConfig.Descriptor;
}
