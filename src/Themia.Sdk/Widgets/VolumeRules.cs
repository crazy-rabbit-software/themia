namespace Themia.Core.Widgets;

/// <summary>
/// Which speaker icon the output level deserves. Windows draws four, and so does this: with no
/// number on the tile ([S-2730]) the glyph on the mute button is how the state reads at a glance.
/// </summary>
public enum VolumeLevel
{
    /// <summary>Muted, whatever the level underneath says.</summary>
    Muted = 0,

    /// <summary>Not muted, but turned all the way down — audibly the same, deliberately not.</summary>
    Silent = 1,

    Low = 2,
    Medium = 3,
    High = 4,
}

/// <summary>
/// The arithmetic between the mixer's 0..1 scalar and what a person reads and drags.
/// <para>
/// Separate from the endpoint because it is the part that can be wrong in a way nobody notices: a
/// scalar that rounds to <c>0%</c> while sound is still coming out reads as a broken mute, and a
/// slider that reports back a percentage the mixer never agreed to is how a widget and the volume
/// flyout end up one step apart forever.
/// </para>
/// </summary>
public static class VolumeRules
{
    /// <summary>
    /// The scalar as the whole percentage Windows' own flyout shows.
    /// <para>
    /// Rounded away from zero at the halfway point, as every other volume readout on the machine
    /// does, and never all the way down to <c>0%</c> while the scalar is above zero: silence and
    /// "very quiet" are different facts and a tile that conflates them is lying about the one thing
    /// it is for.
    /// </para>
    /// </summary>
    public static int Percent(double scalar)
    {
        if (!double.IsFinite(scalar) || scalar <= 0)
        {
            return 0;
        }

        var percent = (int)Math.Round(Math.Clamp(scalar, 0, 1) * 100, MidpointRounding.AwayFromZero);
        return percent == 0 ? 1 : percent;
    }

    /// <summary>
    /// The scalar a percentage means. The inverse of <see cref="Percent"/> at every whole step, so
    /// a slider dropped on 34 sets exactly what a tile then reads back as 34%.
    /// </summary>
    public static double Scalar(double percent)
        => double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) / 100.0 : 0;

    /// <summary>
    /// Which speaker glyph the state calls for. Mute wins over the level: a muted endpoint at 80%
    /// is silent, and showing three sound waves over it would be the widget disagreeing with the
    /// machine.
    /// </summary>
    public static VolumeLevel Level(double scalar, bool muted)
    {
        if (muted)
        {
            return VolumeLevel.Muted;
        }

        var percent = Percent(scalar);

        return percent switch
        {
            <= 0 => VolumeLevel.Silent,
            < 34 => VolumeLevel.Low,
            < 67 => VolumeLevel.Medium,
            _ => VolumeLevel.High,
        };
    }

    /// <summary>
    /// [S-2731] Which way the slider lies. Along the tile, so it is as long as the tile allows: a
    /// column ([S-660] <see cref="AreaShape.Tall"/>) stands it up, every other shape lays it down.
    /// </summary>
    public static VolumeAxis Axis(WidgetArea area) => area.Shape == AreaShape.Tall ? VolumeAxis.Vertical : VolumeAxis.Horizontal;
}

/// <summary>[S-2731] The volume slider's direction.</summary>
public enum VolumeAxis
{
    /// <summary>Left to right, the speaker button at its left.</summary>
    Horizontal = 0,

    /// <summary>Bottom to top, the speaker button under it.</summary>
    Vertical = 1,
}
