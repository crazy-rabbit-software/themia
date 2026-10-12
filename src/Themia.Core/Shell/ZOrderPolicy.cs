namespace Themia.Core.Shell;

/// <summary>What to do with the widget surface's z-order right now.</summary>
public enum ZAction
{
    /// <summary>
    /// Show Desktop is genuinely in effect: hold the surface at the top of the topmost band, because
    /// the shell has elevated the desktop window past normal z-order and anything below it is buried.
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
/// that is easy to get wrong and impossible to test through the window manager. Every caller — the
/// event hooks and the backstop alike — routes through <see cref="Decide"/>, so the surface can never
/// be driven into two different states by two different code paths.
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
}
