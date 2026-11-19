namespace Themia.Core.Shell;

/// <summary>[S-13017] [S-13025] When a surface that wants the keys becomes the foreground window.</summary>
public enum ForegroundStep
{
    /// <summary>It is the foreground window already: nothing to do.</summary>
    Already,

    /// <summary>It takes the foreground now.</summary>
    Now,

    /// <summary>It takes the foreground when the press that asked lets go.</summary>
    AfterPress,

    /// <summary>It may not take it (a full-screen app covers its display, [S-34f]).</summary>
    Never,
}

/// <summary>
/// [S-13017] [S-13025] The one rule for when a desktop surface takes the foreground, pure. A plain press waits for its
/// release: activating the surface under a held button took the pointer from the move or the band that had captured it.
/// A shell drag of our own (a desktop icon's, a fence item's, any <c>ItemDrag</c>) takes it now, before it starts: the drag
/// runs OLE's loop on this thread, which follows the mouse through a capture, and Windows gives a window that is not the
/// foreground only the mouse over its own visible part (<c>SetCapture</c>: "a background window ... receives messages only
/// for mouse events that occur when the cursor hot spot is within the visible portion of the window"); OLE's capture
/// window is hidden, so a drag begun from a background surface saw no mouse at all, ended with nothing and a second press
/// was needed, the one after the first press's release had made the surface the foreground.
/// </summary>
public static class PressForeground
{
    /// <summary>
    /// What the surface does: <paramref name="isForeground"/> it is already; <paramref name="mayTake"/> false (a full-screen
    /// app over it) never; a shell drag about to start now; a press still held (<paramref name="pressHeld"/>) after it lets
    /// go; otherwise now.
    /// </summary>
    public static ForegroundStep When(bool isForeground, bool mayTake, bool pressHeld, bool shellDrag)
        => isForeground ? ForegroundStep.Already
            : !mayTake ? ForegroundStep.Never
            : pressHeld && !shellDrag ? ForegroundStep.AfterPress
            : ForegroundStep.Now;
}
