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
/// pumping messages — so a busy UI thread can never stall the system's mouse (the timeout trouble of
/// the original's always-on hooks) — and the callback only reads the window under the pointer and
/// queues one signal to the UI thread. The foreground event, on the same thread, covers leaving by
/// keyboard (Alt+Tab, the Windows key).
/// </para>
/// </summary>
internal sealed unsafe partial class OutsideClickWatcher : IDisposable
{
    private const string Scope = "outside-click";

    private const int WhMouseLl = 14;
    private const uint WmQuit = 0x0012;
    private const uint WmArm = 0x8001; // WM_APP + 1
    private const uint WmDisarm = 0x8002;
    private const uint PmNoRemove = 0x0000;
    private const uint GaRoot = 2;
    private const uint EventSystemForeground = 0x0003;

    /// <summary>The live watcher, for the static native callbacks. One per process.</summary>
    private static OutsideClickWatcher? _current;

    private readonly DispatcherQueue _ui;
    private readonly Action _outside;
    private readonly uint _ownProcess = (uint)Environment.ProcessId;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();
    private uint _threadId;

    /// <summary>1 while a signal is queued to the UI thread: a burst of presses is one signal.</summary>
    private int _queued;

    /// <summary>The foreground window's class before the latest change (hook thread only).</summary>
    private string? _foregroundClass;

    private bool _disposed;

    /// <param name="ui">The UI thread, where <paramref name="outside"/> runs.</param>
    /// <param name="outside">A press outside, or another app took the foreground.</param>
    public OutsideClickWatcher(DispatcherQueue ui, Action outside)
    {
        _ui = ui;
        _outside = outside;
        _current = this;
        _thread = new Thread(Run) { IsBackground = true, Name = "Themia outside clicks" };
        _thread.Start();

        // A handshake, not a guess: the thread signals as soon as it has a message queue to post to.
        _started.Wait();
    }

    /// <summary>Start listening (idempotent).</summary>
    public void Arm() => _ = Post(WmArm);

    /// <summary>Stop listening (idempotent): the hooks come off, nothing runs per mouse event.</summary>
    public void Disarm() => _ = Post(WmDisarm);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Joined only when the quit reached the thread: a thread that cannot be told to stop is left to
        // die with the process (it is a background thread) rather than hanging the exit.
        if (Post(WmQuit))
        {
            _thread.Join();
        }

        _started.Dispose();

        if (ReferenceEquals(_current, this))
        {
            _current = null;
        }
    }

    private bool Post(uint message)
    {
        if (_threadId != 0 && PostThreadMessage(_threadId, message, 0, 0))
        {
            return true;
        }

        Log.Warn(Scope, $"could not reach the watcher thread (win32 {Marshal.GetLastWin32Error()})");
        return false;
    }

    /// <summary>The watcher thread: install and remove the hooks on request, pump until quit.</summary>
    private void Run()
    {
        using var hooks = new Hooks();

        try
        {
            // Any message call creates the thread's queue; PostThreadMessage needs it to exist.
            PeekMessage(out _, 0, 0, 0, PmNoRemove);
            _threadId = GetCurrentThreadId();
            _started.Set();

            while (true)
            {
                var got = GetMessage(out var msg, 0, 0, 0);
                if (got == 0)
                {
                    break; // WM_QUIT
                }

                if (got < 0)
                {
                    Log.Error(Scope, $"GetMessage failed (win32 {Marshal.GetLastWin32Error()}); clicks outside go unheard");
                    break;
                }

                switch (msg.Message)
                {
                    case WmArm:
                        _foregroundClass = ClassOf(Win32.GetForegroundWindow());
                        hooks.Install();
                        break;
                    case WmDisarm:
                        hooks.Remove();
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the watcher thread threw; clicks outside go unheard", ex);
        }
        finally
        {
            // The constructor must never wait forever, whatever happened above.
            _started.Set();
        }
    }

    /// <summary>Queue one signal to the UI thread; a press burst coalesces into it.</summary>
    private void Signal()
    {
        if (Interlocked.Exchange(ref _queued, 1) != 0)
        {
            return;
        }

        var queued = _ui.TryEnqueue(() =>
        {
            Volatile.Write(ref _queued, 0);
            try
            {
                _outside();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "handling a click outside threw", ex);
            }
        });

        if (!queued)
        {
            Volatile.Write(ref _queued, 0);
            Log.Warn(Scope, "could not queue a click outside to the UI thread");
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

        return CallNextHookEx(0, code, wParam, lParam);
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

    /// <summary>
    /// The two hooks, owned by the watcher thread: both are removed on that thread (UnhookWinEvent must
    /// run on the thread that installed it), on disarm and on every way out of the loop.
    /// </summary>
    private sealed class Hooks : IDisposable
    {
        private nint _mouse;
        private nint _foreground;

        public void Install()
        {
            if (_mouse == 0)
            {
                var proc = (nint)(delegate* unmanaged<int, nint, nint, nint>)&OnMouseHook;
                _mouse = SetWindowsHookEx(WhMouseLl, proc, Win32.GetModuleHandle(null), 0);
                if (_mouse == 0)
                {
                    Log.Warn(Scope, $"could not hook the mouse (win32 {Marshal.GetLastWin32Error()}); a click outside will not close menus");
                }
            }

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

        public void Remove()
        {
            if (_mouse != 0)
            {
                if (!UnhookWindowsHookEx(_mouse))
                {
                    Log.Warn(Scope, $"could not unhook the mouse (win32 {Marshal.GetLastWin32Error()})");
                }

                _mouse = 0;
            }

            if (_foreground != 0)
            {
                if (!Win32.UnhookWinEvent(_foreground))
                {
                    Log.Warn(Scope, "could not remove the foreground hook");
                }

                _foreground = 0;
            }
        }

        public void Dispose() => Remove();
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

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public Win32.Point Pt;
        public uint Private;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static partial nint SetWindowsHookEx(int idHook, nint lpfn, nint hmod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("user32.dll")]
    private static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial nint WindowFromPoint(Win32.Point point);

    [LibraryImport("user32.dll")]
    private static partial nint GetAncestor(nint hwnd, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    private static partial int GetMessage(out Msg msg, nint hwnd, uint min, uint max);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out Msg msg, nint hwnd, uint min, uint max, uint remove);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessage(uint threadId, uint msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();
}
