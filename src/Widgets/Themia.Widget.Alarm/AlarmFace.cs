using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-4629] The alarm's face on the family's blocks: the timers' face ([S-6739], <see cref="TimerFace"/>)
/// with the alarm's time as its Readout, the meridiem its unit, "name · hint" its line under, and the
/// switch (or Dismiss and Snooze while it rings) its Controls row.
/// </summary>
public static class AlarmFace
{
    /// <summary>The widest time of day in either clock: every figure an 8.</summary>
    public const string WidestTime = "88:88";

    /// <summary>
    /// [S-4628] A time of day as the Readout draws it: the figures, and the meridiem after them as its
    /// unit with the space that stood between ("6:45 AM" is "6:45" and " AM"). A time whose last word
    /// holds a figure, or that has no space ("06:45", a meridiem written first), is all value.
    /// </summary>
    public static (string Value, string Unit) Split(string? time)
    {
        if (string.IsNullOrEmpty(time))
        {
            return (string.Empty, string.Empty);
        }

        var space = time.LastIndexOfAny([' ', ' ', ' ']);
        if (space <= 0 || space == time.Length - 1 || time[(space + 1)..].Any(char.IsDigit) || !char.IsDigit(time[space - 1]))
        {
            return (time, string.Empty);
        }

        return (time[..space], time[space..]);
    }

    /// <summary>The widest case the face is sized on: every figure an 8, the language's meridiem, a line under.</summary>
    public static ReadoutText Worst(string unit) => new(WidestTime, unit, LineUnder: true, OneReadout: true);

    /// <summary>[S-4629] The face for a view of <paramref name="area"/>: its content is the area less the inset either side and at the foot.</summary>
    public static TimerPlan Plan(WidgetArea area, string unit)
        => TimerFace.Plan(area, area.Width - (2 * FamilySpace.Inset), area.Height - FamilySpace.Inset, Worst(unit), controls: true);
}
