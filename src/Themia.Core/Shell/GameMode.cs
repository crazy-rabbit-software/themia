namespace Themia.Core.Shell;

/// <summary>
/// What Windows says about the user's attention: its <c>QUERY_USER_NOTIFICATION_STATE</c>, the same
/// signal it uses to hold back its own notifications. The numbers are the Win32 values, so the
/// interop layer casts straight into this.
/// </summary>
public enum UserNotificationState
{
    /// <summary>The question could not be asked, or Windows answered with a value this app does not know.</summary>
    Unknown = 0,

    /// <summary>A screen saver is running, the machine is locked, or a remote session is not active.</summary>
    NotPresent = 1,

    /// <summary>A full-screen application has the foreground (borderless-fullscreen video, a kiosk app, a borderless game).</summary>
    Busy = 2,

    /// <summary>An exclusive full-screen Direct3D application is running.</summary>
    RunningD3DFullScreen = 3,

    /// <summary>Windows' presentation settings are on ("I am currently giving a presentation").</summary>
    PresentationMode = 4,

    /// <summary>Nothing is in the way.</summary>
    AcceptsNotifications = 5,

    /// <summary>The first hour after a new user's first sign-in.</summary>
    QuietTime = 6,

    /// <summary>A Store app is running full-screen (a Windows 8 notion; not a game signal).</summary>
    App = 7,
}

/// <summary>Who owns the foreground, as far as game mode cares.</summary>
public enum ForegroundKind
{
    /// <summary>No foreground window at all — mid-switch, or the secure desktop is up.</summary>
    None = 0,

    /// <summary>An ordinary application, which may or may not be full-screen.</summary>
    Application = 1,

    /// <summary>A window of this process: a widget surface in edit mode, or the layout editor.</summary>
    OwnWindow = 2,

    /// <summary>The desktop itself (<c>Progman</c>, <c>WorkerW</c>, the icon view).</summary>
    Desktop = 3,

    /// <summary>A transient shell surface: Start, Search, Task View, the taskbar.</summary>
    ShellSurface = 4,
}

/// <summary>
/// [S-920] Game mode's decision: should the widgets be suspended right now?
/// <para>
/// A pure function of the shell's answer, who has the foreground and what is in effect now, kept out
/// of the Win32 that gathers those signals so every rule can be tested. Every trigger — a foreground
/// change, a window going full-screen in place, a display change, the switch being flipped — routes
/// through <see cref="Decide"/>, so two triggers can never leave the desktop in two different states.
/// </para>
/// </summary>
public static class GameMode
{
    /// <param name="enabled">The user's "pause widgets during full-screen apps" switch.</param>
    /// <param name="state">What <c>SHQueryUserNotificationState</c> answered just now.</param>
    /// <param name="foreground">Who owns the foreground just now.</param>
    /// <param name="suspended">Whether the widgets are suspended now.</param>
    /// <returns>Whether the widgets should be suspended.</returns>
    public static bool Decide(bool enabled, UserNotificationState state, ForegroundKind foreground, bool suspended)
    {
        // [S-921] Off means off, at once: flipping the switch while a game runs brings the widgets back.
        if (!enabled)
        {
            return false;
        }

        // [S-922] Never suspend under the user's own hand. A widget surface in edit mode spans its
        // whole monitor and takes the foreground, which Windows reports as a full-screen app — the
        // widgets would hide themselves the moment the user started arranging them.
        if (foreground == ForegroundKind.OwnWindow)
        {
            return false;
        }

        // [S-923] Fail open. A shell that cannot be asked must never leave the desktop stuck empty.
        if (state == UserNotificationState.Unknown)
        {
            return false;
        }

        // [S-924] Hold through the moments that say nothing about the game: Start, Search or the
        // taskbar opened over it, the lock screen, a foreground switch in flight. Resuming there
        // would rebuild every widget only to tear them down again a second later when the game
        // takes the screen back — the churn game mode exists to avoid.
        if (foreground is ForegroundKind.ShellSurface or ForegroundKind.None
            || state == UserNotificationState.NotPresent)
        {
            return suspended;
        }

        // [S-925] Presentation settings are a promise about the whole machine, whatever is in front.
        if (state == UserNotificationState.PresentationMode)
        {
            return true;
        }

        // [S-926] Otherwise only an application that has the screen counts: exclusive Direct3D, or
        // any window covering its monitor (borderless games, full-screen video). The desktop is never
        // "full-screen", and Show Desktop over a game is the user asking to see the widgets.
        return foreground == ForegroundKind.Application
            && state is UserNotificationState.Busy or UserNotificationState.RunningD3DFullScreen;
    }

    /// <summary>
    /// Map a raw <c>QUERY_USER_NOTIFICATION_STATE</c> into the closed set, so a value a future Windows
    /// adds reads as <see cref="UserNotificationState.Unknown"/> (fail open, [S-923]) rather than as
    /// whatever enum member happens to share its number.
    /// </summary>
    public static UserNotificationState FromRaw(int raw)
        => raw is >= 1 and <= 7 ? (UserNotificationState)raw : UserNotificationState.Unknown;
}
