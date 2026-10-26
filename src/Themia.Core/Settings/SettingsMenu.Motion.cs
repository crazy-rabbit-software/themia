using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Motion;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3529] Appearance › Motion: the startup, unlock and view-change intros ([S-1014]), each a
/// submenu of the same three things (its style, its length, Play now), and any change replays that
/// moment on the desktop, so the user sees what they picked. Live values never move ([S-6330]), so
/// there is nothing of theirs here. Deliberately not part of a style preset: motion is the user's
/// accessibility choice.
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-1014] The ten styles, in the original's order, for all three moments; named in the current language.</summary>
    public static IReadOnlyList<(IntroAnimation Value, string Text)> IntroStyles() =>
    [
        (IntroAnimation.None, Str.Settings_Style_StartupAnim_None.Text()),
        (IntroAnimation.Fade, Str.Settings_Style_StartupAnim_Fade.Text()),
        (IntroAnimation.Scale, Str.Settings_Style_StartupAnim_Scale.Text()),
        (IntroAnimation.Slide, Str.Shell_Motion_Slide.Text()),
        (IntroAnimation.Sides, Str.Shell_Motion_Sides.Text()),
        (IntroAnimation.TopBottom, Str.Shell_Motion_TopBottom.Text()),
        (IntroAnimation.Flip, Str.Settings_Style_StartupAnim_Flip.Text()),
        (IntroAnimation.Blur, Str.Settings_Style_StartupAnim_Blur.Text()),
        (IntroAnimation.Hyperspace, Str.Settings_Style_StartupAnim_Hyperspace.Text()),
        (IntroAnimation.Hacker, Str.Settings_Style_StartupAnim_Hacker.Text()),
    ];

    /// <summary>[S-3529] The Motion line: the three moments, and why nothing plays while Windows' animations are off ([S-1010]).</summary>
    private static SettingsSubmenu Motion(ISettingsMenuHost host, string key)
    {
        var lines = new List<SettingsLine>
        {
            Moment(host, key + "/startup", IntroMode.Startup, Glyphs.Startup, Str.Shell_Motion_Startup.Text()),
            Moment(host, key + "/unlock", IntroMode.Unlock, Glyphs.Lock, Str.Shell_Motion_Unlock.Text()),
            Moment(host, key + "/transition", IntroMode.Transition, Glyphs.View, Str.Shell_Motion_Transition.Text()),
        };

        // [S-1010] Said, not hidden: with Windows' animation effects off nothing here plays.
        if (!host.AnimationsEnabled)
        {
            lines.Add(new SettingsSeparator(key + "/-off"));
            lines.Add(new SettingsNote(key + "/off", Str.Shell_Motion_AnimationsOff.Text()));
        }

        return new SettingsSubmenu(key, Str.Shell_Motion_Header.Text(), Glyphs.Motion, lines);
    }

    /// <summary>
    /// [S-1014] One moment: its style with the dot (the line's value), its length (a slider, 100 ms to
    /// 5 s in tenths of a second) and Play now. A new style or length is stored and played at once.
    /// </summary>
    private static SettingsSubmenu Moment(ISettingsMenuHost host, string key, IntroMode mode, string glyph, string text)
    {
        var design = host.State.Design;
        var current = IntroMotion.StyleFor(design, mode);

        void Set(Action<DesignState> change)
        {
            host.Mutate(s => change(s.Design));
            host.ReplayIntro(mode);
        }

        var lines = new List<SettingsLine>();
        string? value = null;
        foreach (var (style, label) in IntroStyles())
        {
            var holds = style == current;
            value = holds ? label : value;
            lines.Add(new SettingsRadio(key + "/" + style, label, holds, () =>
            {
                if (!holds)
                {
                    Set(d => SetStyle(d, mode, style));
                }
            }));
        }

        lines.Add(new SettingsSeparator(key + "/-length"));
        lines.Add(new SettingsSlider(key + "/length", Str.Shell_Motion_Length.Text(), Glyphs.Timer, IntroMotion.MsFor(design, mode), IntroMotion.MinMs, IntroMotion.MaxMs, 100,
            Seconds, v => Set(d => SetLength(d, mode, v))));
        lines.Add(new SettingsCommand(key + "/play", Str.Shell_Motion_PlayNow.Text(), Glyphs.Play, () => host.ReplayIntro(mode)));

        return new SettingsSubmenu(key, text, glyph, lines) { Value = value };
    }

    /// <summary>[S-1014] A length as the menu writes it: seconds to a tenth ("0.5 s"), short enough for the slider's value column.</summary>
    public static string Seconds(double ms) => $"{LocaleFormat.Number(ms / 1000, 1)} s";

    private static void SetStyle(DesignState design, IntroMode mode, IntroAnimation style)
    {
        switch (mode)
        {
            case IntroMode.Transition: design.TransitionAnimation = style; break;
            case IntroMode.Unlock: design.UnlockAnimation = style; break;
            default: design.IntroAnimation = style; break;
        }
    }

    private static void SetLength(DesignState design, IntroMode mode, double ms)
    {
        switch (mode)
        {
            case IntroMode.Transition: design.TransitionAnimationMs = ms; break;
            case IntroMode.Unlock: design.UnlockAnimationMs = ms; break;
            default: design.IntroAnimationMs = ms; break;
        }
    }
}
