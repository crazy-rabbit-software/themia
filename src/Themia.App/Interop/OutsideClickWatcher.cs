using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Interop;

/// <summary>
/// [S-815] Hears a press anywhere outside Themia's windows, and another app taking the foreground,
/// while an interaction session is open — the "click outside" a <c>WS_EX_NOACTIVATE</c> surface never
/// gets by itself.
/// <para>
/// Why a low-level mouse hook. The surface never takes activation outside a keyboard lease, so its
/// popups get no deactivation and WinUI's light dismiss never sees the outside press; the foreground
/// event cannot stand in for it either, because a press on the window that already has the foreground
/// changes nothing — and on a desktop-widget app that window is the desktop itself, the commonest case.
/// The hook is the one signal that sees every press: the desktop, the taskbar, another app, another
/// display. It is kept cheap and harmless: installed only while a session is open (a press on a
/// widget arms it, the first press outside disarms it), on a thread of its own whose only work is
/// pumping messages (<see cref="LowLevelHookThread"/>) — so a busy UI thread can never stall the system's mouse (the
/// timeout trouble of the original's always-on hooks) — and the callback only reads the window under the pointer and
/// queues one signal to the UI thread. The foreground event, on the same thread, covers leaving by
/// keyboard (Alt+Tab, the Windows key).
/// </para>
/// </summary>
internal sealed unsafe partial class OutsideClickWatcher : LowLevelHookThread
{
    private const string Scope = "outside-click";
    private const string Lost = "clicks outside go unheard";

    private const int WhMouseLl = 14;
    private const uint GaRoot = 2;
    private const uint EventSystemForeground = 0x0003;

    /// <summary>The live watcher, for the static native callbacks. One per process.</summary>
    private static OutsideClickWatcher? _current;

    private readonly Action _outside;
    private readonly uint _ownProcess = (uint)Environment.ProcessId;

    /// <summary>1 while a signal is queued to the UI thread: a burst of presses is one signal.</summary>
    private int _queued;

    /// <summary>The foreground window's class before the latest change (hook thread only).</summary>
    private string? _foregroundClass;

    /// <summary>Hook thread only: the mouse hook while armed.</summary>
    private HookHandle? _mouse;

    /// <summary>
    /// Hook thread only: the foreground event's hook while armed. A raw handle on purpose: <c>UnhookWinEvent</c> must run
    /// on the thread that set it, which a SafeHandle's finalizer cannot promise; <see cref="Stopped"/> removes it on every way out.
    /// </summary>
    private nint _foreground;

    /// <param name="ui">The UI thread, where <paramref name="outside"/> runs.</param>
    /// <param name="outside">A press outside, or another app took the foreground.</param>
    public OutsideClickWatcher(DispatcherQueue ui, Action outside)
        : base(Scope, Lost, "Themia outside clicks", ui)
    {
        _outside = outside;
        _current = this;
        Start();
    }

    protected override void Armed()
    {
        _foregroundClass = ClassOf(Win32.GetForegroundWindow());
        _mouse ??= HookHandle.Install(WhMouseLl, &OnMouseHook, Scope, "a click outside will not close menus");

        if (_foreground == 0)
        {
            var proc = (nint)(delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void>)&OnWinEvent;
            _foreground = Win32.SetWinEventHook(
                EventSystemForeground,
                EventSystemForeground,
                0,
                proc,
                0,
                0,
                Win32.WinEventOutOfContext | Win32.WinEventSkipOwnProcess);
            if (_foreground == 0)
            {
                Log.Warn(Scope, "could not watch the foreground; switching apps by keyboard will not close menus");
            }
        }
    }

    protected override void Disarmed() => Stopped();

    protected override void Stopped()
    {
        _mouse?.Dispose();
        _mouse = null;

        if (_foreground != 0)
        {
            if (!Win32.UnhookWinEvent(_foreground))
            {
                Log.Warn(Scope, "could not remove the foreground hook");
            }

            _foreground = 0;
        }
    }

    protected override void Disposed()
    {
        if (ReferenceEquals(_current, this))
        {
            _current = null;
        }
    }

    /// <summary>Queue one signal to the UI thread; a press burst coalesces into it.</summary>
    private void Signal()
    {
        if (Interlocked.Exchange(ref _queued, 1) != 0)
        {
            return;
        }

        if (!Enqueue(
            () =>
            {
                Volatile.Write(ref _queued, 0);
                _outside();
            },
            "a click outside"))
        {
            Volatile.Write(ref _queued, 0);
        }
    }

    private void OnPress(Win32.Point at)
    {
        var hwnd = WindowFromPoint(at);
        uint process = 0;
        if (hwnd != 0)
        {
            var root = GetAncestor(hwnd, GaRoot);
            Win32.GetWindowThreadProcessId(root != 0 ? root : hwnd, out process);
        }

        if (SessionInput.IsOutside(process, _ownProcess))
        {
            Signal();
        }
    }

    private void OnForeground(nint hwnd)
    {
        var previous = _foregroundClass;
        _foregroundClass = ClassOf(hwnd);

        if (SessionInput.ForegroundEndsSession(previous))
        {
            Signal();
        }
    }

    private static string? ClassOf(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        var buffer = stackalloc char[256];
        var length = Win32.GetClassName(hwnd, buffer, 256);
        return length > 0 ? new string(buffer, 0, length) : null;
    }

    // ---- native callbacks: catch, log, pass on ----------------------------------------------------

    [UnmanagedCallersOnly]
    private static nint OnMouseHook(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= 0 && SessionInput.IsButtonDown((uint)wParam) && _current is { } watcher)
            {
                watcher.OnPress(((MsllHookStruct*)lParam)->Pt);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the mouse hook threw", ex);
        }

        return HookHandle.Next(code, wParam, lParam);
    }

    [UnmanagedCallersOnly]
    private static void OnWinEvent(nint hook, uint kind, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            if (kind == EventSystemForeground && _current is { } watcher)
            {
                watcher.OnForeground(hwnd);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the foreground hook threw", ex);
        }
    }

    // ---- interop ----------------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public Win32.Point Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [LibraryImport("user32.dll")]
    private static partial nint WindowFromPoint(Win32.Point point);

    [LibraryImport("user32.dll")]
    private static partial nint GetAncestor(nint hwnd, uint flags);
}
