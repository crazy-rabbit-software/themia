using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>The Pomodoro settings rows. Each writes straight to the live config and saves.</summary>
internal static partial class WidgetSettings
{
    public static void Pomodoro(List<FrameworkElement> rows, PomodoroConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.Pomodoro_Timing.Text()));

        // [S-01] Seeded from the clamped accessors, so a value that got onto disk out of range shows
        // as the value actually in use rather than snapping when the slider is first touched.
        rows.Add(FluentLayout.Row(Glyphs.Pomodoro, Str.Pomodoro_FocusLength_Label.Text(), Str.Pomodoro_FocusLength_Desc.Text(),
            FluentLayout.Slider(config.EffectiveWorkMinutes, 1, 180, 1, Str.WidgetsC_Pomodoro_MinutesUnit.Text(),
                value => { config.WorkMinutes = (int)Math.Round(value); save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Timer, Str.Pomodoro_BreakLength_Label.Text(), Str.Pomodoro_BreakLength_Desc.Text(),
            FluentLayout.Slider(config.EffectiveBreakMinutes, 1, 180, 1, Str.WidgetsC_Pomodoro_MinutesUnit.Text(),
                value => { config.BreakMinutes = (int)Math.Round(value); save(); })));

        // [S-1460] Off or one of twelve ringtones, gentle to insistent; the volume row only while a
        // sound is chosen. Changing either previews the focus cue, so a choice is made by ear.
        rows.Add(FluentLayout.Row(Glyphs.Sound, Str.Pomodoro_AlarmSound_Label.Text(), Str.Pomodoro_AlarmSound_Desc.Text(),
            FluentLayout.Choice(
                PomodoroSounds.Order.Select(sound => (sound, PomodoroSounds.Label(sound))).ToArray(),
                config.Sound,
                value =>
                {
                    config.Sound = value;
                    save();
                    Preview(config);
                })));

        // [S-6074] The volume only while a sound is chosen; choosing one builds the page again.
        if (config.Offers(SoundSetting.Volume))
        {
            rows.Add(FluentLayout.Row(Glyphs.Volume, Str.Pomodoro_AlarmVolume_Label.Text(), Str.Pomodoro_AlarmVolume_Desc.Text(),
                FluentLayout.Slider(config.EffectiveVolume, 0, 100, 1, "%",
                    value => { config.Volume = (int)Math.Round(value); save(); Preview(config); })));
        }

        // The player is single-flight: a slider drag restarts the cue rather than stacking copies.
        static void Preview(PomodoroConfig config)
            => SoundCue.Play(PomodoroSounds.Wave(config.Sound, PomodoroPhase.Work, config.EffectiveVolume));
    }
}
