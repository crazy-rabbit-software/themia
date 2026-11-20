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
/// [S-8110] The Pomodoro's options, all on its properties menu ([S-3719]): the phases ([S-8112]) and whether the cycle
/// repeats ([S-8114]), then the sound (previewed once) and its level. It has no settings page and no Size: the card is
/// its size ([S-8117]).
/// </summary>
internal static class PomodoroMenu
{
    /// <summary>The steps the volume offers: 0 to 100 in tens.</summary>
    private const int VolumeStep = 10;

    /// <summary>[S-8114] The Repeat line's glyph (Segoe Fluent's repeat-all).</summary>
    private static readonly string RepeatGlyph = char.ConvertFromUtf32(0xE8EE);

    public static void Lines(WidgetMenuLines menu, PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.Lines.Add(Phases(menu, config));
        menu.Lines.Add(menu.Toggle(RepeatGlyph, PomodoroStr.Pomodoro_Repeat_Label.Text(), config.Repeat, value => config.Repeat = value));

        // [S-1460] Off or one of twelve ringtones, gentle to insistent; the volume only while a sound
        // is chosen ([S-6074]). Changing either previews the focus cue, so a choice is made by ear.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Choice(Glyphs.Music, PomodoroStr.Pomodoro_AlarmSound_Label.Text(),
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

    /// <summary>
    /// [S-8112] The phases, in their order ([S-3729]: the menu's one editable list): each a line of its name and
    /// length, dragged (or Ctrl+Up / Down) to reorder, opening Rename… (a name of its own; cleared, the kind's), its
    /// kind, its duration (1–180 min, typed) and Remove (greyed on the last one); then Add a focus phase, Add a short
    /// break and Add a long break, each at its classic length, greyed at sixteen.
    /// </summary>
    internal static ItemMenuEntry Phases(WidgetMenuLines menu, PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        config.Phases ??= PomodoroConfig.Classic();
        var kinds = new[] { PomodoroPhaseKind.Focus, PomodoroPhaseKind.ShortBreak, PomodoroPhaseKind.LongBreak };
        var adds = new[]
        {
            (PomodoroPhaseKind.Focus, PomodoroStr.Pomodoro_Phases_AddFocus),
            (PomodoroPhaseKind.ShortBreak, PomodoroStr.Pomodoro_Phases_AddShortBreak),
            (PomodoroPhaseKind.LongBreak, PomodoroStr.Pomodoro_Phases_AddLongBreak),
        };

        return menu.List(PomodoroModule.Glyph, PomodoroStr.Pomodoro_Phases_Label.Text(), config.Phases,
            new MenuList<PomodoroPhaseSpec>(Line, PomodoroStr.Pomodoro_Phases_Empty.Text())
            {
                Glyph = phase => phase.Kind == PomodoroPhaseKind.Focus ? PomodoroModule.Glyph : Glyphs.Timer,
                Rename = new MenuListRename<PomodoroPhaseSpec>(Rename)
                {
                    Current = phase => phase.Name ?? string.Empty,
                    Placeholder = phase => PomodoroCycle.KindLabel(Strings.Current, phase.Kind),
                    AllowEmpty = true,
                },
                Extra = phase =>
                [
                    menu.Choice(null, PomodoroStr.Pomodoro_Phase_Kind.Text(),
                        [.. kinds.Select(kind => (kind, PomodoroCycle.KindLabel(Strings.Current, kind)))],
                        phase.Kind,
                        value => phase.Kind = value),
                    menu.Number(Glyphs.Timer, PomodoroStr.Pomodoro_Phase_Duration.Text(),
                        phase.EffectiveMinutes, PomodoroConfig.MinMinutes, PomodoroConfig.MaxMinutes, 1, Minutes, value => phase.Minutes = value),
                ],
                AddLines = [.. adds.Select(add => menu.Command(Glyphs.Add, add.Item2.Text(), () => config.Phases.Add(PomodoroPhaseSpec.Of(add.Item1))))],
                Max = PomodoroConfig.MaxPhases,
                Min = 1,
            });
    }

    /// <summary>[S-8112] A phase's line: its name and its length ("Focus · 25 min").</summary>
    internal static string Line(PomodoroPhaseSpec phase)
        => PomodoroStr.Pomodoro_Phases_Item.Format(("phase", PomodoroCycle.Label(phase)), ("length", Minutes(phase.EffectiveMinutes)));

    /// <summary>[S-8112] A name typed for a phase: trimmed and cut to its most; blank gives the kind's name back. True where it changed.</summary>
    internal static bool Rename(PomodoroPhaseSpec phase, string typed)
    {
        var name = (typed ?? string.Empty).Trim();
        var kept = name.Length == 0 ? null : name.Length > PomodoroPhaseSpec.MaxName ? name[..PomodoroPhaseSpec.MaxName] : name;
        if (string.Equals(kept, phase.Name, StringComparison.Ordinal))
        {
            return false;
        }

        phase.Name = kept;
        return true;
    }

    /// <summary>"25 min".</summary>
    internal static string Minutes(int minutes) => $"{minutes}{PomodoroStr.WidgetsC_Pomodoro_MinutesUnit.Text()}";

    /// <summary>The player is single-flight: a new pick restarts the cue rather than stacking copies.</summary>
    private static void Preview(PomodoroConfig config)
        => SoundCue.Play(PomodoroSounds.Wave(config.Sound, PomodoroPhase.Work, config.EffectiveVolume));
}
