using Themia.Core.Model;

namespace Themia.Core.Motion;

/// <summary>[S-6329] The host's half of the glide rule: the user's switch lives in the design state.</summary>
public static class GlideSwitch
{
    /// <summary>
    /// [S-6329] Whether live values move smoothly now — readouts glide ([S-6323]) and history charts
    /// scroll ([S-1826]): only with Windows' animation effects on ([S-6326], [S-1828]) <b>and</b> the
    /// user's Smooth value changes switch on (<see cref="DesignState.SmoothValues"/>). No design yet
    /// (before the first apply) is the default, on. The one answer both motions ask.
    /// </summary>
    public static bool Smooth(DesignState? design, bool windowsAnimations)
        => windowsAnimations && (design?.SmoothValues ?? true);
}
