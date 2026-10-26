using System.Runtime.InteropServices;

namespace Themia.App.Interop;

/// <summary>
/// The window-manager and DWM surface the desktop z-order controller needs. Kept in its own file
/// because it is a self-contained concern, not because it differs in kind.
/// </summary>
internal static unsafe partial class Win32
{
    // ---- WinEvent ------------------------------------------------------------------------------

    internal const uint EventSystemForeground = 0x0003;

    /// <summary>A window was minimised (<c>START</c>) or restored (<c>END</c>).</summary>
    internal const uint EventSystemMinimizeStart = 0x0016;

    internal const uint EventSystemMinimizeEnd = 0x0017;

    /// <summary>
    /// A container reordered its children. For top-level windows the container is the desktop
    /// window, so this is how any change to the top-level z-order is announced.
    /// </summary>
    internal const uint EventObjectReorder = 0x8004;

    /// <summary>
    /// Show Desktop hides applications by DWM-cloaking them, a path that goes through neither
    /// <c>SC_MINIMIZE</c> nor <c>WM_SHOWWINDOW</c>, so a window procedure never sees it.
    /// </summary>
    internal const uint EventObjectCloaked = 0x8017;

    internal const uint EventObjectUncloaked = 0x8018;

    internal const uint WinEventOutOfContext = 0x0000;
    internal const uint WinEventSkipOwnProcess = 0x0002;

    internal const int ObjIdWindow = 0;
    internal const int ChildIdSelf = 0;

    // ---- DWM cloaking --------------------------------------------------------------------------

    /// <summary>Writeable: asks DWM to stop compositing a window without hiding it.</summary>
    internal const int DwmwaCloak = 13;

    /// <summary>Read-only: non-zero when the window is cloaked, and by whom.</summary>
    internal const int DwmwaCloaked = 14;

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmGetWindowAttribute(nint hWnd, int attribute, out uint value, int size);

    // ---- window enumeration and inspection -----------------------------------------------------

    /// <summary>The window immediately below a given one in the z-order.</summary>
    internal const uint GwHwndNext = 2;

    /// <summary>The window immediately above a given one in the z-order.</summary>
    internal const uint GwHwndPrev = 3;

    /// <summary><c>GA_PARENT</c>: the parent window, which for a top-level window is the desktop window.</summary>
    internal const uint GaParent = 1;

    /// <summary>The desktop window: the root every top-level window is a child of (not <c>Progman</c>).</summary>
    [LibraryImport("user32.dll")]
    internal static partial nint GetDesktopWindow();

    [LibraryImport("user32.dll")]
    internal static partial nint GetAncestor(nint hWnd, uint gaFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(nint lpEnumFunc, nint lParam);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint GetWindow(nint hWnd, uint uCmd);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindow(string? lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW", SetLastError = true)]
    internal static partial int GetWindowTextLength(nint hWnd);
}
