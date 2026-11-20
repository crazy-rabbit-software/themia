using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Pomodoro;

namespace Themia.Core.Widgets;

/// <summary>[S-1466] What a pomodoro draws round its time: the sectioned ring with the time in it, the sectioned bar, or the digits alone.</summary>
public enum PomodoroLook
{
    Ring,
    Bar,
    Digits,
}

/// <summary>
/// [S-8117] How the face's parts stand in the card: stacked in a tall card (the face, its caption, the bar, the
/// controls at the foot), side by side in a wide one (the face on the left, the caption, the bar and the controls in
/// a column on its right).
/// </summary>
public enum PomodoroArrangement
{
    Tall,
    Wide,
}

/// <summary>
/// [S-8117] The pomodoro's face as planned: its look, the time's step stacked and side by side, the ring's diameter (0 off
/// the Ring style) and the block's natural size in each arrangement, from its first ink to its last (the card's inset not
/// included).
/// </summary>
public readonly record struct PomodoroPlan(PomodoroLook Look, TypeStep Step, TypeStep WideStep, double Ring, SizeDip Tall, SizeDip Wide)
{
    /// <summary>The time's step in <paramref name="arrangement"/>.</summary>
    public TypeStep StepIn(PomodoroArrangement arrangement) => arrangement == PomodoroArrangement.Wide ? WideStep : Step;
}

/// <summary>[S-14281] The block's parts as they stand: its height and each part's top in it, before the scale.</summary>
public readonly record struct PomodoroStand(double Height, double Face, double Caption, double Bar, double Controls);

/// <summary>
/// [S-1466] [S-8117] The pomodoro's face (owner, 2026-11-20: "the pomodoro timer should be designed more fluently not all
/// in just one column. especially the bar style."; "the size should not be settable, it is set by the size of the
/// widget."): the time left as the Readout (on 68 stacked, 96 side by side; in the ring on 40), the caption (the phase and its place in the
/// cycle, [S-8116]), the sectioned bar on the Bar style ([S-8115]) and the Controls row (Start or Pause worded, −, +,
/// Reset, Skip), planned once and scaled as one block to the card ([S-12808], [S-12833]); the card's aspect picks
/// whether they stand stacked or side by side (<see cref="Choose"/>). No Size setting: the card is the size.
/// </summary>
public static class PomodoroFace
{
    /// <summary>[S-8117] From the face to the column beside it in a wide card.</summary>
    public const double ColumnGap = FamilySpace.S6;

    /// <summary>The room a face is given past its own size, so a box rounded to the device's pixels still holds it.</summary>
    public const double Slack = 2;

    /// <summary>[S-12833] The least scale the block is drawn at: its Body text at 9, its 16 glyphs at 12.</summary>
    public static double Least => ScaleFloor.Least(FamilyType.Body.FontSize, FamilyShape.Icon);

    /// <summary>
    /// [S-8117] The time's step: 40 in the ring's hole; else 68 stacked, and 96 side by side, where its capitals stand as
    /// tall as the column beside it (the caption's capitals to the controls' foot).
    /// </summary>
    public static TypeStep StepOf(PomodoroLook look, PomodoroArrangement arrangement)
        => look == PomodoroLook.Ring ? FamilyType.Big40 : arrangement == PomodoroArrangement.Wide ? FamilyType.Hero96 : FamilyType.Hero68;

    /// <summary>The look a style draws.</summary>
    public static PomodoroLook LookOf(PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.DrawsRing ? PomodoroLook.Ring : config.DrawsBar ? PomodoroLook.Bar : PomodoroLook.Digits;
    }

    /// <summary>[S-1466] The widest time the config's phases can show ("25:00" reads "88:88"), so neither a tick nor a new phase moves it.</summary>
    public static ReadoutText Worst(PomodoroConfig config)
    {
        var longest = PomodoroCycle.Lengths(config).DefaultIfEmpty(0).Max();
        var digits = new string([.. PomodoroCycle.Clock(longest).Select(c => char.IsDigit(c) ? '8' : c)]);
        return new ReadoutText(digits, OneReadout: true);
    }

    /// <summary>[S-8116] The longest caption the cycle can show, so a new phase never moves the block.</summary>
    public static string WorstCaption(PomodoroConfig config, Locale locale)
    {
        var phases = PomodoroCycle.Phases(config);
        return Enumerable.Range(0, phases.Count).Select(i => PomodoroCycle.Caption(locale, phases, i)).MaxBy(c => c.Length) ?? string.Empty;
    }

    /// <summary>
    /// [S-8117] The least ring (in fours) whose hole (<see cref="ChartRules.Hole"/>) holds <paramref name="worst"/> on
    /// <paramref name="step"/>; never under the family's largest icon.
    /// </summary>
    public static double RingOf(TypeStep step, ReadoutText worst)
    {
        var need = Math.Max(ReadoutRules.Width(step, worst) / 0.75, 2 * ReadoutRules.Height(step, worst)) + (2 * FamilyShape.Stroke);
        return Math.Max(FamilyShape.IconHuge, Math.Ceiling(need / 4) * 4);
    }

    /// <summary>[S-8117] The Controls row's width: Start (or Pause, the longer) worded, then −, +, Reset and Skip as icon buttons, 8 apart.</summary>
    public static double ControlsWidth(Locale locale)
    {
        var main = new[] { PomodoroStr.WidgetsC_Pomodoro_Start.Text(locale), PomodoroStr.WidgetsC_Pomodoro_Pause.Text(locale) }.MaxBy(l => l.Length) ?? string.Empty;
        return TimerFace.WordedWidth(main) + (4 * FamilyShape.IconButton) + (4 * ControlsRules.Gap);
    }

    /// <summary>
    /// [S-8117] The block's natural size in each arrangement, from its first ink to its last. Stacked: the face, its
    /// caption under it (8 under a ring; right under the time, as a readout's line under), the bar 8 under that, the
    /// controls 8 under. Side by side: the face, 24, and a column of the caption, the bar 8 under it and the controls 8
    /// under that; the face's ink and the column's (the caption's capitals to the controls' foot) stand as one band, as
    /// tall as the taller, the shorter centred on it.
    /// </summary>
    public static PomodoroPlan Plan(PomodoroConfig config, Locale locale)
    {
        var look = LookOf(config);
        var worst = Worst(config);
        var (step, wideStep) = (StepOf(look, PomodoroArrangement.Tall), StepOf(look, PomodoroArrangement.Wide));
        var ring = look == PomodoroLook.Ring ? RingOf(step, worst) : 0;
        var caption = FamilyType.EstimateWidth(FamilyType.Body, WorstCaption(config, locale).Length);
        var bar = look == PomodoroLook.Bar ? FamilySpace.S2 + FamilyShape.Track : 0;
        var controls = ControlsWidth(locale);
        var under = FamilySpace.S2 + FamilyShape.Button;

        var time = LineInk.Of(step);
        var (faceW, faceH, faceOver) = ring > 0 ? (ring, ring, 0.0) : (ReadoutRules.Width(step, worst), step.LineHeight, time.Over);
        var tall = new SizeDip(
            Math.Max(faceW, Math.Max(caption, controls)),
            faceH - faceOver + (ring > 0 ? FamilySpace.S2 : 0) + FamilyType.BodyLine + bar + under);

        var wideTime = LineInk.Of(wideStep);
        var (wideW, wideInk) = ring > 0 ? (ring, ring) : (ReadoutRules.Width(wideStep, worst), wideStep.LineHeight - wideTime.Over - wideTime.Under);
        var column = FamilyType.BodyLine + bar + under - LineInk.Of(FamilyType.Body).Over;
        var wide = new SizeDip(wideW + ColumnGap + Math.Max(caption, controls), Math.Max(wideInk, column));
        return new PomodoroPlan(look, step, wideStep, ring, tall, wide);
    }

    /// <summary>
    /// [S-14281] Where the block's parts stand (WR-7, owner 2026-11-19: "Keep them together"; 2026-11-20: "the pomodoro
    /// timer should be designed more fluently not all in just one column"), each part's top in the block and the block's
    /// height, before the scale. Stacked: the face, the caption <paramref name="captionGap"/> under it, the bar 8 under
    /// that (a <paramref name="bar"/> of 0: none), the controls 8 under the last, each right under the one before.
    /// Side by side: the face's ink (<paramref name="faceInk"/> inside its box of <paramref name="face"/>) and the
    /// column's (the caption's capitals, <paramref name="captionOver"/> under its line's top, to the controls' foot) are
    /// one band as tall as the taller, the shorter centred on it; the column's parts right under one another, never
    /// spread over the band. The block's box starts at the caption's line.
    /// </summary>
    public static PomodoroStand Stand(
        PomodoroArrangement arrangement, double face, LineInk faceInk, double caption, double captionOver, double bar, double controls, double captionGap = 0)
    {
        var barPart = bar > 0 ? FamilySpace.S2 + bar : 0;
        if (arrangement == PomodoroArrangement.Tall)
        {
            var under = face + captionGap;
            var controlsTop = under + caption + barPart + FamilySpace.S2;
            return new PomodoroStand(controlsTop + controls, 0, under, under + caption + FamilySpace.S2, controlsTop);
        }

        var faceSpan = Math.Max(0, face - faceInk.Over - faceInk.Under);
        var columnSpan = caption + barPart + FamilySpace.S2 + controls - captionOver;
        var band = Math.Max(faceSpan, columnSpan);
        var top = (band - columnSpan) / 2;
        return new PomodoroStand(
            captionOver + band,
            captionOver + ((band - faceSpan) / 2) - faceInk.Over,
            top,
            top + caption + FamilySpace.S2,
            top + caption + barPart + FamilySpace.S2);
    }

    /// <summary>
    /// [S-8117] Which arrangement a room <paramref name="width"/> × <paramref name="height"/> (the card less its inset)
    /// takes (owner, 2026-11-20: "not all in just one column"): side by side where the room is wider than the geometric
    /// mean of the two arrangements' aspects, stacked where it is not; but the other where the chosen one would fall
    /// under the least scale and the other would not fall as far.
    /// </summary>
    public static PomodoroArrangement Choose(double width, double height, PomodoroPlan plan)
    {
        if (!(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height) || !Usable(plan.Tall) || !Usable(plan.Wide))
        {
            return PomodoroArrangement.Wide;
        }

        var threshold = Math.Sqrt(plan.Tall.Width / plan.Tall.Height * (plan.Wide.Width / plan.Wide.Height));
        var preferred = width / height >= threshold ? PomodoroArrangement.Wide : PomodoroArrangement.Tall;
        var other = preferred == PomodoroArrangement.Wide ? PomodoroArrangement.Tall : PomodoroArrangement.Wide;
        var (mine, theirs) = (Scale(width, height, plan, preferred), Scale(width, height, plan, other));
        return mine < Least - 1e-9 && theirs > mine ? other : preferred;
    }

    /// <summary>[S-8117] The scale the block fills a room at in <paramref name="arrangement"/>: the smaller of the two axes'.</summary>
    public static double Scale(double width, double height, PomodoroPlan plan, PomodoroArrangement arrangement)
    {
        var size = arrangement == PomodoroArrangement.Wide ? plan.Wide : plan.Tall;
        return Usable(size) ? Math.Min(width / size.Width, height / size.Height) : 1;
    }

    /// <summary>
    /// [S-8117] [S-12833] The pomodoro's sizing ([S-2438]): a new card is the side-by-side block in its inset, and no
    /// shorter than the least card; the least card is the stacked block scaled down to its floor (WR-14), so every card
    /// it may be made holds one arrangement at a scale it can be read at. The descriptor relaxes the maximum ([S-12411]).
    /// </summary>
    public static WidgetSizing Sizing(PomodoroConfig config, Locale locale)
    {
        var plan = Plan(config, locale);
        var wide = Box(plan.Wide);

        // The block scales; the inset round it does not.
        var least = Box(new SizeDip(plan.Tall.Width * Least, plan.Tall.Height * Least));
        var min = new SizeDip(Math.Max(WidgetSizing.LeastScaled, least.Width), Math.Max(WidgetSizing.LeastScaled, least.Height));
        var natural = new SizeDip(Math.Max(wide.Width, min.Width), Math.Max(wide.Height, min.Height));
        return WidgetSizing.Range(min, natural, natural);
    }

    /// <summary>A block's card: the block in the inset on every side, and the slack.</summary>
    public static SizeDip Box(SizeDip block)
        => new(Math.Ceiling(block.Width) + (2 * FamilySpace.Inset) + Slack, Math.Ceiling(block.Height) + (2 * FamilySpace.Inset) + Slack);

    private static bool Usable(SizeDip size) => size.Width > 0 && size.Height > 0 && double.IsFinite(size.Width) && double.IsFinite(size.Height);
}
