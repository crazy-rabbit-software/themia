using System.Globalization;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Pomodoro;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-8110] The Pomodoro's options, all on its properties menu ([S-3719]): Appearance ([S-3723]),
/// the focus and break lengths, then the sound (previewed once) and its level. It has no settings page.
/// </summary>
internal static class PomodoroMenu
{
    /// <summary>The steps the volume offers as dots: 0 to 100 in tens.</summary>
    private const int VolumeStep = 10;

    public static void Lines(WidgetMenuLines menu, PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();

        // [S-8101] Seeded from the clamped accessors, so a value out of range on disk shows as the one in use.
        menu.Lines.Add(menu.Number(PomodoroModule.Glyph, PomodoroStr.Pomodoro_FocusLength_Label.Text(),
            config.EffectiveWorkMinutes, 1, 180, 1, Minutes, value => config.WorkMinutes = value));
        menu.Lines.Add(menu.Number(Glyphs.Timer, PomodoroStr.Pomodoro_BreakLength_Label.Text(),
            config.EffectiveBreakMinutes, 1, 180, 1, Minutes, value => config.BreakMinutes = value));

        // [S-1460] Off or one of twelve ringtones, gentle to insistent; the volume only while a sound
        // is chosen ([S-6074]). Changing either previews the focus cue, so a choice is made by ear.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Choice(Glyphs.Sound, PomodoroStr.Pomodoro_AlarmSound_Label.Text(),
            [.. PomodoroSounds.Order.Select(sound => (sound, PomodoroSounds.Label(sound)))],
            config.Sound,
            value =>
            {
                config.Sound = value;
                Preview(config);
            }));

        if (config.Offers(SoundSetting.Volume))
        {
            menu.Lines.Add(menu.Number(Glyphs.Volume, PomodoroStr.Pomodoro_AlarmVolume_Label.Text(),
                config.EffectiveVolume, 0, 100, VolumeStep, value => string.Create(CultureInfo.InvariantCulture, $"{value}%"),
                value =>
                {
                    config.Volume = value;
                    Preview(config);
                }));
        }
    }

    /// <summary>"25 min".</summary>
    internal static string Minutes(int minutes) => $"{minutes}{PomodoroStr.WidgetsC_Pomodoro_MinutesUnit.Text()}";

    /// <summary>The player is single-flight: a new pick restarts the cue rather than stacking copies.</summary>
    private static void Preview(PomodoroConfig config)
        => SoundCue.Play(PomodoroSounds.Wave(config.Sound, PomodoroPhase.Work, config.EffectiveVolume));
}
