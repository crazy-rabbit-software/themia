using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>
/// [S-815] [S-1594] The one thread a low-level hook lives on: it pumps messages and nothing else, so a busy UI thread
/// can never stall the system's mouse or keyboard; its hooks go on when it is armed and off when it is disarmed, both
/// on this thread (a hook is removed on the thread that installed it), and on every way out of its loop. A watcher
/// (<see cref="OutsideClickWatcher"/>, <see cref="PlacementKeyWatcher"/>) says what arming installs and what disarming
/// removes; its native callbacks decide on the hook thread and hand the UI thread one signal through
/// <see cref="Enqueue"/>.
/// </summary>
internal abstract partial class LowLevelHookThread : IDisposable
{
    private const uint WmQuit = 0x0012;
    private const uint WmArm = 0x8001; // WM_APP + 1
    private const uint WmDisarm = 0x8002;
    private const uint PmNoRemove = 0x0000;

    private readonly string _scope;
    private readonly string _lost;
    private readonly DispatcherQueue _ui;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();
    private uint _threadId;
    private bool _disposed;

    /// <param name="scope">The log's scope.</param>
    /// <param name="lost">What goes unheard when the thread stops ("clicks outside go unheard").</param>
    /// <param name="name">The thread's name.</param>
    /// <param name="ui">The UI thread, where <see cref="Enqueue"/> runs what it is handed.</param>
    protected LowLevelHookThread(string scope, string lost, string name, DispatcherQueue ui)
    {
        _scope = scope;
        _lost = lost;
        _ui = ui;
        _thread = new Thread(Run) { IsBackground = true, Name = name };
    }

    /// <summary>Start listening (idempotent).</summary>
    public void Arm() => _ = Post(WmArm);

    /// <summary>Stop listening (idempotent).</summary>
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
        Disposed();
    }

    /// <summary>Start the thread; the end of the watcher's constructor, once every field it reads is set.</summary>
    protected void Start()
    {
        _thread.Start();

        // A handshake, not a guess: the thread signals as soon as it has a message queue to post to.
        _started.Wait();
    }

    /// <summary>Hook thread: an arm arrived. Install the hooks (idempotent).</summary>
    protected abstract void Armed();

    /// <summary>Hook thread: a disarm arrived. Remove the hooks, or hold them until a pending key is up.</summary>
    protected abstract void Disarmed();

    /// <summary>Hook thread, on every way out of the loop: every hook comes off.</summary>
    protected abstract void Stopped();

    /// <summary>After the thread has stopped: let go of what the static callbacks reach.</summary>
    protected abstract void Disposed();

    /// <summary>Hook thread: ask for a disarm from the queue, rather than inside the hook's own call.</summary>
    protected void DisarmLater() => _ = Post(WmDisarm);

    /// <summary>Queue <paramref name="action"/> to the UI thread, caught and logged there; false when it could not be queued.</summary>
    protected bool Enqueue(Action action, string what)
    {
        var queued = _ui.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(_scope, $"{what} threw", ex);
            }
        });

        if (!queued)
        {
            Log.Warn(_scope, $"could not queue {what} to the UI thread");
        }

        return queued;
    }

    private bool Post(uint message)
    {
        if (_threadId != 0 && PostThreadMessage(_threadId, message, 0, 0))
        {
            return true;
        }

        Log.Warn(_scope, $"could not reach the watcher thread (win32 {Marshal.GetLastWin32Error()})");
        return false;
    }

    /// <summary>The hook thread: install and remove the hooks on request, pump until quit.</summary>
    private void Run()
    {
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
                    Log.Error(_scope, $"GetMessage failed (win32 {Marshal.GetLastWin32Error()}); {_lost}");
                    break;
                }

                switch (msg.Message)
                {
                    case WmArm:
                        Armed();
                        break;
                    case WmDisarm:
                        Disarmed();
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(_scope, $"the watcher thread threw; {_lost}", ex);
        }
        finally
        {
            try
            {
                Stopped();
            }
            catch (Exception ex)
            {
                Log.Error(_scope, "removing the hooks threw", ex);
            }

            // The constructor must never wait forever, whatever happened above.
            _started.Set();
        }
    }

    // ---- interop ----------------------------------------------------------------------------------

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

/// <summary>
/// [S-815] [S-1594] A low-level hook (<c>SetWindowsHookEx</c>): removed when disposed, on the thread that installed it
/// (<see cref="LowLevelHookThread"/>), and by its finalizer should a path ever miss it. Never a raw handle.
/// </summary>
internal sealed partial class HookHandle : SafeHandle
{
    public HookHandle()
        : base(0, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == 0;

    /// <summary>
    /// Hook <paramref name="kind"/> (<c>WH_MOUSE_LL</c>, <c>WH_KEYBOARD_LL</c>) to <paramref name="proc"/>; null when
    /// Windows refused, the reason logged under <paramref name="scope"/> with <paramref name="lost"/>.
    /// </summary>
    public static unsafe HookHandle? Install(int kind, delegate* unmanaged<int, nint, nint, nint> proc, string scope, string lost)
    {
        var hook = SetWindowsHookEx(kind, (nint)proc, Win32.GetModuleHandle(null), 0);
        if (!hook.IsInvalid)
        {
            return hook;
        }

        Log.Warn(scope, $"could not set the hook (win32 {Marshal.GetLastWin32Error()}); {lost}");
        hook.Dispose();
        return null;
    }

    /// <summary>Pass the event on to the next hook: what every hook callback returns unless it swallows the event.</summary>
    public static nint Next(int code, nint wParam, nint lParam) => CallNextHookEx(0, code, wParam, lParam);

    protected override bool ReleaseHandle()
    {
        if (UnhookWindowsHookEx(handle))
        {
            return true;
        }

        Log.Warn("hooks", $"could not remove a hook (win32 {Marshal.GetLastWin32Error()})");
        return false;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static partial HookHandle SetWindowsHookEx(int idHook, nint lpfn, nint hmod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("user32.dll")]
    private static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);
}
