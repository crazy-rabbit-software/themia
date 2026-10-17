using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Interop;

/// <summary>
/// [S-1594] Hears Escape while a placement session is open, wherever the keyboard is, and cancels
/// the placement on the UI thread.
/// <para>
/// Why a low-level keyboard hook. The surface only hears a key as a XAML <c>KeyDown</c>, which needs
/// it to be the foreground window with the focus inside it — and a placement mostly starts when it
/// is neither: from a card menu or the add menu, whose closing leaves the focus on nothing (the
/// toolbar button it returns to is hidden while placing), from a surface that never took the
/// foreground, or with the pointer, and the ghost, on another display. The hook is the one signal that
/// sees the key in every one of those. It follows <see cref="OutsideClickWatcher"/>'s pattern: on only
/// while a session is open, on a thread of its own whose only work is pumping messages (a busy UI
/// thread can never stall the system's keyboard), and the callback only decides through Core
/// <see cref="PlacementKeyFilter"/> and queues one signal to the UI thread. The cancelling press is
/// swallowed so it does not also reach whatever has the keyboard.
/// </para>
/// </summary>
internal sealed unsafe partial class PlacementKeyWatcher : IDisposable
{
    private const string Scope = "placement-keys";

    private const int WhKeyboardLl = 13;
    private const uint WmQuit = 0x0012;
    private const uint WmArm = 0x8001; // WM_APP + 1
    private const uint WmDisarm = 0x8002;
    private const uint PmNoRemove = 0x0000;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    /// <summary>The live watcher, for the static native callback. One per process.</summary>
    private static PlacementKeyWatcher? _current;

    private readonly DispatcherQueue _ui;
    private readonly Action _cancel;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();

    /// <summary>Hook thread only: fed every key event in order.</summary>
    private readonly PlacementKeyFilter _filter = new();

    private uint _threadId;

    /// <summary>Hook thread only: the hook is wanted off, once the cancelling Escape is up ([S-1594]).</summary>
    private bool _disarmPending;

    private bool _disposed;

    /// <param name="ui">The UI thread, where <paramref name="cancel"/> runs.</param>
    /// <param name="cancel">Escape was pressed while placing.</param>
    public PlacementKeyWatcher(DispatcherQueue ui, Action cancel)
    {
        _ui = ui;
        _cancel = cancel;
        _current = this;
        _thread = new Thread(Run) { IsBackground = true, Name = "Themia placement keys" };
        _thread.Start();

        // A handshake, not a guess: the thread signals as soon as it has a message queue to post to.
        _started.Wait();
    }

    /// <summary>Start listening (idempotent).</summary>
    public void Arm() => _ = Post(WmArm);

    /// <summary>Stop listening (idempotent); a held cancelling Escape keeps it on until it is released.</summary>
    public void Disarm() => _ = Post(WmDisarm);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

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

    private void Run()
    {
        using var hook = new Hook();

        try
        {
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
                    Log.Error(Scope, $"GetMessage failed (win32 {Marshal.GetLastWin32Error()}); Escape no longer cancels a placement");
                    break;
                }

                switch (msg.Message)
                {
                    case WmArm:
                        _disarmPending = false;
                        if (!hook.Installed)
                        {
                            _filter.Reset();
                            hook.Install();
                        }

                        break;

                    case WmDisarm:
                        if (_filter.Holding)
                        {
                            _disarmPending = true;
                        }
                        else
                        {
                            hook.Remove();
                        }

                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the watcher thread threw; Escape no longer cancels a placement", ex);
        }
        finally
        {
            _started.Set();
        }
    }

    /// <summary>Hook thread: one key event. True to keep it from every other window.</summary>
    private bool OnKey(int virtualKey, bool down)
    {
        var modifier = IsDown(VkControl) || IsDown(VkMenu) || IsDown(VkLWin) || IsDown(VkRWin);
        var verdict = _filter.Decide(virtualKey, down, modifier);

        if (verdict == PlacementKeyVerdict.Cancel)
        {
            Signal();
        }

        // The cancelling Escape is up: the off-switch that waited for it takes effect now, from the
        // queue rather than inside the hook's own call.
        if (_disarmPending && !_filter.Holding)
        {
            _disarmPending = false;
            _ = Post(WmDisarm);
        }

        return verdict != PlacementKeyVerdict.PassOn;
    }

    private void Signal()
    {
        var queued = _ui.TryEnqueue(() =>
        {
            try
            {
                _cancel();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "cancelling the placement threw", ex);
            }
        });

        if (!queued)
        {
            Log.Warn(Scope, "could not queue the placement's Escape to the UI thread");
        }
    }

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    // ---- native callback: catch, log, pass on -----------------------------------------------------

    [UnmanagedCallersOnly]
    private static nint OnKeyboardHook(int code, nint wParam, nint lParam)
    {
        try
        {
            var message = (uint)wParam;
            if (code >= 0 && _current is { } watcher
                && message is WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp
                && watcher.OnKey((int)((KbdllHookStruct*)lParam)->VkCode, message is WmKeyDown or WmSysKeyDown))
            {
                return 1;
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the keyboard hook threw", ex);
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    /// <summary>The hook, owned by the watcher thread and removed on it, on disarm and on every way out.</summary>
    private sealed class Hook : IDisposable
    {
        private nint _handle;

        public bool Installed => _handle != 0;

        public void Install()
        {
            if (_handle != 0)
            {
                return;
            }

            var proc = (nint)(delegate* unmanaged<int, nint, nint, nint>)&OnKeyboardHook;
            _handle = SetWindowsHookEx(WhKeyboardLl, proc, Win32.GetModuleHandle(null), 0);
            if (_handle == 0)
            {
                Log.Warn(Scope, $"could not hook the keyboard (win32 {Marshal.GetLastWin32Error()}); Escape cancels a placement only on a surface that has the keyboard");
            }
        }

        public void Remove()
        {
            if (_handle == 0)
            {
                return;
            }

            if (!UnhookWindowsHookEx(_handle))
            {
                Log.Warn(Scope, $"could not unhook the keyboard (win32 {Marshal.GetLastWin32Error()})");
            }

            _handle = 0;
        }

        public void Dispose() => Remove();
    }

    // ---- interop ----------------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdllHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
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
    private static partial short GetAsyncKeyState(int virtualKey);

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
