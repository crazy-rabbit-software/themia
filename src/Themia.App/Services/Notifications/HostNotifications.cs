using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Notifications;

/// <summary>[S-4408] The app's one notification feed (<see cref="NotificationFeed"/>) as a widget's host hands it over.</summary>
internal sealed class HostNotifications : INotifications
{
    public static HostNotifications Instance { get; } = new();

    private HostNotifications()
    {
    }

    public NotificationSnapshot Snapshot => NotificationFeed.Snapshot;

    public IDisposable Acquire(Action changed) => NotificationFeed.Acquire(changed);

    public void Refresh() => NotificationFeed.Refresh();

    public void Dismiss(uint id) => NotificationFeed.Dismiss(id);

    public void DismissApp(string appId) => NotificationFeed.DismissApp(appId);

    public void ClearAll() => NotificationFeed.ClearAll();

    public Task<NotificationAccess> RequestAccessAsync() => NotificationFeed.RequestAccessAsync();
}
