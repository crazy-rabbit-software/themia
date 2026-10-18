using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>What is known about a top-level window when a layout looks for the one it opened.</summary>
/// <param name="Handle">The HWND.</param>
/// <param name="ProcessId">Its process.</param>
/// <param name="ExeName">Its process's file name (<c>WINWORD.EXE</c>), or empty when it cannot be read.</param>
/// <param name="ClassName">Its window class.</param>
/// <param name="Visible">WS_VISIBLE.</param>
/// <param name="Owned">It has an owner (a dialog, a tool window of another window).</param>
/// <param name="ToolWindow">WS_EX_TOOLWINDOW.</param>
/// <param name="TitleLength">The length of its title: splash screens usually have none.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
public readonly record struct WindowFacts(
    nint Handle,
    int ProcessId,
    string ExeName,
    string ClassName,
    bool Visible,
    bool Owned,
    bool ToolWindow,
    int TitleLength,
    int Width,
    int Height);

/// <summary>How well a window answers a launch.</summary>
public enum WindowMatch
{
    /// <summary>Not this launch's window.</summary>
    None = 0,

    /// <summary>The launch's, but not obviously its main window (a splash, untitled, small): kept in case nothing better comes.</summary>
    Fallback = 1,

    /// <summary>The launch's main window: put it in its pane now.</summary>
    Main = 2,
}

/// <summary>
/// What a launch is waiting for: the process it started (0 when the shell handed none back — a URL,
/// a single-instance app), the exe its window belongs to, and the windows that were already there.
/// </summary>
public sealed record WindowWatch(LayoutSource Source, int ProcessId, string? ExeHint, IReadOnlySet<nint> Before, int OwnProcessId);

/// <summary>
/// [S-1409] Which window a layout launch opened (original <c>find_best_window</c> and
/// <c>launch_explorer</c>). Pure, so the rules that keep the desktop and the taskbar from ever being
/// moved are tested.
/// </summary>
public static class LayoutWindows
{
    /// <summary>File Explorer's folder windows. The only <c>explorer.exe</c> windows a layout may move.</summary>
    public const string ExplorerClass = "CabinetWClass";

    /// <summary>A real main window, not a splash or a transient: visible, unowned, not a tool window, titled, at least 400 × 300.</summary>
    public static bool IsMain(WindowFacts window)
        => IsTopLevel(window) && window.TitleLength > 0 && window.Width >= 400 && window.Height >= 300;

    /// <summary>A window a person would call "a window": visible, unowned, not a tool window.</summary>
    public static bool IsTopLevel(WindowFacts window) => window.Visible && !window.Owned && !window.ToolWindow;

    /// <summary>
    /// [S-1409] Judge one window against a launch. A folder pane takes only a File Explorer folder
    /// window (by class, never by exe: <c>explorer.exe</c> also owns the desktop and the taskbar)
    /// that is new, or that came to the front after the launch (the folder was already open). Any
    /// other pane takes a window of the process it started, or of its exe that is new or came to the
    /// front (a single-instance app hands the launch to its running copy). Never one of this app's
    /// own windows; never an <c>explorer.exe</c> window that is not a folder window.
    /// </summary>
    public static WindowMatch Judge(WindowWatch watch, WindowFacts window, bool cameToFront)
    {
        ArgumentNullException.ThrowIfNull(watch);

        if (!IsTopLevel(window) || window.ProcessId == watch.OwnProcessId)
        {
            return WindowMatch.None;
        }

        var fresh = !watch.Before.Contains(window.Handle) || cameToFront;
        bool ours;

        if (watch.Source == LayoutSource.Folder)
        {
            ours = fresh && string.Equals(window.ClassName, ExplorerClass, StringComparison.Ordinal);
        }
        else
        {
            if (IsExplorer(window.ExeName) && !string.Equals(window.ClassName, ExplorerClass, StringComparison.Ordinal))
            {
                return WindowMatch.None;
            }

            var byProcess = watch.ProcessId != 0 && window.ProcessId == watch.ProcessId;
            var byExe = fresh
                && !string.IsNullOrEmpty(watch.ExeHint)
                && string.Equals(window.ExeName, watch.ExeHint, StringComparison.OrdinalIgnoreCase);
            ours = byProcess || byExe;
        }

        if (!ours)
        {
            return WindowMatch.None;
        }

        return IsMain(window) ? WindowMatch.Main : WindowMatch.Fallback;
    }

    /// <summary>
    /// [S-1409] The running window to move when an app is already open (original "reposition if
    /// running"): the first main window of that exe in z-order. None for a folder or a web address,
    /// and never an <c>explorer.exe</c> window.
    /// </summary>
    public static WindowFacts? Running(IEnumerable<WindowFacts> windows, LayoutSource source, string? exeHint, int ownProcessId)
    {
        ArgumentNullException.ThrowIfNull(windows);

        if (source is LayoutSource.Folder or LayoutSource.Url || string.IsNullOrEmpty(exeHint) || IsExplorer(exeHint))
        {
            return null;
        }

        foreach (var window in windows)
        {
            if (window.ProcessId != ownProcessId
                && IsMain(window)
                && string.Equals(window.ExeName, exeHint, StringComparison.OrdinalIgnoreCase))
            {
                return window;
            }
        }

        return null;
    }

    private static bool IsExplorer(string? exe) => string.Equals(exe, "explorer.exe", StringComparison.OrdinalIgnoreCase);
}
