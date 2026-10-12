using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1476] [S-1477] The clock's lines under its own text size: the place label, the date and the
/// offset line. A size moves a line's floor and ceiling along the type ramp and changes the share
/// of the tile it may ask for; it never invents a font size, so a larger date is still a step
/// Windows uses, and the ladder ([S-1487]) still drops a line that does not fit rather than
/// clipping it. The time takes what the lines leave, so a larger date costs the digits, not the
/// tile's edge.
/// </summary>
public static class ClockText
{
    /// <summary>Ramp steps the floor moves (Large lifts it off Caption: a date that is meant to be read).</summary>
    public static int FloorShift(ClockTextSize size) => size == ClockTextSize.Large ? 1 : 0;

    /// <summary>Ramp steps the ceiling moves: one down for Small, two up for Large.</summary>
    public static int CeilingShift(ClockTextSize size) => size switch
    {
        ClockTextSize.Small => -1,
        ClockTextSize.Large => 2,
        _ => 0,
    };

    /// <summary>How much more (or less) of the tile's height a line may ask for than at the default size.</summary>
    public static double ShareFactor(ClockTextSize size) => size switch
    {
        ClockTextSize.Small => 0.75,
        ClockTextSize.Large => 1.6,
        _ => 1,
    };

    /// <summary>
    /// The step a clock line is set in: <see cref="FluentType.Fit"/> between the default
    /// <paramref name="floor"/> and <paramref name="ceiling"/> moved by the size, in
    /// <paramref name="share"/> of <paramref name="height"/> scaled by it. Regular weight: a date
    /// set larger is still support, never a second title (the ramp's large steps are semibold).
    /// </summary>
    public static TypeStep Fit(ClockTextSize size, double width, double height, double share, double characters, TypeStep floor, TypeStep ceiling)
    {
        var low = Shift(floor, FloorShift(size));
        var high = Shift(ceiling, CeilingShift(size));
        if (high.FontSize < low.FontSize)
        {
            high = low;
        }

        return FluentType.Fit(width, height * share * ShareFactor(size), characters, low, high) with { SemiBold = false };
    }

    /// <summary>The smallest step a line may take at this size: what the short-date switch measures against.</summary>
    public static TypeStep Floor(ClockTextSize size, TypeStep floor) => Shift(floor, FloorShift(size));

    /// <summary>
    /// The step <paramref name="by"/> places along <see cref="FluentType.Ramp"/> from
    /// <paramref name="step"/>, clamped to the ramp's ends. A step not on the ramp is matched to the
    /// nearest one by size.
    /// </summary>
    public static TypeStep Shift(TypeStep step, int by)
    {
        var ramp = FluentType.Ramp;
        var at = 0;
        var best = double.MaxValue;

        for (var i = 0; i < ramp.Count; i++)
        {
            var distance = Math.Abs(ramp[i].FontSize - step.FontSize);
            if (distance < best)
            {
                best = distance;
                at = i;
            }
        }

        return ramp[Math.Clamp(at + by, 0, ramp.Count - 1)];
    }
}
