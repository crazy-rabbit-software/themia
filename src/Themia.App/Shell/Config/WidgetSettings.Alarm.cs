using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.System;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-4631] The Alarm settings: the one alarm's time, its switch, its name and the days it repeats on,
/// then the sound, its level and the snooze. Every row writes the live config and saves; the save is a
/// state change, which the alarm service syncs from at once.
/// </summary>
internal static partial class WidgetSettings
{
    public static void Alarm(List<FrameworkElement> rows, AlarmConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.Alarm_Label.Text()));
        AlarmRows(rows, config, save);

        // [S-4627] Said where alarms are set: they ring only while Themia runs.
        rows.Add(FluentLayout.Note(Str.Alarm_Settings_WhileRunning.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Sound, Str.Alarm_Settings_Sound_Label.Text(), Str.Alarm_Settings_Sound_Desc.Text(),
            FluentLayout.Choice(
                PomodoroSounds.Order.Select(sound => (sound, PomodoroSounds.Label(sound))).ToArray(),
                config.Sound,
                value =>
                {
                    config.Sound = value;
                    save();
                    PreviewRing(config);
                }),
            "ringtone", "ring", "sound"));

        // [S-6074] The volume only while a sound is chosen; choosing one builds the page again.
        if (config.Offers(SoundSetting.Volume))
        {
            rows.Add(FluentLayout.Row(Glyphs.Volume, Str.Alarm_Settings_Volume_Label.Text(), Str.Alarm_Settings_Volume_Desc.Text(),
                FluentLayout.Slider(config.EffectiveVolume, 0, 100, 1, "%",
                    value => { config.Volume = (int)Math.Round(value); save(); PreviewRing(config); }),
                "volume", "loud"));
        }

        rows.Add(FluentLayout.Row(Glyphs.Alarm, Str.Alarm_Settings_Snooze_Label.Text(), Str.Alarm_Settings_Snooze_Desc.Text(),
            FluentLayout.Slider(Math.Clamp(config.SnoozeMinutes, AlarmConfig.MinSnooze, AlarmConfig.MaxSnooze), AlarmConfig.MinSnooze, AlarmConfig.MaxSnooze, 1,
                Str.WidgetsC_Pomodoro_MinutesUnit.Text(),
                value => { config.SnoozeMinutes = (int)Math.Round(value); save(); }),
            "snooze"));

        // Played once, not looped: a choice made by ear, and the single-flight player stops it at the next.
        static void PreviewRing(AlarmConfig config)
            => SoundCue.Play(PomodoroSounds.Wave(config.Sound, PomodoroPhase.Work, config.EffectiveVolume));
    }

    /// <summary>The alarm itself: its time, its switch, its name and the days it repeats on.</summary>
    private static void AlarmRows(List<FrameworkElement> rows, AlarmConfig alarm, Action save)
    {
        var locale = Strings.Current;

        var time = new TimePicker
        {
            SelectedTime = TimeSpan.FromMinutes(AlarmTimes.Minutes(alarm.Time)),
            ClockIdentifier = LocaleFormat.Uses24Hour(locale) ? "24HourClock" : "12HourClock",
            MinuteIncrement = 1,
        };

        time.SelectedTimeChanged += (_, _) =>
        {
            // A cleared picker leaves the alarm where it was.
            if (time.SelectedTime is not { } picked)
            {
                return;
            }

            var text = AlarmTimes.Text((int)Math.Round(picked.TotalMinutes));
            if (text == alarm.Time)
            {
                return;
            }

            alarm.Time = text;
            save();
        };

        rows.Add(FluentLayout.Row(Glyphs.Alarm, Str.Alarm_Settings_Time_Label.Text(), null, time, "time", "wake"));

        rows.Add(FluentLayout.Row(Glyphs.Bell, Str.Alarm_Settings_On_Label.Text(), Str.Alarm_Settings_On_Desc.Text(),
            FluentLayout.Toggle(alarm.On, value =>
            {
                alarm.On = value;
                save();
            }),
            "on", "off", "enable"));

        var label = new TextBox
        {
            Text = alarm.Label,
            PlaceholderText = Str.Alarm_Settings_NamePlaceholder.Text(),
            MaxLength = AlarmConfig.MostName,
            MinWidth = FluentLayout.ChoiceMinWidth,
        };

        // On lost focus and on Enter, never per keystroke.
        void CommitLabel()
        {
            var text = label.Text.Trim();
            if (text == alarm.Label)
            {
                return;
            }

            alarm.Label = text;
            save();
        }

        label.LostFocus += (_, _) => CommitLabel();
        label.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                CommitLabel();
                args.Handled = true;
            }
        };

        rows.Add(FluentLayout.Row(Glyphs.Rename, Str.Alarm_Settings_Name_Label.Text(), null, label, "name", "label"));

        // [S-4621] A button per day, in the language's week.
        var days = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1 };
        foreach (var day in AlarmDays.From(LocaleFormat.FirstDayOfWeek(locale)))
        {
            var button = new ToggleButton
            {
                Content = LocaleFormat.WeekdayName(locale, day, NameWidth.Short),
                IsChecked = AlarmDays.Has(AlarmDays.Mask(alarm.Days), day),
                MinWidth = 44,
                Padding = new Thickness(Fluent.Space1, Fluent.Space1, Fluent.Space1, Fluent.Space1),
            };

            var each = day;
            button.Click += (_, _) =>
            {
                var mask = AlarmDays.Mask(alarm.Days);
                mask = button.IsChecked == true ? mask | AlarmDays.Bit(each) : mask & ~AlarmDays.Bit(each);
                alarm.Days = AlarmDays.Of(mask);
                save();
            };

            days.Children.Add(button);
        }

        rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.Alarm_Settings_Repeat.Text(), Str.Alarm_Settings_Repeat_Desc.Text(),
            new ScrollViewer
            {
                Content = days,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollMode = ScrollMode.Disabled,
            },
            "days", "repeat", "weekdays"));
    }
}
