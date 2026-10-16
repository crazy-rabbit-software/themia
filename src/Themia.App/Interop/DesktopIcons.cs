using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Interop;

/// <summary>
/// [S-930] Windows' own desktop icons: whether they are shown, the switch that hides them, and their
/// selection.
/// <para>
/// Everything goes through the shell, never around it. Hiding is the exact command the desktop's
/// context menu posts (View › Show desktop icons), so Explorer hides the icons <i>and</i> persists it
/// in its own settings: the choice survives Themia quitting, Explorer restarting and a reboot, and
/// the context menu's check mark stays honest. Themia never re-asserts or restores it ([S-932]).
/// </para>
/// <para>
/// The shell's windows are looked up on every use rather than cached: Explorer restarts replace
/// them, and a cached handle would send the command to a dead window. The lookup is a handful of
/// <c>FindWindowEx</c> calls, and it only runs on a user action or a shell event.
/// </para>
/// </summary>
internal static unsafe class DesktopIcons
{
    private const string Scope = "desktop-icons";

    /// <summary>The desktop context menu's "Show desktop icons" command. Undocumented, unchanged since Vista, the same on 10 and 11.</summary>
    private const nint ToggleIconsCommand = 0x7402;

    private const uint LvmGetSelectedCount = 0x1032;

    /// <summary>
    /// The desktop's view (<c>SHELLDLL_DefView</c>) and its icon list (<c>SysListView32</c>), or null
    /// when Explorer is not running. The view lives under <c>Progman</c>, or under a <c>WorkerW</c>
    /// once a wallpaper slideshow or a wallpaper engine has put one in between.
    /// </summary>
    public static (nint View, nint List)? Find()
    {
        // The shell's own Progman, not the first window of that class: any program may register a
        // "Progman" or a "WorkerW" (a dock's stand-in shell windows, the shell's own worker windows
        // in every process that uses it). Only views of the shell's process are candidates ([S-930]).
        var progman = Win32.GetShellWindow();
        if (progman == 0)
        {
            return null;
        }

        Win32.GetWindowThreadProcessId(progman, out var shellPid);
        if (shellPid == 0)
        {
            return null;
        }

        var found = new List<(nint View, nint List)>();
        var candidates = new List<DesktopViewCandidate>();

        void Consider(nint parent, bool underProgman)
        {
            var view = Win32.FindWindowEx(parent, 0, "SHELLDLL_DefView", null);
            if (view == 0)
            {
                return;
            }

            Win32.GetWindowThreadProcessId(view, out var pid);

            // FindWindowEx ignores visibility, so the list is found whether the icons are shown or not.
            var list = Win32.FindWindowEx(view, 0, "SysListView32", null);
            found.Add((view, list));
            candidates.Add(new DesktopViewCandidate(underProgman, pid == shellPid, list != 0));
        }

        Consider(progman, underProgman: true);

        // A WorkerW in between: a child of Progman on Windows 11 24H2 and later, top-level before.
        for (var worker = Win32.FindWindowEx(progman, 0, "WorkerW", null); worker != 0; worker = Win32.FindWindowEx(progman, worker, "WorkerW", null))
        {
            Consider(worker, underProgman: false);
        }

        for (var worker = Win32.FindWindowEx(0, 0, "WorkerW", null); worker != 0; worker = Win32.FindWindowEx(0, worker, "WorkerW", null))
        {
            Consider(worker, underProgman: false);
        }

        var chosen = DesktopIconRules.ChooseView(candidates);
        return chosen < 0 ? null : found[chosen];
    }

    /// <summary>
    /// [S-931] The icon list's own <c>WS_VISIBLE</c> bit, or null when the window has gone (a live
    /// list always has a style: it is a <c>WS_CHILD</c>).
    /// </summary>
    private static bool? IsShown(nint list)
    {
        var style = (uint)Win32.GetWindowLongPtr(list, Win32.GwlStyle);
        return style == 0 ? null : DesktopIconRules.IsShown(style);
    }

    /// <summary>
    /// [S-931] Whether the icons are shown, or null when the shell cannot be found. Explorer hides the
    /// list window itself, so its own visible bit <i>is</i> the state and is current the instant the
    /// shell has handled the command — unlike the persisted flag, which Explorer writes lazily.
    /// Non-blocking: no message goes to Explorer, so the UI thread may call it.
    /// </summary>
    public static bool? AreVisible() => Find() is { } shell ? IsShown(shell.List) : null;

    /// <summary>Bounds the wait on a hung Explorer; a responsive one answers in milliseconds.</summary>
    private const uint CommandTimeoutMs = 3000;

    private static readonly DesktopIconRequests Requests = new();

    /// <summary>[S-939] A request is being applied. The switch does not adopt the live state meanwhile.</summary>
    public static bool Busy => Requests.Busy;

    /// <summary>
    /// [S-939] Raised on a worker thread once every request has been applied (or given up on, logged).
    /// The switch reads the live state then. Subscribe while on screen only.
    /// </summary>
    public static event Action? Settled;

    /// <summary>
    /// [S-939] Ask for the icons shown or hidden. Returns at once; one worker applies the requests,
    /// latest wins, and raises <see cref="Settled"/> when it is done.
    /// </summary>
    public static void Request(bool visible)
    {
        if (!Requests.Submit(visible))
        {
            return;
        }

        _ = Task.Run(static () =>
        {
            while (Requests.Take() is bool target)
            {
                try
                {
                    Apply(target);
                }
                catch (Exception ex)
                {
                    Log.Error(Scope, "applying the desktop icons switch threw", ex);
                }
            }

            try
            {
                Settled?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a desktop icons listener threw", ex);
            }
        });
    }

    /// <summary>
    /// [S-931] [S-939] Bring the icons to <paramref name="visible"/>. The command is a toggle with no
    /// "set" form, so it is sent only when the live state is known and differs, and at most once.
    /// Sent, not posted, and bounded: a posted command is still queued in Explorer when the state is
    /// read back, so the read cannot tell "done" from "not yet", and the next request would toggle on
    /// a stale state. The send returns once Explorer has handled it; the read after it is the
    /// confirmation. Blocking — the worker thread only.
    /// </summary>
    private static void Apply(bool visible)
    {
        var sent = false;
        while (true)
        {
            var shell = Find();
            var live = shell is { } found ? IsShown(found.List) : null;

            switch (DesktopIconRules.Plan(live, visible, sent))
            {
                case DesktopIconStep.Done:
                    if (sent)
                    {
                        Log.Info(Scope, visible ? "desktop icons shown" : "desktop icons hidden");
                    }

                    return;

                case DesktopIconStep.Unreadable:
                    Log.Warn(Scope, sent
                        ? "the desktop's icon view went away after the desktop icons command; the switch shows what Windows reports next"
                        : "the desktop's icon view was not found; desktop icons left as they are");
                    return;

                case DesktopIconStep.DidNotTake:
                    Log.Warn(Scope, $"Explorer handled the desktop icons command but the icons are still {(live == true ? "shown" : "hidden")}; not toggling again");
                    return;

                case DesktopIconStep.Toggle:
                    Marshal.SetLastPInvokeError(0);
                    if (Win32.SendMessageTimeout(shell!.Value.View, Win32.WmCommand, ToggleIconsCommand, 0,
                            Win32.SmtoAbortIfHung | Win32.SmtoBlock, CommandTimeoutMs, out _) == 0)
                    {
                        // A timeout may still be handled later; the list's show/hide event then tells the switch.
                        Log.Warn(Scope, $"Explorer did not take the desktop icons command (win32 {Marshal.GetLastPInvokeError()}); desktop icons left as they are");
                        return;
                    }

                    sent = true;
                    break;

                default:
                    Log.Error(Scope, $"unknown desktop icons step for visible={visible}");
                    return;
            }
        }
    }

    /// <summary>
    /// [S-938] Clear the desktop's icon selection, as a click anywhere else on the desktop would.
    /// <para>
    /// Asks first, cheaply: <c>LVM_GETSELECTEDCOUNT</c> needs no memory in Explorer, so the common
    /// click with nothing selected costs one message. Only when something is selected does it talk
    /// to the desktop's folder view (<see cref="DesktopFolderView"/>). Blocking — call it off the UI
    /// thread.
    /// </para>
    /// </summary>
    public static void ClearSelection()
    {
        if (Find() is not { } shell)
        {
            return;
        }

        // Bounded: a hung Explorer must not hold this thread.
        if (Win32.SendMessageTimeout(shell.List, LvmGetSelectedCount, 0, 0, Win32.SmtoAbortIfHung | Win32.SmtoBlock, 200, out var count) == 0
            || count <= 0)
        {
            return;
        }

        DesktopFolderView.DeselectAll();
    }

    /// <summary>
    /// [S-932] Hear the icons being shown or hidden, by anyone — the desktop's context menu, a script,
    /// this app. The shell broadcasts nothing for it, but the list window's own show and hide are
    /// window events, so a WinEvent hook scoped to that one thread hears exactly them. Dispose to stop.
    /// Returns null (logged) when the shell cannot be found or the hook cannot be installed.
    /// </summary>
    public static IDisposable? Watch(Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);

        if (Find() is not { } shell)
        {
            return null;
        }

        var thread = Win32.GetWindowThreadProcessId(shell.List, out var pid);
        if (thread == 0)
        {
            return null;
        }

        var watch = new IconWatch(shell.List, changed);
        var callback = (nint)(delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void>)&OnWinEvent;
        watch.Hook = Win32.SetWinEventHook(Win32.EventObjectShow, Win32.EventObjectHide, 0, callback, pid, thread, Win32.WinEventOutOfContext);

        if (watch.Hook == 0)
        {
            Log.Warn(Scope, "could not watch the desktop icons; the switch shows what was read when the page opened");
            return null;
        }

        lock (Watches)
        {
            Watches.Add(watch);
        }

        return watch;
    }

    private static readonly List<IconWatch> Watches = [];

    [UnmanagedCallersOnly]
    private static void OnWinEvent(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        // A hook callback must never let an exception out; one that misbehaves is removed for good.
        try
        {
            // Icons (child items) are shown and hidden all the time; only the list window itself is the switch.
            if (objectId != Win32.ObjIdWindow || childId != Win32.ChildIdSelf)
            {
                return;
            }

            IconWatch? match = null;
            lock (Watches)
            {
                foreach (var watch in Watches)
                {
                    if (watch.Hook == hook && watch.List == hwnd)
                    {
                        match = watch;
                        break;
                    }
                }
            }

            match?.Changed();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the desktop icons hook threw", ex);
        }
    }

    private sealed class IconWatch(nint list, Action changed) : IDisposable
    {
        public nint List { get; } = list;

        public nint Hook { get; set; }

        public void Changed() => changed();

        public void Dispose()
        {
            lock (Watches)
            {
                Watches.Remove(this);
            }

            if (Hook != 0)
            {
                Win32.UnhookWinEvent(Hook);
                Hook = 0;
            }
        }
    }
}
