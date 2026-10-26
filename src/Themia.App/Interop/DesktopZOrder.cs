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
/// and each <c>WM_WINDOWPOSCHANGING</c> is rewritten back to the anchor.
/// </para>
/// <para>
/// <b>Show Desktop.</b> Win+D raises the desktop window <i>past</i> normal z-order, above everything
/// pinned below it, so the widgets vanish at the one moment the user asked to see their desktop.
/// While the desktop is genuinely fronted there is nothing on screen for the widgets to cover, so
/// lifting them for the duration does not break the "never above other windows" rule — it is the only
/// way to honour it and still be visible. <see cref="ZOrderPolicy"/> owns that decision.
/// </para>
/// <para>
/// <b>Cloaking.</b> Show Desktop on some builds and a fullscreen exclusive application hide windows by
/// asking DWM to <i>cloak</i> them, which goes through neither <c>SC_MINIMIZE</c> nor
/// <c>WM_SHOWWINDOW</c>, so a window procedure never sees it. A cloaked window is still visible to
/// <c>IsWindowVisible</c>, still not iconic, still at whatever z-order you last put it at — and simply
/// never composited. It is undone by clearing <c>DWMWA_CLOAK</c> on every check.
/// </para>
/// <para>
/// <b>Event-driven, no timer</b> ([S-34a]). Everything a check decides from changes only through a
/// window-manager operation that announces itself when it is done: the foreground, a window minimised
/// or restored, shown or cloaked, and any change to the top-level z-order (the desktop window's
/// <c>EVENT_OBJECT_REORDER</c>, which is also how a desktop raised past the lifted widgets is heard).
/// Out-of-context WinEvents are queued to this thread and arrive in order; each one only queues one
/// coalesced check, and the check reads everything fresh, so a burst costs one check and an event that
/// lands mid-transition is followed by the one that ends it. The host adds what is not a WinEvent:
/// a display change, an Explorer restart, an unlock and a resume (<see cref="Refresh"/>).
/// </para>
/// </summary>
internal static unsafe class DesktopZOrder
{
    private const string Scope = "z-order";
    private const uint WmWindowPosChanging = 0x0046;

    /// <summary>
    /// How far a z-order walk goes before giving up. The z-order can change under a walk, and a walk
    /// that follows a window being moved can revisit the same windows; a real stack is a few hundred.
    /// </summary>
    private const int WalkLimit = 4096;

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

    /// <summary>[S-34d] The bound on repairs between two user actions. UI thread only.</summary>
    private static readonly ZOrderRepairs Repairs = new();

    /// <summary>Reused by the z-order walks, which only ever run on the UI thread.</summary>
    private static readonly List<StackWindow> Walk = [];

    private static DispatcherQueue? _dispatcher;
    private static nint _foregroundHook;
    private static nint _minimizeHook;
    private static nint _reorderHook;
    private static nint _cloakHook;

    /// <summary>
    /// [S-34a] Installed only while the widgets are lifted: a window shown without taking the
    /// foreground is the one way an application comes back on screen that no other hook hears, and
    /// outside the lift nothing depends on it. <c>EVENT_OBJECT_SHOW</c> fires for carets and cursors
    /// too, so it is not worth hearing the rest of the time.
    /// </summary>
    private static nint _showHook;

    /// <summary>Between <see cref="Start"/> and <see cref="Stop"/>: game mode stops the controller with the surfaces it hides ([S-928]).</summary>
    private static bool _started;

    /// <summary>Whether the surfaces are held above the elevated desktop layer. UI thread only, like every caller.</summary>
    private static bool _pinnedTopmost;

    /// <summary>A check is already queued, so the next event need not queue another.</summary>
    private static bool _checkQueued;

    /// <summary>
    /// Start watching the shell. Idempotent. The hooks are installed out-of-context so their
    /// callbacks arrive on this thread's message queue rather than inside another process.
    /// </summary>
    public static void Start(DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
        _started = true;

        // Own windows are skipped where our own writes would only echo back: our SetWindowPos calls
        // reorder the stack, and the surfaces never minimise, show or take the foreground from Windows.
        Hook(ref _foregroundHook, Win32.EventSystemForeground, Win32.EventSystemForeground, skipOwnProcess: true, "foreground changes");
        Hook(ref _minimizeHook, Win32.EventSystemMinimizeStart, Win32.EventSystemMinimizeEnd, skipOwnProcess: true, "windows being minimised");
        Hook(ref _reorderHook, Win32.EventObjectReorder, Win32.EventObjectReorder, skipOwnProcess: true, "z-order changes");

        // Not skipping our own process: a cloak of our own surface is exactly the event that matters,
        // and which process Windows charges it to is not documented.
        Hook(ref _cloakHook, Win32.EventObjectCloaked, Win32.EventObjectUncloaked, skipOwnProcess: false, "cloaking");

        if (_pinnedTopmost)
        {
            Hook(ref _showHook, Win32.EventObjectShow, Win32.EventObjectShow, skipOwnProcess: true, "windows being shown");
        }

        // Whatever changed while the hooks were out is caught up on here.
        Refresh();
    }

    public static void Stop()
    {
        _started = false;
        Unhook(ref _foregroundHook);
        Unhook(ref _minimizeHook);
        Unhook(ref _reorderHook);
        Unhook(ref _cloakHook);
        Unhook(ref _showHook);
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
    /// [S-34a] Something outside the WinEvents may have moved the ground: a display change, an
    /// Explorer restart (<c>TaskbarCreated</c>), the session unlocked, the PC resumed. Queues one
    /// check with a fresh repair allowance.
    /// </summary>
    public static void Refresh()
    {
        Repairs.Refill();
        QueueCheck();
    }

    /// <summary>
    /// Decide what the surfaces' z-order should be from the shell's current state, and make it so,
    /// now. For the host's own changes (a surface shown, its activation style changed); safe to call
    /// as often as you like, every path in and out of the pinned state goes through here.
    /// </summary>
    public static void Reconcile()
    {
        Repairs.Refill();
        Check();
    }

    private static void QueueCheck()
    {
        if (!_started || _checkQueued || _dispatcher is not { } dispatcher)
        {
            return;
        }

        _checkQueued = true;
        if (!dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, RunQueuedCheck))
        {
            _checkQueued = false;
            Log.Error(Scope, "could not queue a z-order check; the widgets keep their place until the next change");
        }
    }

    private static void RunQueuedCheck()
    {
        _checkQueued = false;

        // Queued before a Stop: a suspended desktop has nothing to look after ([S-928]).
        if (_started)
        {
            Check();
        }
    }

    private static void Check()
    {
        try
        {
            // The cloak invariant is unconditional: a cloaked surface is invisible whatever its
            // z-order, and nothing else in this app would ever notice.
            Uncloak();

            var pinned = _pinnedTopmost;

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
                if (SetPinned(true))
                {
                    // The lift itself: every surface to the top of the topmost band.
                    ForEachSurface(PinToTopOfTopmost);
                }
                else
                {
                    // [S-34b] Already lifted: only a surface the desktop layer has risen past again.
                    ForEachSurface(KeepPinned);
                }

                break;

            case ZAction.DropToAnchor:
                SetPinned(false);
                ForEachSurface(Anchor);
                break;

            case ZAction.SelfHeal:
                ForEachSurface(KeepAnchored);
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

    /// <summary>Set the lift; true when it changed. The show hook lives exactly as long as the lift.</summary>
    private static bool SetPinned(bool pinned)
    {
        if (_pinnedTopmost == pinned)
        {
            return false;
        }

        _pinnedTopmost = pinned;

        if (pinned)
        {
            if (_started)
            {
                // A stopped controller installs it on Start instead.
                Hook(ref _showHook, Win32.EventObjectShow, Win32.EventObjectShow, skipOwnProcess: true, "windows being shown");
            }
        }
        else
        {
            Unhook(ref _showHook);
        }

        Log.Debug(Scope, pinned ? "desktop shown: lifting the widgets" : "desktop dismissed: returning the widgets to the desktop layer");
        return true;
    }

    /// <summary>[S-34c] Re-anchor a surface only when something on screen has ended up below it.</summary>
    private static void KeepAnchored(nint hwnd)
    {
        if (!Win32.IsWindow(hwnd) || ZOrderPolicy.AnchorHolds(IsTopmost(hwnd), Stack(hwnd, Win32.GwHwndNext, StackWindow.Other)))
        {
            return;
        }

        if (MayRepair())
        {
            Log.Debug(Scope, "a window ended up below a widget surface; putting the surface back on the desktop layer");
            Anchor(hwnd);
        }
    }

    /// <summary>[S-34b] Re-raise a lifted surface only when the desktop layer is above it again.</summary>
    private static void KeepPinned(nint hwnd)
    {
        if (!Win32.IsWindow(hwnd) || ZOrderPolicy.PinHolds(IsTopmost(hwnd), Stack(hwnd, Win32.GwHwndPrev, StackWindow.DesktopLayer)))
        {
            return;
        }

        if (MayRepair())
        {
            Log.Debug(Scope, "the desktop rose above a lifted widget surface; lifting it again");
            PinToTopOfTopmost(hwnd);
        }
    }

    /// <summary>[S-34d] One repair from the allowance; the first refusal is logged.</summary>
    private static bool MayRepair()
    {
        switch (Repairs.TrySpend())
        {
            case RepairGrant.Granted:
                return true;

            case RepairGrant.Refused:
                Log.Warn(Scope, $"{ZOrderRepairs.Allowance} z-order repairs since the last user action were all undone; " +
                    "another program may be fighting for the same place, so standing down until the next foreground change");
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// The windows on one side of a surface, nearest first, as the stack rules see them: below it down
    /// to the desktop window (or the bottom), or above it up to the top. Stops at the first window of
    /// <paramref name="decisive"/>, which settles the answer on its own.
    /// </summary>
    private static ReadOnlySpan<StackWindow> Stack(nint hwnd, uint direction, StackWindow decisive)
    {
        Walk.Clear();
        var desktop = ShellSignals.DesktopWindow();
        var next = Win32.GetWindow(hwnd, direction);

        for (var hops = 0; next != 0 && hops < WalkLimit; hops++)
        {
            if (direction == Win32.GwHwndNext && next == desktop)
            {
                break;
            }

            var window = Describe(next);
            Walk.Add(window);
            if (window == decisive)
            {
                break;
            }

            next = Win32.GetWindow(next, direction);
        }

        return CollectionsMarshal.AsSpan(Walk);
    }

    private static StackWindow Describe(nint hwnd)
    {
        lock (Gate)
        {
            if (OriginalProcs.ContainsKey(hwnd))
            {
                return StackWindow.Own;
            }
        }

        if (!Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd) || ShellSignals.IsCloaked(hwnd))
        {
            return StackWindow.Hidden;
        }

        return ShellSignals.IsDesktopLayerWindow(hwnd) ? StackWindow.DesktopLayer : StackWindow.Other;
    }

    private static bool IsTopmost(nint hwnd)
        => ((uint)Win32.GetWindowLongPtr(hwnd, Win32.GwlExStyle) & Win32.WsExTopmost) != 0;

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
        if (IsTopmost(hwnd))
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
        if (!Win32.IsWindow(hwnd)
            || Win32.DwmGetWindowAttribute(hwnd, Win32.DwmwaCloaked, out var cloaked, sizeof(uint)) < 0
            || cloaked == 0
            || !MayRepair())
        {
            return;
        }

        // The reason says who did it (1 the app, 2 the shell, 4 an owner), which is what a live pass
        // needs to know when a surface keeps disappearing.
        Log.Debug(Scope, $"a widget surface was cloaked (DWM_CLOAKED 0x{cloaked:X}); uncloaking it");

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

    private static void Hook(ref nint handle, uint eventMin, uint eventMax, bool skipOwnProcess, string what)
    {
        if (handle != 0)
        {
            return;
        }

        var callback = (nint)(delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void>)&OnWinEvent;
        var flags = Win32.WinEventOutOfContext | (skipOwnProcess ? Win32.WinEventSkipOwnProcess : 0);

        handle = Win32.SetWinEventHook(eventMin, eventMax, 0, callback, 0, 0, flags);
        if (handle == 0)
        {
            Log.Warn(Scope, $"could not watch {what}; the widgets may be out of place until the next foreground change");
        }
    }

    private static void Unhook(ref nint handle)
    {
        if (handle == 0)
        {
            return;
        }

        if (!Win32.UnhookWinEvent(handle))
        {
            Log.Debug(Scope, "UnhookWinEvent failed; the hook is gone with this thread anyway");
        }

        handle = 0;
    }

    private static ShellEvent? KindOf(uint eventId) => eventId switch
    {
        Win32.EventSystemForeground => ShellEvent.Foreground,
        Win32.EventSystemMinimizeStart or Win32.EventSystemMinimizeEnd => ShellEvent.Minimize,
        Win32.EventObjectReorder => ShellEvent.Reorder,
        Win32.EventObjectShow => ShellEvent.Show,
        Win32.EventObjectCloaked or Win32.EventObjectUncloaked => ShellEvent.Cloak,
        _ => null,
    };

    private static EventSource SourceOf(nint hwnd)
    {
        if (hwnd == 0)
        {
            return EventSource.Other;
        }

        var desktop = Win32.GetDesktopWindow();
        if (hwnd == desktop)
        {
            return EventSource.DesktopWindow;
        }

        return Win32.GetAncestor(hwnd, Win32.GaParent) == desktop ? EventSource.TopLevelWindow : EventSource.Other;
    }

    [UnmanagedCallersOnly]
    private static void OnWinEvent(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        // A system hook callback runs on Windows' own call stack: letting a managed exception unwind
        // out of here is undefined behaviour.
        try
        {
            if (KindOf(eventId) is not { } kind)
            {
                return;
            }

            var wholeWindow = objectId == Win32.ObjIdWindow && childId == Win32.ChildIdSelf;
            switch (ZOrderEvents.Classify(kind, wholeWindow, SourceOf(hwnd)))
            {
                case ZTrigger.CheckAfresh:
                    Repairs.Refill();
                    QueueCheck();
                    break;

                case ZTrigger.Check:
                    QueueCheck();
                    break;

                default:
                    break;
            }
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
            lock (Gate)
            {
                if (!OriginalProcs.TryGetValue(hwnd, out original))
                {
                    return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
                }
            }

            // While pinned the surface is deliberately above everything, so rewriting the z-order
            // here would undo the pin on the next message. The rest of the time every requested
            // z-order becomes the anchor, which is what stops a click on an activatable surface from
            // raising it over the user's windows.
            if (!_pinnedTopmost && msg == WmWindowPosChanging && lParam != 0)
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
