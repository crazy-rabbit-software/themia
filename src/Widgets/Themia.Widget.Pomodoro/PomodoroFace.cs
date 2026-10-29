using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-1466] What a pomodoro tile draws: the ring with the digits in it, the digits over the bar, or the digits alone.</summary>
public enum PomodoroLook
{
    Ring,
    Bar,
    Digits,
}

/// <summary>
/// [S-1466] The pomodoro's face as planned: its look, the ring's diameter (0 off the ring), the
/// readout's step, whether the phase stands under it and whether the Controls row shows.
/// </summary>
public readonly record struct PomodoroPlan(PomodoroLook Look, double Ring, TypeStep Step, bool Under, bool Controls);

/// <summary>
/// [S-1466] [S-6035] The pomodoro on the timers' face ([S-6739], <see cref="TimerFace"/>): the time
/// left as the Readout on the Hero steps, the phase (Focus, Break) its line under, Start/Pause as the
/// one main action with Reset and Skip as icon buttons in the Controls row under it. The Ring style
/// sets the readout in the Chart ring of the phase's progress wherever a ring holds it, else it draws
/// the Bar style's face; the Bar style puts the bar (the Chart's 4 track) 8 under the readout, the
/// content's width, where the readout's floor step, the bar and the Controls row the tile has room
/// for fit; Digits draws neither. The readout is sized on the longer phase's widest time, so it does
/// not move as the seconds or the phase change.
/// </summary>
public static class PomodoroFace
{
    /// <summary>What the bar takes from the readout's box: the gap and the track.</summary>
    public const double BarCost = FamilySpace.S2 + FamilyShape.Track;

    /// <summary>[S-1466] The widest readout the config's phases can show ("25:00" reads "88:88"), with its line under.</summary>
    public static ReadoutText Worst(PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var longest = Math.Max(PomodoroCycle.PhaseSeconds(PomodoroPhase.Work, config), PomodoroCycle.PhaseSeconds(PomodoroPhase.Break, config));
        var digits = new string([.. PomodoroCycle.Clock(longest).Select(c => char.IsDigit(c) ? '8' : c)]);
        return new ReadoutText(digits, LineUnder: true, OneReadout: true);
    }

    /// <summary>[S-1466] The plan for <paramref name="config"/>'s style in a view of <paramref name="area"/> (the card less its header).</summary>
    public static PomodoroPlan Plan(PomodoroConfig config, WidgetArea area)
    {
        var worst = Worst(config);
        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);

        if (config.DrawsRing)
        {
            var ring = TimerFace.Ring(area, width, height, worst, controls: true);
            if (ring.Ring > 0)
            {
                return new PomodoroPlan(PomodoroLook.Ring, ring.Ring, ring.Step, ring.Under, ring.Controls);
            }
        }

        if (config.DrawsBar)
        {
            // The controls the tile has room for come before the bar: the bar is drawn only where both fit.
            var controls = TimerFace.ControlsFit(height, worst);
            var least = ReadoutRules.Height(FamilyType.Big28, worst with { LineUnder = false }) + BarCost + (controls ? TimerFace.ControlsCost : 0);
            if (least <= height)
            {
                var barred = TimerFace.Plan(area, width, height - BarCost, worst, controls: true);
                if (barred.Controls == controls)
                {
                    return new PomodoroPlan(PomodoroLook.Bar, 0, barred.Step, barred.Under, barred.Controls);
                }
            }
        }

        var plain = TimerFace.Plan(area, width, height, worst, controls: true);
        return new PomodoroPlan(PomodoroLook.Digits, 0, plain.Step, plain.Under, plain.Controls);
    }
}
