namespace Themia.Core.Shell;

/// <summary>[S-66] What the copy already running does when Themia is started again.</summary>
public enum SecondStartAction
{
    /// <summary>Nothing: Windows started the second copy at sign-in, not the user ([S-934]).</summary>
    Ignore,

    /// <summary>
    /// Edit mode on (it stays on when it was), and the edit bar's settings menu open on the main
    /// display ([S-3520]): what a user who starts Themia again from Start is looking for, since it
    /// has no window, no taskbar button and no tray entry ([S-64]).
    /// </summary>
    OpenSettings,

    /// <summary>The message carried something no copy of Themia sends: logged and ignored.</summary>
    Unknown,
}

/// <summary>
/// [S-66] The second start's hand-off as pure rules: the copy that stands down says how it was
/// started in the activation message's <c>wParam</c>, and the running copy decides from that alone.
/// <para>
/// Any program can post the registered message, so the value is a trust boundary: only the two
/// values a second copy sends mean anything, and anything else is <see cref="SecondStartAction.Unknown"/>.
/// </para>
/// </summary>
public static class SecondStart
{
    /// <summary>The user started it (Start, a shortcut, <c>Themia.exe</c> again).</summary>
    public const nint ByHand = 1;

    /// <summary>Windows started it at sign-in (the Run entry's flag or the package's startup task).</summary>
    public const nint AtSignIn = 2;

    /// <summary>What the copy that stands down sends for how it was started.</summary>
    public static nint Encode(LaunchSource source) => source == LaunchSource.Autostart ? AtSignIn : ByHand;

    /// <summary>
    /// What the running copy does for the message's <paramref name="wParam"/>: a start by hand opens
    /// the settings menu ([S-66]), whether edit mode was on or not; a start at sign-in does nothing,
    /// so a second autostart entry never throws edit mode up over the desktop at sign-in.
    /// </summary>
    public static SecondStartAction Decide(nint wParam) => wParam switch
    {
        ByHand => SecondStartAction.OpenSettings,
        AtSignIn => SecondStartAction.Ignore,
        _ => SecondStartAction.Unknown,
    };
}
