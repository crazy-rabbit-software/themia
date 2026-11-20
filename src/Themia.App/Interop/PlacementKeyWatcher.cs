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
/// edit bar button it returns to is hidden while placing), from a surface that never took the
/// foreground, or with the pointer, and the ghost, on another display. The hook is the one signal that
/// sees the key in every one of those. It is <see cref="OutsideClickWatcher"/>'s kind (<see cref="LowLevelHookThread"/>):
/// on only while a session is open, on a thread of its own whose only work is pumping messages (a busy UI
/// thread can never stall the system's keyboard), and the callback only decides through Core
/// <see cref="PlacementKeyFilter"/> and queues one signal to the UI thread. The cancelling press is
/// swallowed so it does not also reach whatever has the keyboard.
/// </para>
/// </summary>
internal sealed unsafe class PlacementKeyWatcher : LowLevelHookThread
{
    private const string Scope = "placement-keys";
    private const string Lost = "Escape no longer cancels a placement";

    private const int WhKeyboardLl = 13;
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

    private readonly Action _cancel;

    /// <summary>Hook thread only: fed every key event in order.</summary>
    private readonly PlacementKeyFilter _filter = new();

    /// <summary>Hook thread only: the keyboard hook while armed.</summary>
    private HookHandle? _hook;

    /// <summary>Hook thread only: the hook is wanted off, once the cancelling Escape is up ([S-1594]).</summary>
    private bool _disarmPending;

    /// <param name="ui">The UI thread, where <paramref name="cancel"/> runs.</param>
    /// <param name="cancel">Escape was pressed while placing.</param>
    public PlacementKeyWatcher(DispatcherQueue ui, Action cancel)
        : base(Scope, Lost, "Themia placement keys", ui)
    {
        _cancel = cancel;
        _current = this;
        Start();
    }

    protected override void Armed()
    {
        _disarmPending = false;
        if (_hook is null)
        {
            _filter.Reset();
            _hook = HookHandle.Install(WhKeyboardLl, &OnKeyboardHook, Scope, "Escape cancels a placement only on a surface that has the keyboard");
        }
    }

    /// <summary>A held cancelling Escape keeps the hook on until it is released.</summary>
    protected override void Disarmed()
    {
        if (_filter.Holding)
        {
            _disarmPending = true;
        }
        else
        {
            Stopped();
        }
    }

    protected override void Stopped()
    {
        _hook?.Dispose();
        _hook = null;
    }

    protected override void Disposed()
    {
        if (ReferenceEquals(_current, this))
        {
            _current = null;
        }
    }

    /// <summary>Hook thread: one key event. True to keep it from every other window.</summary>
    private bool OnKey(int virtualKey, bool down)
    {
        var modifier = IsDown(VkControl) || IsDown(VkMenu) || IsDown(VkLWin) || IsDown(VkRWin);
        var verdict = _filter.Decide(virtualKey, down, modifier);

        if (verdict == PlacementKeyVerdict.Cancel)
        {
            _ = Enqueue(_cancel, "cancelling the placement");
        }

        // The cancelling Escape is up: the off-switch that waited for it takes effect now, from the
        // queue rather than inside the hook's own call.
        if (_disarmPending && !_filter.Holding)
        {
            _disarmPending = false;
            DisarmLater();
        }

        return verdict != PlacementKeyVerdict.PassOn;
    }

    private static bool IsDown(int virtualKey) => (Win32.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

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

        return HookHandle.Next(code, wParam, lParam);
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
}
