using Themia.Core.Geometry;

namespace Themia.Core.Shell;

/// <summary>What to do with the widget surface's z-order right now.</summary>
public enum ZAction
{
    /// <summary>
    /// Show Desktop is genuinely in effect: hold the surface in the topmost band just above the
    /// desktop layer (<see cref="ZOrderPolicy.Lift"/>), because the shell has elevated the desktop
    /// window past normal z-order and anything below it is buried.
    /// </summary>
    PinTopmost = 0,

    /// <summary>
    /// The pin is held but Show Desktop is over: drop back to the resting anchor, just above the
    /// desktop window and below every real application.
    /// </summary>
    DropToAnchor = 1,

    /// <summary>
    /// A transient shell surface — Start, Search, Task View, the taskbar — owns the foreground while
    /// the pin is held. Change nothing.
    /// </summary>
    HoldTopmost = 2,

    /// <summary>Nothing special is happening: put back anything that has drifted out of its band.</summary>
    SelfHeal = 3,
}

/// <summary>
/// The z-order decision, as a pure function of the shell signals.
/// <para>
/// Separated from the Win32 that gathers the signals and applies the result because this is the part
/// that is easy to get wrong and impossible to test through the window manager. Every check — whichever
/// event queued it ([S-34a]) — routes through <see cref="Decide"/>, so the surface can never be driven
/// into two different states by two different code paths.
/// </para>
/// </summary>
public static class ZOrderPolicy
{
    /// <param name="showDesktopActive">
    /// The canonical determination, not merely "the desktop has the foreground": the desktop layer is
    /// foreground <b>and</b> no ordinary application window is still on screen <b>and</b> at least one
    /// application has been cloaked. A plain click on the wallpaper satisfies the first alone, and
    /// treating that as Show Desktop would throw the widgets in front of everything the user is doing.
    /// </param>
    /// <param name="pinnedTopmost">Whether the surface is currently held topmost.</param>
    /// <param name="shellSurfaceForeground">
    /// Whether Start, Search, Task View or the taskbar owns the foreground.
    /// </param>
    /// <param name="ownSurfaceForeground">
    /// Whether one of the widget surfaces itself owns the foreground — the user clicked a widget.
    /// </param>
    /// <param name="applicationOnScreen">
    /// [S-33e] Whether an ordinary application window is on screen again (not minimised, not
    /// cloaked, not topmost). Only read while the pin is held and a shell or own surface is in front.
    /// </param>
    public static ZAction Decide(
        bool showDesktopActive,
        bool pinnedTopmost,
        bool shellSurfaceForeground,
        bool ownSurfaceForeground,
        bool applicationOnScreen)
    {
        if (showDesktopActive)
        {
            return ZAction.PinTopmost;
        }

        if (!pinnedTopmost)
        {
            return ZAction.SelfHeal;
        }

        // Pressing the Windows key puts Start in front, which is not the desktop — so a rule of
        // "anything but the desktop took the foreground, drop the pin" sends the surface back under
        // the still-elevated desktop layer, i.e. the widgets vanish on the Windows key. Dismissing
        // Start then hands the foreground to the taskbar rather than to the desktop, so they stay
        // gone. Holding the pin is what keeps them visible; not re-raising is what lets Start, which
        // was activated after us in the same band, stay on top of them.
        //
        // [S-28] Clicking a widget is the same moment from the other side. It hands the foreground to
        // the surface, so the desktop no longer owns it and Show Desktop no longer reads as active —
        // but nothing has been restored and the desktop layer is still elevated. Dropping the pin
        // then put the surface under it: the widgets vanished on the first click after Win+D, which
        // is exactly when the user had gone to the desktop to use them.
        //
        // [S-33e] Both holds are only for while the desktop is still what is on screen. Show Desktop
        // can end with the taskbar, Task View or a widget still in front (the taskbar's own Show
        // Desktop button, a second Win+D, a three-finger swipe): the windows come back, nothing hands
        // the foreground to them, and holding there drew the widgets over the user's work until a click.
        if (applicationOnScreen)
        {
            return ZAction.DropToAnchor;
        }

        return shellSurfaceForeground || ownSurfaceForeground ? ZAction.HoldTopmost : ZAction.DropToAnchor;
    }

    /// <summary>
    /// [S-34c] Whether a surface is still at its resting place: not topmost, and nothing on screen
    /// below it but other surfaces and the desktop layer.
    /// <para>
    /// Asked before every re-anchor, so a write happens only when the place is actually broken: a
    /// write that always happened would be heard back as a z-order change and asked again, and the
    /// controller would never go quiet.
    /// </para>
    /// </summary>
    /// <param name="topmost">Whether the surface carries <c>WS_EX_TOPMOST</c>.</param>
    /// <param name="below">
    /// Every window between the surface and the desktop window (or the bottom of the z-order when
    /// there is none), nearest first.
    /// </param>
    public static bool AnchorHolds(bool topmost, ReadOnlySpan<StackWindow> below)
    {
        if (topmost)
        {
            return false;
        }

        foreach (var window in below)
        {
            // Anything else on screen below the surface is a window the widgets now cover.
            if (window == StackWindow.Other)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// [S-33] [S-33f] [S-34b] Where a lifted surface belongs in the z-order, and whether it is there:
    /// <b>directly above the desktop layer, below every other window on screen</b>.
    /// <para>
    /// The lift exists to get past the desktop window Show Desktop raises, and past nothing else. It
    /// was "the top of the topmost band", which is above every dock, appbar and always-on-top window
    /// too: with nothing but such a window on screen (a dock over the work area, its flyout, its
    /// tooltip) the widgets were drawn over it and took its clicks. A lifted surface goes where the
    /// desktop's own icons are: just above the highest desktop-layer window on screen, whichever band
    /// that is in. Start, the taskbar and every other program's topmost window stay above it without
    /// anything being asked of them ([S-33c]).
    /// </para>
    /// </summary>
    /// <param name="stack">The whole top-level z-order, top first.</param>
    /// <param name="self">The surface's own position in <paramref name="stack"/>.</param>
    public static LiftPlace Lift(ReadOnlySpan<StackWindow> stack, int self)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(self);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(self, stack.Length);

        var desktop = -1;
        for (var i = 0; i < stack.Length && desktop < 0; i++)
        {
            if (i != self && stack[i] == StackWindow.DesktopLayer)
            {
                desktop = i;
            }
        }

        // No desktop on screen (Explorer is down): nothing to be above, and nothing to measure by.
        if (desktop < 0 || (self < desktop && !stack[(self + 1)..desktop].Contains(StackWindow.Other)))
        {
            return new LiftPlace(LiftMove.Stay, -1);
        }

        var after = desktop - 1 == self ? desktop - 2 : desktop - 1;
        return after < 0 ? new LiftPlace(LiftMove.Top, -1) : new LiftPlace(LiftMove.Under, after);
    }

    /// <summary>
    /// [S-33g] What a z-order move asked of a surface (its <c>WM_WINDOWPOSCHANGING</c>) becomes. At
    /// rest every one becomes the anchor ([S-20a]). While lifted only the controller's own placement
    /// goes through: an activation (a click in edit mode, the surface taking the keys on a click on
    /// empty desktop, [S-5550]) would otherwise raise it to the top of the topmost band, over every
    /// dock, and it used to.
    /// </summary>
    /// <param name="lifted">Whether the surfaces are lifted for Show Desktop.</param>
    /// <param name="ownPlacement">Whether the controller itself is placing the surface right now.</param>
    public static ZMove Move(bool lifted, bool ownPlacement)
        => !lifted ? ZMove.ToAnchor : ownPlacement ? ZMove.Allow : ZMove.KeepOrder;

    /// <summary>
    /// [S-34f] Whether a surface may take the foreground (a key's way to it: the add page from
    /// Ctrl+Alt+Shift+W, the settings on a second start, edit mode's focus): never while a full-screen
    /// app covers its display from above. The surface stays under that app whatever it does, so the
    /// foreground would give it nothing the user can see, and it would tell the shell and a dock (Karpo,
    /// its §702 and §705) that the full-screen app is no longer the display's front: their bars came back
    /// over the video. A click on the surface never meets this: what it clicked was on screen.
    /// </summary>
    /// <param name="display">The surface's display, whole, in physical pixels.</param>
    /// <param name="above">Every window above the surface in the z-order.</param>
    public static bool MayTakeForeground(PixelRect display, ReadOnlySpan<AboveWindow> above)
    {
        foreach (var window in above)
        {
            if (window.OnScreen && !window.Own && !window.MaximizedWithCaption && Covers(window.Rect, display))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>[S-34f] Whether <paramref name="window"/> reaches every edge of <paramref name="display"/> (a full-screen app runs to all four, and past them on some).</summary>
    public static bool Covers(PixelRect window, PixelRect display)
        => display.Width > 0 && display.Height > 0
            && window.X <= display.X && window.Y <= display.Y
            && window.X + window.Width >= display.X + display.Width
            && window.Y + window.Height >= display.Y + display.Height;
}

/// <summary>[S-34f] One window above a surface, as <see cref="ZOrderPolicy.MayTakeForeground"/> sees it.</summary>
/// <param name="Rect">Where it is, in physical pixels.</param>
/// <param name="OnScreen">Visible, not minimised, not cloaked, and not click-through (an overlay drawn over a whole display, a recorder's or a game bar's, is no app in front).</param>
/// <param name="Own">One of this app's windows (a surface, its flyouts).</param>
/// <param name="MaximizedWithCaption">Maximised with a title bar: it covers a display only where nothing holds a strip there, and is no full-screen app.</param>
public readonly record struct AboveWindow(PixelRect Rect, bool OnScreen, bool Own, bool MaximizedWithCaption);

/// <summary>[S-33g] What becomes of a z-order move asked of a surface.</summary>
public enum ZMove
{
    /// <summary>Rewritten to the resting anchor.</summary>
    ToAnchor = 0,

    /// <summary>The surface keeps the place it has: the move's z-order part is dropped.</summary>
    KeepOrder = 1,

    /// <summary>Goes through as asked.</summary>
    Allow = 2,
}

/// <summary>[S-33f] What <see cref="ZOrderPolicy.Lift"/> asks for.</summary>
public enum LiftMove
{
    /// <summary>The surface is where a lifted surface belongs.</summary>
    Stay = 0,

    /// <summary>The desktop layer is the top window: above it, the top of the z-order.</summary>
    Top = 1,

    /// <summary>Directly under the window at <see cref="LiftPlace.After"/>.</summary>
    Under = 2,
}

/// <param name="Move">What to do.</param>
/// <param name="After">For <see cref="LiftMove.Under"/>, the position in the stack of the window to go under; -1 otherwise.</param>
public readonly record struct LiftPlace(LiftMove Move, int After);

/// <summary>What one window in the z-order is, as far as a surface's place is concerned.</summary>
public enum StackWindow
{
    /// <summary>One of this app's own surfaces.</summary>
    Own = 0,

    /// <summary>Not on screen: hidden, minimised or cloaked. Where it sits changes nothing.</summary>
    Hidden = 1,

    /// <summary>The desktop layer (<c>Progman</c>, a <c>WorkerW</c>), on screen.</summary>
    DesktopLayer = 2,

    /// <summary>Any other window on screen.</summary>
    Other = 3,
}
