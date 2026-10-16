using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Motion;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1014] The Motion section of the Appearance page: the startup, unlock and view-change intros,
/// each with the same controls — a style, a length and a ▶ replay. Any change replays that moment on the desktop, so the user sees what they picked.
/// Then [S-6329] the Smooth value changes switch: live readouts glide and history charts scroll, or both jump.
/// Deliberately not part of a design preset: motion is the user's accessibility choice.
/// </summary>
internal static partial class ConfigPages
{
    /// <summary>The ten styles, in the original's order, for all three moments; named in the current language.</summary>
    private static (IntroAnimation Value, string Label)[] IntroStyles() =>
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

    private static IEnumerable<FrameworkElement> MotionRows(AppHost host)
    {
        yield return FluentLayout.Header(Str.Shell_Motion_Header.Text());

        yield return MotionMoment(host, IntroMode.Startup, Glyphs.Startup, Str.Shell_Motion_Startup.Text(),
            Str.Shell_Motion_StartupDesc.Text());

        yield return MotionMoment(host, IntroMode.Unlock, Glyphs.Lock, Str.Shell_Motion_Unlock.Text(),
            Str.Shell_Motion_UnlockDesc.Text());

        yield return MotionMoment(host, IntroMode.Transition, Glyphs.View, Str.Shell_Motion_Transition.Text(),
            Str.Shell_Motion_TransitionDesc.Text());

        // [S-6329] Live values: readouts glide and history charts scroll, or both jump.
        yield return FluentLayout.Row(Glyphs.Gauge, Str.Shell_Motion_SmoothValues.Text(), Str.Shell_Motion_SmoothValuesDesc.Text(),
            FluentLayout.Toggle(host.State.Design.SmoothValues, value => host.Mutate(s => s.Design.SmoothValues = value)),
            "animation", "interpolate", "glide", "scroll", "graph");

        // [S-1010] Said, not hidden: with Windows' animation effects off nothing here plays.
        if (!Motion.SystemMotion.AnimationsEnabled)
        {
            yield return Note(Str.Shell_Motion_AnimationsOff.Text());
        }
    }

    private static Border MotionMoment(AppHost host, IntroMode mode, string glyph, string title, string description)
    {
        var design = host.State.Design;

        void Set(Action<DesignState> change)
        {
            host.Mutate(s => change(s.Design));
            host.ReplayIntro(mode);
        }

        var style = FluentLayout.Choice(IntroStyles(), IntroMotion.StyleFor(design, mode), value => Set(d =>
        {
            switch (mode)
            {
                case IntroMode.Transition: d.TransitionAnimation = value; break;
                case IntroMode.Unlock: d.UnlockAnimation = value; break;
                default: d.IntroAnimation = value; break;
            }
        }));

        var replay = FluentLayout.IconCommand(Glyphs.Play, Str.Shell_Motion_PlayNow.Text());
        replay.Click += (_, _) => host.ReplayIntro(mode);

        var length = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Children =
            {
                FluentLayout.Slider(IntroMotion.MsFor(design, mode), IntroMotion.MinMs, IntroMotion.MaxMs, 100, " ms", value => Set(d =>
                {
                    switch (mode)
                    {
                        case IntroMode.Transition: d.TransitionAnimationMs = value; break;
                        case IntroMode.Unlock: d.UnlockAnimationMs = value; break;
                        default: d.IntroAnimationMs = value; break;
                    }
                })),
                replay,
            },
        };

        var rows = new List<Border>
        {
            FluentLayout.Row(Glyphs.Timer, Str.Shell_Motion_Length.Text(), Str.Shell_Motion_WholeLengthDesc.Text(), length),
        };

        return FluentLayout.Expander(glyph, title, description, style, rows);
    }
}
