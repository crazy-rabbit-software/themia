#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2056]): <c>list</c> is the notifications' sample
/// (<see cref="ShowSampleAsync"/>); the rest the same notifications in more states, never the person's.
/// </summary>
internal sealed partial class NotificationsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["list", "many", "open", "empty", "denied", "unspecified", "unsupported"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "list")
        {
            return ShowSampleAsync();
        }

        ShowSnapshot(state switch
        {
            "many" or "open" => new NotificationSnapshot(NotificationAccess.Allowed, Sample(12)),
            "empty" => new NotificationSnapshot(NotificationAccess.Allowed, []),
            "denied" => new NotificationSnapshot(NotificationAccess.Denied, []),
            "unspecified" => new NotificationSnapshot(NotificationAccess.Unspecified, []),
            "unsupported" => new NotificationSnapshot(NotificationAccess.Unsupported, []),
            _ => null,
        }, state == "open" ? SampleChat : null);
        return Task.CompletedTask;
    }
}
#endif
