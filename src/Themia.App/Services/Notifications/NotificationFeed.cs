using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Windows.Foundation;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Themia.App.Services.Notifications;

/// <summary>What the notification widgets show: whether Windows lets Themia read its list, and the list.</summary>
internal sealed record NotificationSnapshot(NotificationAccess Access, IReadOnlyList<NotificationItem> Items)
{
    public static NotificationSnapshot Pending { get; } = new(NotificationAccess.Pending, []);
}

/// <summary>
/// [S-4401] Windows' notification list, read through <c>UserNotificationListener</c> — the documented
/// listener, which a packaged Themia may use with the <c>userNotificationListener</c> capability and
/// Windows' consent, and which answers an unpackaged process too where the privacy page's notification
/// access allows it. Karpo reads Windows' own database (<c>wpndatabase.db</c>) for the whole toast and
/// dismisses through this listener; Themia ships through the Store, so it reads only through the
/// listener: the toast's words and its sender, not its buttons.
/// <para>
/// <b>One feed for the whole app</b>, counted by its widgets like the toggles' hub ([S-3907]): the first
/// widget starts it, the last one stops it. A change reaches each widget on its own thread.
/// </para>
/// <para>
/// <b>Heard, not polled</b> ([S-4408]): the listener's <c>NotificationChanged</c> where Windows raises
/// it to this process; where it refuses the subscription (it does to some unpackaged callers), the
/// change notification of the folder Windows keeps the list in, as a signal only — the folder's files
/// are never opened. Reads are single-flight: a change during a read asks for one more read, not one
/// per change. Access, which Windows announces nowhere, is looked at again when the pointer comes by.
/// </para>
/// </summary>
internal static class NotificationFeed
{
    private const string Scope = "notifications";

    private static readonly Lock Gate = new();
    private static readonly List<NotificationLease> Leases = [];
    private static readonly HashSet<uint> Hidden = [];

    private static NotificationSnapshot _fromWindows = NotificationSnapshot.Pending;
    private static NotificationSnapshot _shown = NotificationSnapshot.Pending;
    private static UserNotificationListener? _listener;
    private static TypedEventHandler<UserNotificationListener, UserNotificationChangedEventArgs>? _handler;
    private static FileSystemWatcher? _watcher;
    private static bool _reading;
    private static bool _again;
    private static int _generation;

    /// <summary>What every widget shows now.</summary>
    public static NotificationSnapshot Snapshot
    {
        get
        {
            lock (Gate)
            {
                return _shown;
            }
        }
    }

    /// <summary>Follow the list; <paramref name="changed"/> runs on the calling thread's dispatcher.</summary>
    public static NotificationLease Acquire(Action changed)
    {
        var queue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("acquire the notification feed from a thread with a dispatcher");

        var lease = new NotificationLease(queue, changed);
        bool first;
        int generation;
        lock (Gate)
        {
            first = Leases.Count == 0;
            Leases.Add(lease);
            if (first)
            {
                _generation++;
            }

            generation = _generation;
        }

        if (first)
        {
            _ = Task.Run(() => StartAsync(generation));
        }

        return lease;
    }

    internal static void Release(NotificationLease lease)
    {
        bool last;
        lock (Gate)
        {
            last = Leases.Remove(lease) && Leases.Count == 0;
            if (last)
            {
                _generation++;
            }
        }

        if (last)
        {
            Stop();
        }
    }

    /// <summary>Look at the access again (the privacy page has no event) and read the list.</summary>
    public static void Refresh() => _ = Task.Run(() => ReadAsync(Volatile.Read(ref _generation)));

    /// <summary>[S-4407] One notification: hidden at once, removed from Windows' list on the pool.</summary>
    public static void Dismiss(uint id) => Remove([id]);

    /// <summary>[S-4402] Every notification of one app.</summary>
    public static void DismissApp(string appId)
    {
        uint[] ids;
        lock (Gate)
        {
            ids = [.. _shown.Items.Where(item => string.Equals(item.AppId, appId, StringComparison.OrdinalIgnoreCase)).Select(item => item.Id)];
        }

        Remove(ids);
    }

    /// <summary>[S-4402] Windows' whole list, as its own Clear all.</summary>
    public static void ClearAll()
    {
        uint[] ids;
        lock (Gate)
        {
            ids = [.. _shown.Items.Select(item => item.Id)];
            Hidden.UnionWith(ids);
            Recompute();
        }

        Raise();
        _ = Task.Run(() => Act("clearing the notification list", listener => listener.ClearNotifications()));
    }

    /// <summary>
    /// [S-4403] Ask Windows for access, with its own prompt (a packaged Themia, never asked before).
    /// UI thread: the prompt needs it. Returns the answer; the list follows through the feed.
    /// </summary>
    public static async Task<NotificationAccess> RequestAccessAsync()
    {
        try
        {
            var listener = UserNotificationListener.Current;
            var status = await listener.RequestAccessAsync();
            Refresh();
            return Map(status);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "Windows would not ask for notification access", ex);
            return NotificationAccess.Unsupported;
        }
    }

    private static void Remove(uint[] ids)
    {
        if (ids.Length == 0)
        {
            return;
        }

        lock (Gate)
        {
            Hidden.UnionWith(ids);
            Recompute();
        }

        Raise();
        _ = Task.Run(() => Act("dismissing notifications", listener =>
        {
            foreach (var id in ids)
            {
                listener.RemoveNotification(id);
            }
        }));
    }

    /// <summary>Run a removal on Windows' listener, then read again so what Windows kept shows.</summary>
    private static async Task Act(string what, Action<UserNotificationListener> act)
    {
        UserNotificationListener? listener;
        int generation;
        lock (Gate)
        {
            listener = _listener;
            generation = _generation;
        }

        if (listener is null)
        {
            return;
        }

        try
        {
            act(listener);
        }
        catch (Exception ex)
        {
            // Not deleted, only not removed from Windows' list: the next read shows it again.
            Log.Warn(Scope, $"Windows refused {what}", ex);
            lock (Gate)
            {
                Hidden.Clear();
                Recompute();
            }

            Raise();
        }

        await ReadAsync(generation).ConfigureAwait(false);
    }

    private static async Task StartAsync(int generation)
    {
        UserNotificationListener listener;
        try
        {
            listener = UserNotificationListener.Current;
        }
        catch (Exception ex)
        {
            Log.Info(Scope, $"this Windows has no notification listener (0x{ex.HResult:X8})");
            Publish(generation, new NotificationSnapshot(NotificationAccess.Unsupported, []));
            return;
        }

        lock (Gate)
        {
            if (generation != _generation)
            {
                return;
            }

            _listener = listener;
        }

        await ReadAsync(generation).ConfigureAwait(false);
    }

    /// <summary>Single-flight: a read asked for while one runs makes that one go round once more.</summary>
    private static async Task ReadAsync(int generation)
    {
        UserNotificationListener? listener;
        lock (Gate)
        {
            if (generation != _generation || _listener is null)
            {
                return;
            }

            if (_reading)
            {
                _again = true;
                return;
            }

            _reading = true;
            listener = _listener;
        }

        try
        {
            do
            {
                lock (Gate)
                {
                    _again = false;
                }

                var snapshot = await ReadOnceAsync(listener).ConfigureAwait(false);
                if (snapshot.Access == NotificationAccess.Allowed)
                {
                    Follow(generation, listener);
                }

                Publish(generation, snapshot);
            }
            while (Again(generation));
        }
        finally
        {
            lock (Gate)
            {
                _reading = false;
            }
        }
    }

    private static bool Again(int generation)
    {
        lock (Gate)
        {
            return _again && generation == _generation;
        }
    }

    private static async Task<NotificationSnapshot> ReadOnceAsync(UserNotificationListener listener)
    {
        NotificationAccess access;
        try
        {
            access = Map(listener.GetAccessStatus());
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "Windows would not say whether Themia may read notifications", ex);
            return new NotificationSnapshot(NotificationAccess.Unsupported, []);
        }

        if (access != NotificationAccess.Allowed)
        {
            return new NotificationSnapshot(access, []);
        }

        IReadOnlyList<UserNotification> raw;
        try
        {
            raw = await listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Allowed, and still refused: said as denied, which offers Windows' page.
            Log.Warn(Scope, "Windows' notification list could not be read", ex);
            return new NotificationSnapshot(NotificationAccess.Denied, []);
        }

        var items = new List<NotificationItem>(Math.Min(raw.Count, NotificationRules.MostItems));
        foreach (var notification in raw)
        {
            if (Item(notification) is { } item)
            {
                items.Add(item);
            }
        }

        return new NotificationSnapshot(NotificationAccess.Allowed, [.. NotificationRules.Newest(items).Take(NotificationRules.MostItems)]);
    }

    /// <summary>[S-4401] One notification as text and sender; null where Windows' object would not be read.</summary>
    private static NotificationItem? Item(UserNotification notification)
    {
        try
        {
            var (appId, appName) = Sender(notification);
            var binding = notification.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
            var texts = binding?.GetTextElements().Select(text => text.Text) ?? [];
            var (title, body) = NotificationRules.Text(texts);
            return new NotificationItem(notification.Id, appId, appName, title, body, notification.CreationTime);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "a notification could not be read; skipped", ex);
            return null;
        }
    }

    /// <summary>The sender's AUMID and name; Windows' own senders may refuse their display info.</summary>
    private static (string Id, string Name) Sender(UserNotification notification)
    {
        string id = string.Empty;
        string name = string.Empty;
        try
        {
            var info = notification.AppInfo;
            id = info?.AppUserModelId ?? string.Empty;
            name = info?.DisplayInfo?.DisplayName ?? string.Empty;
        }
        catch (Exception ex)
        {
            // ignore: an app without display info is named from its id ([S-4405]).
            Log.Debug(Scope, $"a sender had no display info (0x{ex.HResult:X8})");
        }

        return (id, name.Trim());
    }

    /// <summary>[S-4408] Hear changes: the listener's event, else the list's folder as a signal.</summary>
    private static void Follow(int generation, UserNotificationListener listener)
    {
        lock (Gate)
        {
            if (generation != _generation || _handler is not null || _watcher is not null)
            {
                return;
            }
        }

        TypedEventHandler<UserNotificationListener, UserNotificationChangedEventArgs> handler = (_, _) => Heard(generation);
        try
        {
            listener.NotificationChanged += handler;
            lock (Gate)
            {
                if (generation == _generation)
                {
                    _handler = handler;
                    return;
                }
            }

            listener.NotificationChanged -= handler;
            return;
        }
        catch (Exception ex)
        {
            Log.Info(Scope, $"Windows does not raise NotificationChanged to this process (0x{ex.HResult:X8}); following the list's folder");
        }

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Notifications");
        if (!Directory.Exists(folder))
        {
            Log.Warn(Scope, "no folder to follow: the list is read when the pointer comes by");
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(folder, "wpndatabase.db*")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };
            watcher.Changed += (_, _) => Heard(generation);
            watcher.Error += (_, args) => Log.Warn(Scope, "the notification folder watch failed", args.GetException());
            watcher.EnableRaisingEvents = true;

            lock (Gate)
            {
                if (generation == _generation)
                {
                    _watcher = watcher;
                    return;
                }
            }

            watcher.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Warn(Scope, "the notification folder cannot be followed: the list is read when the pointer comes by", ex);
        }
    }

    /// <summary>From Windows' thread: something changed; read again (single-flight).</summary>
    private static void Heard(int generation)
    {
        try
        {
            _ = Task.Run(() => ReadAsync(generation));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a change in the notification list could not be followed", ex);
        }
    }

    private static void Publish(int generation, NotificationSnapshot snapshot)
    {
        lock (Gate)
        {
            if (generation != _generation)
            {
                return;
            }

            _fromWindows = snapshot;
            Recompute();
        }

        Raise();
    }

    /// <summary>Under the lock: Windows' list less what was dismissed here and not gone yet ([S-4407]).</summary>
    private static void Recompute()
    {
        var (shown, still) = NotificationRules.Visible(_fromWindows.Items, Hidden);
        Hidden.IntersectWith(still);
        _shown = _fromWindows with { Items = shown };
    }

    private static void Raise()
    {
        NotificationLease[] leases;
        lock (Gate)
        {
            leases = [.. Leases];
        }

        foreach (var lease in leases)
        {
            lease.Post();
        }
    }

    private static void Stop()
    {
        UserNotificationListener? listener;
        TypedEventHandler<UserNotificationListener, UserNotificationChangedEventArgs>? handler;
        FileSystemWatcher? watcher;
        lock (Gate)
        {
            listener = _listener;
            handler = _handler;
            watcher = _watcher;
            _listener = null;
            _handler = null;
            _watcher = null;
            _fromWindows = NotificationSnapshot.Pending;
            _shown = NotificationSnapshot.Pending;
            Hidden.Clear();
        }

        watcher?.Dispose();
        if (listener is not null && handler is not null)
        {
            try
            {
                listener.NotificationChanged -= handler;
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "could not stop following the notification list", ex);
            }
        }
    }

    private static NotificationAccess Map(UserNotificationListenerAccessStatus status) => status switch
    {
        UserNotificationListenerAccessStatus.Allowed => NotificationAccess.Allowed,
        UserNotificationListenerAccessStatus.Denied => NotificationAccess.Denied,
        _ => NotificationAccess.Unspecified,
    };
}

/// <summary>One widget's hold on the feed.</summary>
internal sealed class NotificationLease : IDisposable
{
    private readonly DispatcherQueue _queue;
    private readonly Action _changed;
    private volatile bool _disposed;

    internal NotificationLease(DispatcherQueue queue, Action changed)
    {
        _queue = queue;
        _changed = changed;
    }

    internal void Post()
    {
        if (_disposed)
        {
            return;
        }

        _ = _queue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _changed();
            }
            catch (Exception ex)
            {
                Log.Error("notifications", "a notifications widget could not take a change", ex);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NotificationFeed.Release(this);
    }
}
