using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
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

    /// <summary>
    /// [S-11301] The meridiem the face is sized on: the wider of the language's AM and PM where its clock
    /// writes one ("7:00 AM"), none where it is a 24-hour clock, so the card does not move with the time.
    /// </summary>
    public static string Unit(Locale locale)
    {
        var (_, unit) = Split(LocaleFormat.Time(locale, new TimeOnly(7, 0)));
        if (unit.Length == 0)
        {
            return string.Empty;
        }

        var (am, pm) = (LocaleFormat.Am(locale), LocaleFormat.Pm(locale));
        return " " + (HeroFit.Ems(pm) > HeroFit.Ems(am) ? pm : am);
    }

    /// <summary>[S-11301] The face: the time on the family's one-readout step with its line under.</summary>
    public static TimerSized Sized(AlarmConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);
        return TimerFace.Sized(Worst(Unit(locale)));
    }

    /// <summary>
    /// [S-11301] The alarm's sizing ([S-2438]): its face over the Controls row, which holds the switch, or Dismiss
    /// and Snooze (worded) while it rings; in the inset, the width up to half again wider, scaling down as one to its
    /// floor ([S-11300]). No header by default.
    /// </summary>
    public static WidgetSizing Sizing(AlarmConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);
        var controls = Math.Max(
            TimerFace.ControlsWidth([], aSwitch: true),
            TimerFace.ControlsWidth([Strings.Get(locale, Str.Alarm_Dismiss), Strings.Get(locale, Str.Alarm_Snooze)]));
        return TimerFace.Sizing(Sized(config, locale), controls, header: false);
    }
}
