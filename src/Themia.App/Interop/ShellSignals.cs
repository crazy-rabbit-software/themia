using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>
/// Reads what the shell is currently doing, so <see cref="Themia.Core.Shell.ZOrderPolicy"/> has
/// something honest to decide from.
/// <para>
/// Every question here is answered by asking Windows, never by remembering what happened last: the
/// shell reorders and cloaks windows without telling anyone, and a cached answer is how a surface
/// ends up stuck in a state nothing will ever undo.
/// </para>
/// </summary>
internal static unsafe class ShellSignals
{
    private const string Scope = "shell-signals";

    /// <summary>
    /// The desktop layer. <c>Progman</c> is the desktop window itself; <c>WorkerW</c> is the wallpaper
    /// host that takes its place once a slideshow or certain shell features are in use; the
    /// <c>SHELLDLL_DefView</c> and <c>SysListView32</c> classes are the icon view hosted inside them,
    /// which is what actually holds the foreground after a click on the wallpaper.
    /// </summary>
    private static readonly string[] DesktopClasses =
        ["Progman", "WorkerW", "SHELLDLL_DefView", "SysListView32"];

    /// <summary>
    /// The desktop layer's own top-level windows, the ones Show Desktop raises ([S-33]); the icon
    /// view classes above are only ever their children.
    /// </summary>
    private static readonly string[] DesktopLayerClasses = ["Progman", "WorkerW"];

    /// <summary>
    /// Surfaces the shell puts in front transiently: the taskbar, and the XAML islands that Start,
    /// Search and Task View are built from. None of them is an application the widgets should hide
    /// behind, and none of them means Show Desktop has ended.
    /// </summary>
    private static readonly string[] ShellSurfaceClasses =
    [
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow",
        "Windows.UI.Composition.DesktopWindowContentBridge",
    ];

    /// <summary>Classes that are the shell rather than an application, for the "is anything up?" sweep.</summary>
    private static readonly string[] NonAppClasses =
        [.. DesktopClasses, .. ShellSurfaceClasses];

    /// <summary>Set while a sweep is running, so the callback can report back without allocating.</summary>
    private static bool _sweepFound;

    /// <summary>The HWNDs this app owns, so a sweep never counts the widget surface as an application.</summary>
    private static nint[] _ownWindows = [];

    /// <summary>Tell the sweeps which windows are ours. Called whenever a surface is created or closed.</summary>
    public static void SetOwnWindows(nint[] windows) => _ownWindows = windows;

    /// <summary>Whether the desktop layer currently owns the foreground.</summary>
    public static bool DesktopIsForeground() => IsClass(Win32.GetForegroundWindow(), DesktopClasses);

    /// <summary>Whether Start, Search, Task View or the taskbar currently owns the foreground.</summary>
    public static bool ShellSurfaceIsForeground() => IsClass(Win32.GetForegroundWindow(), ShellSurfaceClasses);

    /// <summary>[S-28] Whether one of the widget surfaces currently owns the foreground.</summary>
    public static bool OwnSurfaceIsForeground() => IsOurs(Win32.GetForegroundWindow());

    /// <summary>
    /// Whether Show Desktop is genuinely in effect, as opposed to the user having clicked the
    /// wallpaper.
    /// <para>
    /// Two signals: the desktop layer is in front, and no ordinary application window is still on
    /// screen. Clicking the wallpaper satisfies the first but leaves the user's windows up, and
    /// acting on it alone would throw the widgets in front of their work every time they clicked the
    /// desktop.
    /// </para>
    /// <para>
    /// There is deliberately no third "and something got hidden" signal. It would only matter when
    /// nothing was on screen to begin with, and lifting the widgets then covers nothing — while the
    /// two candidate signals for it are both unreliable. Measured on this machine: Show Desktop
    /// <b>minimises</b>, it does not cloak, so a cloak test never fires; and the cloak count is
    /// permanently non-zero anyway, because suspended UWP frames sit cloaked in the background all
    /// the time.
    /// </para>
    /// </summary>
    public static bool ShowDesktopIsActive() => DesktopIsForeground() && !AnyOrdinaryAppVisible();

    /// <summary>Whether a real application window is still on screen — see <see cref="OnWindow"/> for what counts.</summary>
    public static bool AnyOrdinaryAppVisible() => Sweep();

    /// <summary>[S-34b] Whether a top-level window is one of the desktop layer's own (<c>Progman</c>, a <c>WorkerW</c>).</summary>
    public static bool IsDesktopLayerWindow(nint hwnd) => IsClass(hwnd, DesktopLayerClasses);

    /// <summary>Whether a window is currently cloaked by DWM: visible to Windows, but not composited.</summary>
    public static bool IsCloaked(nint hwnd)
        => Win32.DwmGetWindowAttribute(hwnd, Win32.DwmwaCloaked, out var cloaked, sizeof(uint)) >= 0 && cloaked != 0;

    /// <summary>The desktop window, which is the z-order anchor the surface rests just above.</summary>
    public static nint DesktopWindow()
    {
        var progman = Win32.FindWindow("Progman", null);
        return progman != 0 && Win32.IsWindow(progman) ? progman : 0;
    }

    private static bool Sweep()
    {
        _sweepFound = false;

        var callback = (nint)(delegate* unmanaged<nint, nint, int>)&OnWindow;

        // EnumWindows returns false both when the callback stopped it early — the normal success
        // path here — and when the enumeration itself failed, so the flag is the answer and the
        // return value is only worth reading when nothing was found.
        if (!Win32.EnumWindows(callback, 0) && !_sweepFound)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                Log.Debug(Scope, $"the window sweep failed (win32 {error})");
            }
        }

        return _sweepFound;
    }

    /// <summary>
    /// Decides whether one window counts as "an application is still on screen".
    /// <para>
    /// The filters are not cosmetic; each one is a window that would otherwise make Show Desktop
    /// look like it had failed. The topmost one in particular: Show Desktop cannot hide a topmost
    /// window, so one sitting there is not evidence that anything went wrong. That is not
    /// hypothetical — a consent prompt from another program is exactly such a window, and it alone
    /// kept this from ever firing.
    /// </para>
    /// </summary>
    [UnmanagedCallersOnly]
    private static int OnWindow(nint hwnd, nint lParam)
    {
        // An enumeration callback runs on Windows' own call stack: a managed exception unwinding out
        // of here is undefined behaviour.
        try
        {
            if (_sweepFound)
            {
                return 0;
            }

            var ex = (uint)Win32.GetWindowLongPtr(hwnd, Win32.GwlExStyle);

            // A palette or tray helper is not what the user was looking at.
            if ((ex & Win32.WsExToolWindow) != 0)
            {
                return 1;
            }

            // Show Desktop leaves topmost windows exactly where they are, so one being up says
            // nothing about whether Show Desktop is in effect.
            if ((ex & Win32.WsExTopmost) != 0)
            {
                return 1;
            }

            // No title is almost always a helper or a message-only window.
            if (Win32.GetWindowTextLength(hwnd) == 0)
            {
                return 1;
            }

            if (IsOurs(hwnd) || IsClass(hwnd, NonAppClasses))
            {
                return 1;
            }

            // "Still on screen" means genuinely on screen. Show Desktop minimises on this build of
            // Windows and cloaks on others, so both count as hidden.
            if (!Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd) || IsCloaked(hwnd))
            {
                return 1;
            }

            _sweepFound = true;
            return 0;
        }
        catch (Exception exception)
        {
            Log.Error(Scope, "the window sweep callback threw", exception);
            return 0;
        }
    }

    private static bool IsOurs(nint hwnd)
    {
        foreach (var own in _ownWindows)
        {
            if (own == hwnd)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsClass(nint hwnd, string[] classes)
    {
        if (hwnd == 0)
        {
            return false;
        }

        Span<char> buffer = stackalloc char[64];

        fixed (char* name = buffer)
        {
            var length = Win32.GetClassName(hwnd, name, buffer.Length);
            if (length <= 0)
            {
                return false;
            }

            var className = new ReadOnlySpan<char>(name, length);

            foreach (var candidate in classes)
            {
                if (className.SequenceEqual(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
