using System.Runtime.InteropServices;
using Microsoft.Win32;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Interop;

/// <summary>
/// [S-5503] [S-5504] Explorer's own desktop icons: the session-only hide Themia holds while it runs,
/// its restore, and their selection.
/// <para>
/// Themia draws the desktop, so it hides Explorer's icon list window itself for this session only
/// (<see cref="Hold"/>): Explorer's own setting and its menu's check mark stay as the user left them, an
/// Explorer restart shows the icons by itself, and <see cref="Restore"/> shows them again on quit and
/// from the last-chance handlers. The shell's own command (View › Show desktop icons), which Explorer
/// persists, is never sent.
/// </para>
/// <para>
/// The shell's windows are looked up on every use rather than cached: Explorer restarts replace
/// them, and a cached handle would reach a dead window. The lookup is a handful of
/// <c>FindWindowEx</c> calls, and it only runs on a user action or a shell event.
/// </para>
/// </summary>
internal static unsafe class DesktopIcons
{
    private const string Scope = "desktop-icons";

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
        // in every process that uses it). Only views of the shell's process are candidates ([S-5503]).
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
    /// [S-5503] The icon list's own <c>WS_VISIBLE</c> bit, or null when the window has gone (a live
    /// list always has a style: it is a <c>WS_CHILD</c>).
    /// </summary>
    private static bool? IsShown(nint list)
    {
        var style = (uint)Win32.GetWindowLongPtr(list, Win32.GwlStyle);
        return style == 0 ? null : DesktopIconRules.IsShown(style);
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

    // ---- [S-5503] [S-5504] the session-only hide ------------------------------------------

    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string PolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";

    /// <summary>The list is held hidden. UI thread.</summary>
    private static bool _holding;

    /// <summary>[S-5503] Re-hides spent since the hold began (at start, and anew for a new Explorer). UI thread.</summary>
    private static int _rehides;

    /// <summary>The held list's show and hide hook. UI thread.</summary>
    private static IDisposable? _holdWatch;

    /// <summary>
    /// [S-5504] This process hid the list at least once: only then do quit and the last-chance
    /// handlers restore it, so a debug render (which never hides) never touches the desktop's icons.
    /// </summary>
    private static volatile bool _hidThisRun;

    /// <summary>
    /// [S-5503] Hide Explorer's icon list for the session and keep it hidden: its own show event, and
    /// the host holding again on <c>TaskbarCreated</c> for a new Explorer, hide it again (at most
    /// <see cref="DesktopIconRules.MaxRehides"/> times, counted anew each call). Never Explorer's
    /// persisted setting. UI thread.
    /// </summary>
    public static void Hold()
    {
        _holding = true;
        _rehides = 0;
        _hidThisRun = true;
        HookAndHide();
    }

    /// <summary>[S-5504] Quit: the hold ends (unhooked first, so the show is not answered with a hide) and the list is shown as Windows has it. UI thread.</summary>
    public static void Release()
    {
        _holding = false;
        _holdWatch?.Dispose();
        _holdWatch = null;
        RestoreIfHidden();
    }

    private static void HookAndHide()
    {
        _holdWatch?.Dispose();
        _holdWatch = Watch(OnHeldListChanged);

        if (Find() is not { } shell || shell.List == 0)
        {
            Log.Warn(Scope, "the desktop's icon list was not found; it is hidden when Explorer builds its desktop");
            return;
        }

        if (IsShown(shell.List) != true)
        {
            return;
        }

        if (!Win32.ShowWindowAsync(shell.List, Win32.SwHide))
        {
            Log.Warn(Scope, $"could not hide the desktop's icons (win32 {Marshal.GetLastPInvokeError()}); they show under Themia's desktop");
            return;
        }

        Log.Info(Scope, "desktop icons hidden for this session");
    }

    /// <summary>[S-5503] The held list was shown or hidden: shown again while held, it is hidden again, up to the cap.</summary>
    private static void OnHeldListChanged()
    {
        if (Find() is not { } shell)
        {
            return;
        }

        var shown = IsShown(shell.List);
        if (DesktopIconRules.Rehide(_holding, shown, _rehides))
        {
            _rehides++;
            if (!Win32.ShowWindowAsync(shell.List, Win32.SwHide))
            {
                Log.Warn(Scope, $"could not hide the desktop's icons again (win32 {Marshal.GetLastPInvokeError()})");
            }

            return;
        }

        if (_holding && shown == true && _rehides == DesktopIconRules.MaxRehides)
        {
            // [S-5575] Said once: the count passes the cap, so this is not reached again until the hold begins anew.
            _rehides++;
            Log.Warn(Scope, $"the desktop's icons were shown again {DesktopIconRules.MaxRehides} times; something else keeps them shown, so they are left shown");
        }
    }

    /// <summary>[S-5504] The last-chance handlers (and quit, through <see cref="Release"/>): show the list again if this process hid it. Any thread, never blocks.</summary>
    public static void RestoreIfHidden()
    {
        if (_hidThisRun)
        {
            Restore();
        }
    }

    /// <summary>
    /// [S-5504] Show the list again as Windows has it: only a hidden list, only when Windows' own
    /// setting says shown. Any thread: no COM, no message awaited (the show is posted to Explorer's
    /// thread), so a crash handler may call it. Never throws.
    /// </summary>
    public static void Restore()
    {
        try
        {
            if (Find() is not { } shell || shell.List == 0)
            {
                return;
            }

            if (!DesktopIconRules.Restore(IsShown(shell.List), WindowsHides()))
            {
                return;
            }

            if (!Win32.ShowWindowAsync(shell.List, Win32.SwShowNa))
            {
                Log.Warn(Scope, $"could not show the desktop's icons again (win32 {Marshal.GetLastPInvokeError()}); Explorer shows them when it restarts");
                return;
            }

            Log.Info(Scope, "desktop icons shown again, as Windows has them");
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "restoring the desktop's icons threw", ex);
        }
    }

    /// <summary>[S-5504] Whether Windows' own setting hides the icons: Explorer's <c>HideIcons</c>, or the <c>NoDesktop</c> policy.</summary>
    public static bool WindowsHides()
        => DesktopIconRules.WindowsHides(
            ReadDword(Registry.CurrentUser, AdvancedKey, "HideIcons"),
            ReadDword(Registry.CurrentUser, PolicyKey, "NoDesktop") ?? ReadDword(Registry.LocalMachine, PolicyKey, "NoDesktop"));

    /// <summary>A DWORD value, or null when it is absent; one that cannot be read is logged and taken as absent.</summary>
    private static int? ReadDword(RegistryKey hive, string path, string name)
    {
        try
        {
            using var key = hive.OpenSubKey(path, writable: false);
            return key?.GetValue(name) is int value ? value : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Warn(Scope, $"could not read {name} under {path}", ex);
            return null;
        }
    }

    /// <summary>
    /// [S-5503] Hear the icon list being shown or hidden, by anyone: Explorer, a script, this app. The
    /// shell broadcasts nothing for it, but the list window's own show and hide are window events, so a
    /// WinEvent hook scoped to that one thread hears exactly them. Dispose to stop. Returns null
    /// (logged) when the shell cannot be found or the hook cannot be installed.
    /// </summary>
    private static IDisposable? Watch(Action changed)
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
            Log.Warn(Scope, "could not watch the desktop's icon list; a list Explorer shows again stays shown");
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
