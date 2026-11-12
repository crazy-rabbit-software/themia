using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Pomodoro;

namespace Themia.Core.Widgets;

/// <summary>[S-1466] What a pomodoro tile draws: the ring with the digits in it, the digits over the bar, or the digits alone.</summary>
public enum PomodoroLook
{
    Ring,
    Bar,
    Digits,
}

/// <summary>
/// [S-11304] The pomodoro's face at its Size: its look, the ring's diameter (0 off the ring), the
/// readout's step, and the face's own width and height (the bar not included).
/// </summary>
public readonly record struct PomodoroPlan(PomodoroLook Look, double Ring, TypeStep Step, double Width, double Height);

/// <summary>
/// [S-1466] [S-11304] The pomodoro on the timers' sized face ([S-11300], <see cref="TimerFace"/>): the
/// time left as the Readout on the step its Size names, the phase (Focus, Break) its line under,
/// Start/Pause as the one main action with −, +, Reset and Skip as icon buttons in the Controls row
/// under it (all icon buttons at Small). The Ring style sets the readout one step under in the least
/// ring that holds it; the Bar style puts the bar (the Chart's 4 track) 8 under the readout, the
/// content's width; Digits draws neither. The readout is sized on the longer phase's widest time.
/// </summary>
public static class PomodoroFace
{
    /// <summary>What the bar takes under the readout: the gap and the track.</summary>
    public const double BarCost = FamilySpace.S2 + FamilyShape.Track;

    /// <summary>[S-1466] The widest readout the config's phases can show ("25:00" reads "88:88"), with its line under.</summary>
    public static ReadoutText Worst(PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var longest = Math.Max(PomodoroCycle.PhaseSeconds(PomodoroPhase.Work, config), PomodoroCycle.PhaseSeconds(PomodoroPhase.Break, config));
        var digits = new string([.. PomodoroCycle.Clock(longest).Select(c => char.IsDigit(c) ? '8' : c)]);
        return new ReadoutText(digits, LineUnder: true, OneReadout: true);
    }

    /// <summary>[S-11304] The face for <paramref name="config"/>'s style and Size.</summary>
    public static PomodoroPlan Plan(PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var face = TimerFace.Sized(config.Size, Worst(config), ring: config.DrawsRing);
        var look = config.DrawsRing ? PomodoroLook.Ring : config.DrawsBar ? PomodoroLook.Bar : PomodoroLook.Digits;
        return new PomodoroPlan(look, face.Ring, face.Step, face.Width, face.Height);
    }

    /// <summary>
    /// [S-11304] The pomodoro's sizing ([S-2438]): its face at its Size, the bar under it on the Bar
    /// style, the Controls row (Start or Pause worded, −, +, Reset, Skip) under that; one height, up to
    /// half again wider. No header by default.
    /// </summary>
    public static WidgetSizing Sizing(PomodoroConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);
        var plan = Plan(config);
        var main = TimerFace.Longest(PomodoroStr.WidgetsC_Pomodoro_Start.Text(locale), PomodoroStr.WidgetsC_Pomodoro_Pause.Text(locale));
        var controls = TimerFace.ControlsWidth(config.Size, [main], icons: 4);
        return TimerFace.Sizing(new TimerSized(plan.Step, plan.Ring, plan.Width, plan.Height), controls, header: false, extra: plan.Look == PomodoroLook.Bar ? BarCost : 0);
    }
}
