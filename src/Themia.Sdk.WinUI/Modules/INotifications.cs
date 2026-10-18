using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>What the notification widgets show: whether Windows lets Themia read its list, and the list.</summary>
public sealed record NotificationSnapshot(NotificationAccess Access, IReadOnlyList<NotificationItem> Items)
{
    public static NotificationSnapshot Pending { get; } = new(NotificationAccess.Pending, []);
}

/// <summary>
/// [S-4401] [S-4408] Windows' notification list as a notification widget reaches it
/// (<see cref="IWidgetHost.Notifications"/>): one feed for the whole app, counted by its widgets (the
/// first starts it, the last stops it); the listener and its access stay host code.
/// </summary>
public interface INotifications
{
    /// <summary>What every widget shows now.</summary>
    NotificationSnapshot Snapshot { get; }

    /// <summary>Follow the list; <paramref name="changed"/> runs on the calling thread's dispatcher. Dispose to let go.</summary>
    IDisposable Acquire(Action changed);

    /// <summary>Look at the access again (the privacy page has no event) and read the list.</summary>
    void Refresh();

    /// <summary>[S-4407] One notification: hidden at once, removed from Windows' list.</summary>
    void Dismiss(uint id);

    /// <summary>[S-4402] Every notification of one app.</summary>
    void DismissApp(string appId);

    /// <summary>[S-4402] Windows' whole list, as its own Clear all.</summary>
    void ClearAll();

    /// <summary>[S-4403] Ask Windows for access, with its own prompt. UI thread. The answer; the list follows through the feed.</summary>
    Task<NotificationAccess> RequestAccessAsync();
}
