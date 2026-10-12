using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Interop;

/// <summary>
/// Keeps the widget surfaces where they belong: on the desktop, under every application, and
/// <b>visible</b> — including while Windows is showing the desktop.
/// <para>
/// Three separate mechanisms are needed, because the shell has three separate ways of getting a
/// window out of the way, and only one of them is z-order.
/// </para>
/// <para>
/// <b>Z-order.</b> A surface has to sit at the bottom, and it does not stay there by itself: edit
/// mode drops <c>WS_EX_NOACTIVATE</c> so the surface can take keyboard focus for the shortcuts, and
/// an activatable window is raised the moment it is clicked. So every window procedure is subclassed
/// and each <c>WM_WINDOWPOSCHANGING</c> is rewritten back to the anchor. Re-asserting on a timer
/// instead would be both a race and a wakeup this app does not otherwise need.
/// </para>
/// <para>
/// <b>Show Desktop.</b> Win+D raises the desktop window <i>past</i> normal z-order, above everything
/// pinned below it, so the widgets vanish at the one moment the user asked to see their desktop.
/// While the desktop is genuinely fronted there is nothing on screen for the widgets to cover, so
/// lifting them for the duration does not break the "never above other windows" rule — it is the only
/// way to honour it and still be visible. <see cref="ZOrderPolicy"/> owns that decision.
/// </para>
/// <para>
/// <b>Cloaking.</b> The part that actually defeated the first attempt at this. Show Desktop and a
/// fullscreen exclusive application both hide windows by asking DWM to <i>cloak</i> them, which goes
/// through neither <c>SC_MINIMIZE</c> nor <c>WM_SHOWWINDOW</c>, so a window procedure never sees it.
/// A cloaked window is still visible to <c>IsWindowVisible</c>, still not iconic, still at whatever
/// z-order you last put it at — and simply never composited. No amount of z-order work recovers it,
/// because z-order was never the problem. It is undone by clearing <c>DWMWA_CLOAK</c>, driven by an
/// <c>EVENT_OBJECT_CLOAKED</c> hook with a slow backstop behind it, because an out-of-context WinEvent
/// is silently dropped when the callback cannot be delivered in time — and a fullscreen game is
/// exactly the load that makes that happen, leaving the surface cloaked forever.
/// </para>
/// </summary>
internal static unsafe class DesktopZOrder
{
    private const string Scope = "z-order";
    private const uint WmWindowPosChanging = 0x0046;

    /// <summary>Steady cadence for the backstop. Nothing here is on a correctness path; it is a net.</summary>
    private static readonly TimeSpan IdleCadence = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Cadence for the moment after a shell event. A secondary monitor's <c>WorkerW</c> can elevate
    /// into the topmost band <i>after</i> the surfaces are pinned, so the pin has to be re-asserted a
    /// few times before settling back to the idle rate.
    /// </summary>
    private static readonly TimeSpan BusyCadence = TimeSpan.FromMilliseconds(100);

    /// <summary>How long to stay at the busy cadence after a shell event.</summary>
    private static readonly TimeSpan BusyWindow = TimeSpan.FromSeconds(1);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public nint Hwnd;
        public nint HwndInsertAfter;
        public int X;
        public int Y;
        public int Cx;
        public int Cy;
        public uint Flags;
    }

    private static readonly Lock Gate = new();
    private static readonly Dictionary<nint, nint> OriginalProcs = [];

    private static DispatcherQueueTimer? _backstop;
    private static nint _foregroundHook;
    private static nint _cloakHook;
    private static bool _pinnedTopmost;
    private static DateTimeOffset _busyUntil;

    /// <summary>Whether the surfaces are currently held above the elevated desktop layer.</summary>
    public static bool PinnedTopmost
    {
        get
        {
            lock (Gate)
            {
                return _pinnedTopmost;
            }
        }
    }

    /// <summary>
    /// Start watching the shell. Idempotent. The hooks are installed out-of-context so their
    /// callbacks arrive on this thread's message queue rather than inside another process.
    /// </summary>
    public static void Start(DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        if (_foregroundHook == 0)
        {
            var callback = (nint)(delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void>)&OnWinEvent;

            _foregroundHook = Win32.SetWinEventHook(
                Win32.EventSystemForeground,
                Win32.EventSystemForeground,
                0,
                callback,
                0,
                0,
                Win32.WinEventOutOfContext | Win32.WinEventSkipOwnProcess);

            // Both cloak events, not just the cloak: an uncloak is how the shell says Show Desktop
            // ended, and reconciling there is what drops the pin promptly rather than a second later.
            _cloakHook = Win32.SetWinEventHook(
                Win32.EventObjectCloaked,
                Win32.EventObjectUncloaked,
                0,
                callback,
                0,
                0,
                Win32.WinEventOutOfContext);

            if (_foregroundHook == 0 || _cloakHook == 0)
            {
                Log.Warn(Scope, "could not watch the shell; the widgets may hide when the desktop is shown");
            }
        }

        if (_backstop is null)
        {
            _backstop = dispatcher.CreateTimer();
            _backstop.IsRepeating = true;
            _backstop.Interval = IdleCadence;
            _backstop.Tick += (_, _) => OnBackstop();
            _backstop.Start();
        }
    }

    public static void Stop()
    {
        if (_foregroundHook != 0)
        {
            Win32.UnhookWinEvent(_foregroundHook);
            _foregroundHook = 0;
        }

        if (_cloakHook != 0)
        {
            Win32.UnhookWinEvent(_cloakHook);
            _cloakHook = 0;
        }

        _backstop?.Stop();
        _backstop = null;
    }

    /// <summary>Start managing a surface. Idempotent.</summary>
    public static void Track(nint hwnd)
    {
        if (hwnd == 0 || !Win32.IsWindow(hwnd))
        {
            return;
        }

        lock (Gate)
        {
            if (OriginalProcs.ContainsKey(hwnd))
            {
                return;
            }

            var replacement = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&SubclassProc;
            var original = Win32.SetWindowLongPtr(hwnd, Win32.GwlpWndProc, replacement);

            if (original == 0)
            {
                Log.Warn(Scope, $"could not subclass a widget surface (win32 {Marshal.GetLastWin32Error()}); it may float above other windows");
                return;
            }

            OriginalProcs[hwnd] = original;
        }

        PublishOwnWindows();
        Anchor(hwnd);
    }

    /// <summary>Stop managing a surface and restore its original window procedure.</summary>
    public static void Forget(nint hwnd)
    {
        lock (Gate)
        {
            if (!OriginalProcs.Remove(hwnd, out var original))
            {
                return;
            }

            if (Win32.IsWindow(hwnd))
            {
                Win32.SetWindowLongPtr(hwnd, Win32.GwlpWndProc, original);
            }
        }

        PublishOwnWindows();
    }

    /// <summary>
    /// Decide what the surfaces' z-order should be from the shell's current state, and make it so.
    /// Safe to call as often as you like; every path in and out of the pinned state goes through here.
    /// </summary>
    public static void Reconcile()
    {
        try
        {
            // The cloak invariant is unconditional: a cloaked surface is invisible whatever its
            // z-order, and nothing else in this app would ever notice.
            Uncloak();

            bool pinned;
            lock (Gate)
            {
                pinned = _pinnedTopmost;
            }

            // Cheap probe before the expensive one. A shell surface owning the foreground settles the
            // decision on its own and implies the desktop does not own it, so short-circuiting here
            // cannot change the outcome — it just keeps the two window sweeps from running for as
            // long as the Start menu stays open.
            var shellSurface = pinned && ShellSignals.ShellSurfaceIsForeground();
            var ownSurface = pinned && !shellSurface && ShellSignals.OwnSurfaceIsForeground();
            var showDesktop = !shellSurface && !ownSurface && ShellSignals.ShowDesktopIsActive();

            // [S-33e] A hold is only right while the user's windows are still away. One sweep, and
            // only while a hold is on the table (pinned, with a shell or own surface in front).
            var applicationOnScreen = (shellSurface || ownSurface) && ShellSignals.AnyOrdinaryAppVisible();

            Apply(ZOrderPolicy.Decide(showDesktop, pinned, shellSurface, ownSurface, applicationOnScreen));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "reconciling the desktop z-order threw", ex);
        }
    }

    private static void Apply(ZAction action)
    {
        switch (action)
        {
            case ZAction.PinTopmost:
                SetPinned(true);
                ForEachSurface(PinToTopOfTopmost);
                break;

            case ZAction.DropToAnchor:
                SetPinned(false);
                ForEachSurface(Anchor);
                break;

            case ZAction.SelfHeal:
                ForEachSurface(Anchor);
                break;

            case ZAction.HoldTopmost:
                // Deliberately nothing, not even a re-raise: re-raising would fight the Start menu
                // for the top of the topmost band, and the pin alone is what keeps the widgets above
                // the still-elevated desktop layer.
                break;

            default:
                break;
        }
    }

    private static void SetPinned(bool pinned)
    {
        lock (Gate)
        {
            if (_pinnedTopmost == pinned)
            {
                return;
            }

            _pinnedTopmost = pinned;
        }

        Log.Debug(Scope, pinned ? "desktop shown: lifting the widgets" : "desktop dismissed: returning the widgets to the desktop layer");
    }

    /// <summary>
    /// Park a surface at its resting place: just above the desktop window, below every application.
    /// <para>
    /// Anchored to the desktop window rather than to <c>HWND_BOTTOM</c> so the surface travels with
    /// it. Falls back to the bottom only when the desktop window cannot be found, which in practice
    /// means Explorer is not up yet.
    /// </para>
    /// </summary>
    private static void Anchor(nint hwnd)
    {
        if (!Win32.IsWindow(hwnd))
        {
            return;
        }

        // Clear the topmost bit first; SetWindowPos alone will not demote a window out of the
        // topmost band once it is in it.
        var ex = (uint)Win32.GetWindowLongPtr(hwnd, Win32.GwlExStyle);
        if ((ex & Win32.WsExTopmost) != 0)
        {
            Win32.SetWindowPos(
                hwnd,
                Win32.HwndNoTopmost,
                0, 0, 0, 0,
                Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoActivate | Win32.SwpNoOwnerZOrder);
        }

        var desktop = ShellSignals.DesktopWindow();
        var insertAfter = desktop != 0 ? desktop : Win32.HwndBottom;

        Win32.SetWindowPos(
            hwnd,
            insertAfter,
            0, 0, 0, 0,
            Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoActivate | Win32.SwpNoOwnerZOrder);
    }

    /// <summary>
    /// Force a surface to the top of the topmost band.
    /// <para>
    /// Two passes, because one is not enough: <c>SetWindowPos(HWND_TOPMOST)</c> frequently returns
    /// success without the <c>WS_EX_TOPMOST</c> bit actually taking, and a window that merely thinks
    /// it is topmost stays under the elevated desktop layer. When the bit is missing it is set
    /// directly and the position re-applied with <c>SWP_FRAMECHANGED</c> so the new style is honoured.
    /// </para>
    /// </summary>
    private static void PinToTopOfTopmost(nint hwnd)
    {
        if (!Win32.IsWindow(hwnd))
        {
            return;
        }

        const uint Flags = Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoActivate;

        Win32.SetWindowPos(hwnd, Win32.HwndTopmost, 0, 0, 0, 0, Flags);

        var ex = (uint)Win32.GetWindowLongPtr(hwnd, Win32.GwlExStyle);
        if ((ex & Win32.WsExTopmost) == 0)
        {
            Win32.SetWindowLongPtr(hwnd, Win32.GwlExStyle, (nint)(ex | Win32.WsExTopmost));
            Win32.SetWindowPos(hwnd, Win32.HwndTopmost, 0, 0, 0, 0, Flags | Win32.SwpFrameChanged);
        }
    }

    /// <summary>Undo any cloak applied to our own surfaces. One read per surface; a write only when broken.</summary>
    private static void Uncloak() => ForEachSurface(static hwnd =>
    {
        if (!Win32.IsWindow(hwnd) || !ShellSignals.IsCloaked(hwnd))
        {
            return;
        }

        uint clear = 0;
        var hr = Win32.DwmSetWindowAttribute(hwnd, Win32.DwmwaCloak, in clear, sizeof(uint));

        if (hr < 0)
        {
            Log.Debug(Scope, $"DwmSetWindowAttribute(CLOAK) returned 0x{hr:X8}");
            return;
        }

        // The cloak is sometimes paired with a hide; showing an already-visible window is a no-op.
        Win32.ShowWindow(hwnd, Win32.SwShowNoActivate);
    });

    private static void ForEachSurface(Action<nint> action)
    {
        nint[] surfaces;
        lock (Gate)
        {
            surfaces = [.. OriginalProcs.Keys];
        }

        foreach (var hwnd in surfaces)
        {
            action(hwnd);
        }
    }

    private static void PublishOwnWindows()
    {
        lock (Gate)
        {
            ShellSignals.SetOwnWindows([.. OriginalProcs.Keys]);
        }
    }

    private static void OnBackstop()
    {
        if (DateTimeOffset.UtcNow >= _busyUntil && _backstop is { } timer && timer.Interval != IdleCadence)
        {
            timer.Interval = IdleCadence;
        }

        Reconcile();
    }

    /// <summary>Something shell-shaped happened: reconcile now, and keep checking closely for a moment.</summary>
    private static void NoteShellActivity()
    {
        _busyUntil = DateTimeOffset.UtcNow + BusyWindow;

        if (_backstop is { } timer && timer.Interval != BusyCadence)
        {
            timer.Interval = BusyCadence;
        }

        Reconcile();
    }

    [UnmanagedCallersOnly]
    private static void OnWinEvent(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        // A system hook callback runs on Windows' own call stack: letting a managed exception unwind
        // out of here is undefined behaviour, and a hook whose callback misbehaves is silently
        // removed for the rest of the session.
        try
        {
            if (hwnd == 0)
            {
                return;
            }

            // Cloak events fire for child objects too, which say nothing about a window being hidden.
            if (eventId != Win32.EventSystemForeground && (objectId != Win32.ObjIdWindow || childId != Win32.ChildIdSelf))
            {
                return;
            }

            NoteShellActivity();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the shell hook threw", ex);
        }
    }

    [UnmanagedCallersOnly]
    private static nint SubclassProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        nint original;

        // A window procedure runs on Windows' own call stack: a managed exception unwinding out of
        // here is undefined behaviour, so every path is guarded and falls through to the original.
        try
        {
            bool pinned;

            lock (Gate)
            {
                if (!OriginalProcs.TryGetValue(hwnd, out original))
                {
                    return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
                }

                pinned = _pinnedTopmost;
            }

            // While pinned the surface is deliberately above everything, so rewriting the z-order
            // here would undo the pin on the next message. The rest of the time every requested
            // z-order becomes the anchor, which is what stops a click on an activatable surface from
            // raising it over the user's windows.
            if (!pinned && msg == WmWindowPosChanging && lParam != 0)
            {
                var position = (WindowPos*)lParam;
                var desktop = ShellSignals.DesktopWindow();

                position->HwndInsertAfter = desktop != 0 ? desktop : Win32.HwndBottom;
                position->Flags &= ~Win32.SwpNoZOrder;
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"window procedure threw handling message 0x{msg:X}", ex);
            return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        return Win32.CallWindowProc(original, hwnd, msg, wParam, lParam);
    }
}
