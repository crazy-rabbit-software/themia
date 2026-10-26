using System.Globalization;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Alarm;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-4631] The Alarm's options, all on its properties menu ([S-3719]), in groups apart: Appearance
/// ([S-3723]); when it rings (the time, the days it repeats on, its switch); what it says (its name);
/// how it sounds (the sound, previewed once, its level and the snooze); the note of [S-4627]. Every line writes the live config and saves; the
/// save is a state change, which the alarm service syncs from at once. It has no settings page.
/// </summary>
internal static class AlarmMenu
{
    /// <summary>The steps the volume offers as dots: 0 to 100 in tens.</summary>
    private const int VolumeStep = 10;

    public static void Lines(WidgetMenuLines menu, AlarmConfig alarm) => Lines(menu, alarm, RegionFormat.Current.WeekStart);

    /// <summary>The lines, the week starting on <paramref name="weekStart"/> (⚙ › Language &amp; region's, [S-3590]; a test's own).</summary>
    internal static void Lines(WidgetMenuLines menu, AlarmConfig alarm, WeekStartDay weekStart)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(alarm);

        var locale = Strings.Current;

        // When it rings: the time, the days and whether it is set at all.
        // [S-4621] Typed ([S-3738]), in either clock; the line shows it in the language's.
        menu.Lines.Add(new ItemMenuEntry(AlarmModule.Glyph, AlarmStr.Alarm_Settings_Time_Label.Text(), () => menu.Ask(new WidgetTextAsk(
            AlarmStr.Alarm_Settings_Time_Label.Text(),
            "07:30",
            Str.Common_Save.Text(),
            typed => TypedTime.TimeOfDay(typed) is null ? AlarmStr.Alarm_Menu_TimeRefused.Text() : null,
            typed =>
            {
                if (TypedTime.TimeOfDay(typed) is { } minutes && AlarmTimes.Text(minutes) != alarm.Time)
                {
                    alarm.Time = AlarmTimes.Text(minutes);
                    menu.Save();
                }
            },
            Initial: AlarmTimes.Text(AlarmTimes.Minutes(alarm.Time)))), Value: AlarmWords.Time(locale, alarm.Plan), Asks: true));

        menu.Lines.Add(Days(menu, alarm, locale, weekStart));
        menu.Lines.Add(menu.Toggle(Glyphs.Bell, AlarmStr.Alarm_Settings_On_Label.Text(), alarm.On, on => alarm.On = on));

        // What it says.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(new ItemMenuEntry(Glyphs.Rename, AlarmStr.Alarm_Settings_Name_Label.Text(), () => menu.Ask(new WidgetTextAsk(
            AlarmStr.Alarm_Settings_Name_Label.Text(),
            AlarmStr.Alarm_Settings_NamePlaceholder.Text(),
            Str.Common_Save.Text(),
            NameRefusal,
            typed =>
            {
                var name = typed.Trim();
                if (name != alarm.Label)
                {
                    alarm.Label = name;
                    menu.Save();
                }
            },
            Initial: alarm.Label,
            AllowEmpty: true)), Value: alarm.Label.Length > 0 ? alarm.Label : null, Asks: true));

        // How it sounds.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Choice(Glyphs.Music, AlarmStr.Alarm_Settings_Sound_Label.Text(),
            [.. PomodoroSounds.Order.Select(sound => (sound, PomodoroSounds.Label(sound)))],
            alarm.Sound,
            value =>
            {
                alarm.Sound = value;
                PreviewRing(alarm);
            }));

        // [S-6074] The volume only while a sound is chosen; choosing one builds the menu again.
        if (alarm.Offers(SoundSetting.Volume))
        {
            menu.Lines.Add(menu.Number(Glyphs.Volume, AlarmStr.Alarm_Settings_Volume_Label.Text(),
                alarm.EffectiveVolume, 0, 100, VolumeStep, Percent,
                value =>
                {
                    alarm.Volume = value;
                    PreviewRing(alarm);
                }));
        }

        menu.Lines.Add(menu.Number(Glyphs.Timer, AlarmStr.Alarm_Settings_Snooze_Label.Text(),
            Math.Clamp(alarm.SnoozeMinutes, AlarmConfig.MinSnooze, AlarmConfig.MaxSnooze), AlarmConfig.MinSnooze, AlarmConfig.MaxSnooze, 1,
            Minutes, value => alarm.SnoozeMinutes = value));

        // [S-4627] Said where alarms are set: they ring only while Themia runs.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(WidgetMenuLines.Note(AlarmStr.Alarm_Settings_WhileRunning.Text()));
    }

    /// <summary>
    /// [S-4631] Repeats on: a switch per day of the week from the start of the week every widget
    /// follows ([S-3770]: ⚙ › Language &amp; region's, never the language's own guess), the days at the
    /// end of the line; none chosen rings once, which the note under the days says.
    /// </summary>
    private static ItemMenuEntry Days(WidgetMenuLines menu, AlarmConfig alarm, Locale locale, WeekStartDay weekStart)
    {
        var mask = AlarmDays.Mask(alarm.Days);
        var lines = new List<ItemMenuEntry>(9);
        foreach (var day in AlarmDays.From(CalendarDays.FirstDay(weekStart)))
        {
            var bit = AlarmDays.Bit(day);
            lines.Add(menu.Toggle(null, LocaleFormat.WeekdayName(locale, day, NameWidth.Wide), AlarmDays.Has(mask, day),
                on => alarm.Days = AlarmDays.Of(on ? mask | bit : mask & ~bit)));
        }

        lines.Add(ItemMenuEntry.Separator);
        lines.Add(WidgetMenuLines.Note(AlarmStr.Alarm_Settings_Repeat_Desc.Text()));

        return WidgetMenuLines.Submenu(Glyphs.Calendar, AlarmStr.Alarm_Settings_Repeat.Text(), lines)
            with { Value = mask == 0 ? null : AlarmWords.Days(locale, mask) };
    }

    /// <summary>Why a typed name cannot be the alarm's, or null: longer than <see cref="AlarmConfig.MostName"/>.</summary>
    internal static string? NameRefusal(string typed)
        => (typed ?? string.Empty).Trim().Length > AlarmConfig.MostName
            ? AlarmStr.Alarm_Menu_NameTooLong.Format(("max", AlarmConfig.MostName))
            : null;

    private static string Percent(int value) => string.Create(CultureInfo.InvariantCulture, $"{value}%");

    private static string Minutes(int minutes) => $"{minutes}{Str.WidgetsC_Pomodoro_MinutesUnit.Text()}";

    /// <summary>Played once, not looped: a choice made by ear, and the single-flight player stops it at the next.</summary>
    private static void PreviewRing(AlarmConfig alarm)
        => SoundCue.Play(PomodoroSounds.Wave(alarm.Sound, PomodoroPhase.Work, alarm.EffectiveVolume));
}
