using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Services;

/// <summary>
/// [S-880] The one way any part of the app says something transient to the user: an undo offer, a
/// licence refusal, a device that would not switch, a widget that failed. Every notice is also
/// logged, so "log it and surface it" is one call.
/// <para>
/// Static, like <see cref="AppServices"/>, because the callers are widget views and services built
/// with nothing injected. Callable from any thread: the notice is handed to the UI thread, where
/// the host picks the display to show it on (the one under the pointer). Before the host has
/// started, and in a debug render, there is no surface — the notice is logged and dropped.
/// </para>
/// </summary>
internal static class Notices
{
    private const string Scope = "notice";

    private static DispatcherQueue? s_dispatcher;
    private static Action<Notice>? s_sink;

    /// <summary>Show <paramref name="notice"/> on the desktop.</summary>
    public static void Show(Notice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);

        var line = notice.Message is null ? notice.Title : $"{notice.Title}: {notice.Message}";
        switch (notice.Kind)
        {
            case NoticeKind.Error:
                Log.Error(Scope, line);
                break;
            case NoticeKind.Warning:
                Log.Warn(Scope, line);
                break;
            default:
                Log.Info(Scope, line);
                break;
        }

        var sink = s_sink;
        var dispatcher = s_dispatcher;
        if (sink is null || dispatcher is null)
        {
            return;
        }

        if (dispatcher.HasThreadAccess)
        {
            Deliver(sink, notice);
        }
        else if (!dispatcher.TryEnqueue(() => Deliver(sink, notice)))
        {
            Log.Warn(Scope, "the UI thread is shutting down; the notice was not shown");
        }
    }

    public static void Info(string title, string? message = null, NoticeAction? action = null)
        => Show(new Notice(NoticeKind.Info, title) { Message = message, Action = action });

    public static void Warning(string title, string? message = null, NoticeAction? action = null)
        => Show(new Notice(NoticeKind.Warning, title) { Message = message, Action = action });

    public static void Error(string title, string? message = null, NoticeAction? action = null)
        => Show(new Notice(NoticeKind.Error, title) { Message = message, Action = action });

    /// <summary>The host's surfaces are up: notices go to them from now on.</summary>
    internal static void Connect(DispatcherQueue dispatcher, Action<Notice> sink)
    {
        s_dispatcher = dispatcher;
        s_sink = sink;
    }

    /// <summary>The host is going away: nothing is left to draw on.</summary>
    internal static void Disconnect()
    {
        s_sink = null;
        s_dispatcher = null;
    }

    private static void Deliver(Action<Notice> sink, Notice notice)
    {
        try
        {
            sink(notice);
        }
        catch (Exception ex)
        {
            // The notice pipeline must never be what breaks the caller; it is already in the log.
            Log.Error(Scope, "could not show a notice", ex);
        }
    }
}
