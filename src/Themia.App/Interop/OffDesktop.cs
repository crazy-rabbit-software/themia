using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>
/// [S-15873] Keeps the windows of a render or check process out of Show Desktop.
/// <para>
/// A render draws in an ordinary window parked at -32000,-32000 and shown without activation. Show
/// Desktop (Win+D) minimised it with the applications, and bringing them back restored it onto the
/// screen: -32000,-32000 is where Windows itself parks a minimised window, so the place it had was not
/// one to restore to. The user saw a check's window appear on the desktop, and the render, drawing in a
/// window that had been minimised under it, ran into its timeout.
/// </para>
/// <para>
/// One hook on this process's own shown windows instead of a call at each of the renders' twenty-odd
/// windows: a render added later is covered without knowing of it. Show Desktop minimises a window
/// that has a minimise box and passes over one that has none (measured with six windows parked at
/// -32000,-32000: the two with the box went down, a tool window that is never activated among them;
/// the four without it stayed, as the real surfaces do, <see cref="DesktopLayer"/>). So each top-level
/// window loses the box. It also becomes a tool window that is never activated, which is what keeps
/// it out of the taskbar, Alt+Tab and any program's sweep for applications on screen ([S-33a]).
/// Nothing about its frame is recalculated: its size and client area stay what the render gave it
/// (the edit bar's pictures keep their sizes).
/// </para>
/// </summary>
internal static unsafe class OffDesktop
{
    private const string Scope = "off-desktop";

    /// <summary>The hook; it lives as long as the process, which exits when its render is done.</summary>
    private static nint _hook;

    /// <summary>
    /// From here every top-level window this process shows is out of Show Desktop's reach. Called
    /// once, on the UI thread, before a render builds anything: the hook is out of context, so its
    /// calls arrive on this thread's message queue. Idempotent.
    /// </summary>
    public static void Enter()
    {
        if (_hook != 0)
        {
            return;
        }

        var callback = (nint)(delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void>)&OnShown;
        _hook = Win32.SetWinEventHook(
            Win32.EventObjectShow,
            Win32.EventObjectShow,
            0,
            callback,
            (uint)Environment.ProcessId,
            0,
            Win32.WinEventOutOfContext);

        if (_hook == 0)
        {
            Log.Warn(Scope, $"could not watch this process's windows (win32 {Marshal.GetLastWin32Error()}); Show Desktop may bring a render's window onto the screen");
        }
    }

    [UnmanagedCallersOnly]
    private static void OnShown(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        // A system hook callback runs on Windows' own call stack: nothing may unwind out of it.
        try
        {
            if (hwnd == 0 || objectId != Win32.ObjIdWindow || childId != Win32.ChildIdSelf
                || Win32.GetAncestor(hwnd, Win32.GaParent) != Win32.GetDesktopWindow())
            {
                return;
            }

            Keep(hwnd);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the shown-window hook threw", ex);
        }
    }

    /// <summary>Take one top-level window out of what Show Desktop minimises. A write only where a bit is off.</summary>
    private static void Keep(nint hwnd)
    {
        var style = (uint)Win32.GetWindowLongPtr(hwnd, Win32.GwlStyle);
        var kept = style & ~(Win32.WsMinimizeBox | Win32.WsMaximizeBox);
        if (kept != style)
        {
            Win32.SetWindowLongPtr(hwnd, Win32.GwlStyle, (nint)kept);
        }

        var ex = (uint)Win32.GetWindowLongPtr(hwnd, Win32.GwlExStyle);
        var wanted = (ex | Win32.WsExToolWindow | Win32.WsExNoActivate) & ~Win32.WsExAppWindow;
        if (wanted != ex)
        {
            Win32.SetWindowLongPtr(hwnd, Win32.GwlExStyle, (nint)wanted);
        }
    }
}
