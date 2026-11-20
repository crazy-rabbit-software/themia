using Themia.Core.Model;

namespace Themia.Core.Motion;

/// <summary>
/// [S-1000] The moment an intro plays for. Each moment has its own style and length in
/// <see cref="DesignState"/>, so the three can never be confused by a shared field.
/// </summary>
public enum IntroMode
{
    /// <summary>A surface's first layout after the app (or the surface) starts, and the Startup preview.</summary>
    Startup,

    /// <summary>A real view change on a display, and the View change preview.</summary>
    Transition,

    /// <summary>The session was unlocked, and the Unlock preview.</summary>
    Unlock,
}

/// <summary>
/// The rules every intro shares, kept out of the UI so they can be pinned: which style and length a
/// moment plays ([S-1000]), the cascade between widgets ([S-1002]), and which styles bring the
/// full-screen overlay ([S-1005]).
/// </summary>
public static class IntroMotion
{
    /// <summary>[S-1000] The shortest intro the settings offer; the original's stepper minimum.</summary>
    public const double MinMs = 100;

    /// <summary>[S-1000] The longest intro the settings offer; the original's stepper maximum.</summary>
    public const double MaxMs = 5000;

    /// <summary>[S-1000] Each moment's default length.</summary>
    public const double DefaultMs = 500;

    /// <summary>[S-1002] Delay added per widget, in paint order, while the length has room for it ([S-1760]).</summary>
    public const double StaggerStepMs = 45;

    /// <summary>[S-1002] The cascade's cap, so a dense view does not trail in for seconds.</summary>
    public const double StaggerCapMs = 540;

    /// <summary>[S-1000] The style a moment plays. The one resolver every caller goes through.</summary>
    public static IntroAnimation StyleFor(DesignState design, IntroMode mode)
    {
        ArgumentNullException.ThrowIfNull(design);
        return mode switch
        {
            IntroMode.Transition => design.TransitionAnimation,
            IntroMode.Unlock => design.UnlockAnimation,
            _ => design.IntroAnimation,
        };
    }

    /// <summary>[S-1000] [S-1760] A moment's whole length, every widget and the overlay included, clamped to the offered range.</summary>
    public static double MsFor(DesignState design, IntroMode mode)
    {
        ArgumentNullException.ThrowIfNull(design);
        var ms = mode switch
        {
            IntroMode.Transition => design.TransitionAnimationMs,
            IntroMode.Unlock => design.UnlockAnimationMs,
            _ => design.IntroAnimationMs,
        };

        return ClampMs(ms);
    }

    /// <summary>A length from a file or a control, made playable: non-finite becomes the default.</summary>
    public static double ClampMs(double ms) => DesignNumbers.AnimationMs.Clamp(ms);

    /// <summary>
    /// [S-1010] Whether anything plays at all: never for <see cref="IntroAnimation.None"/>, and never
    /// while Windows' "Animation effects" is off.
    /// </summary>
    public static bool Plays(IntroAnimation style, bool animationsEnabled)
        => animationsEnabled && style != IntroAnimation.None && Enum.IsDefined(style);

    /// <summary>
    /// [S-1005] Whether the moment brings the full-screen hyperspace overlay: hyperspace at startup or
    /// on unlock. A view change never does — it plays only the widgets' own warp-in.
    /// </summary>
    public static bool PlaysOverlay(IntroAnimation style, IntroMode mode)
        => style == IntroAnimation.Hyperspace && mode != IntroMode.Transition;

    /// <summary>
    /// [S-1007] Whether widgets get a cover tile instead of a keyframe of their own. Hacker is the
    /// one style whose reveal is the tile fading, so the widget itself does not move.
    /// </summary>
    public static bool UsesTiles(IntroAnimation style) => style == IntroAnimation.Hacker;
}
