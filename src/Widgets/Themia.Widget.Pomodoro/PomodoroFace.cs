using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Pomodoro;

namespace Themia.Core.Widgets;

/// <summary>[S-1466] What a pomodoro draws round its time: the sectioned ring with everything in it, the sectioned bar, or the digits alone.</summary>
public enum PomodoroLook
{
    Ring,
    Bar,
    Digits,
}

/// <summary>
/// [S-8117] [S-16603] How the face's parts stand in the card, chosen by the card's shape (<see cref="PomodoroFace.Choose"/>).
/// </summary>
public enum PomodoroArrangement
{
    /// <summary>[S-16601] The Ring style's one: the ring as large as the card's smaller side, the time, the caption and the controls inside it.</summary>
    Ring,

    /// <summary>The time, the caption, the bar across (on the Bar style) and the controls, each right under the one before, centred.</summary>
    Stacked,

    /// <summary>The time on the left, the caption over the controls beside it; on the Bar style the bar across under them both.</summary>
    Side,

    /// <summary>[S-16604] The Bar style in a card taller than wide: the bar upright on the left, as tall as the card, the time, the caption and the controls stacked beside it.</summary>
    Column,
}

/// <summary>A part's box in the room, in DIPs: where its natural box stands and how large it is drawn (its natural size times the scale).</summary>
public readonly record struct PomodoroRect(double X, double Y, double Width, double Height)
{
    public static PomodoroRect None => default;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool IsEmpty => !(Width > 0) || !(Height > 0);
}

/// <summary>
/// [S-16600] The face's parts at their own size (scale 1, as measured or as estimated for the sizing): the time's box (held to
/// its widest reading) and the room in it over and under its figures, the caption's box (its longest) and its room over its
/// capitals, and the Controls row's two boxes: the main action (Start or Pause, held to the wider) and the four icon
/// buttons (−, +, Reset, Skip), 8 apart in a row; upright beside the bar the icons stand 8 under the main action ([S-16608]).
/// </summary>
public readonly record struct PomodoroParts(SizeDip Time, LineInk TimeInk, SizeDip Caption, LineInk CaptionInk, SizeDip Main, SizeDip Tools)
{
    /// <summary>The Controls row: the main action, 8, the icon buttons.</summary>
    public SizeDip Controls => new(Main.Width + ControlsRules.Gap + Tools.Width, Math.Max(Main.Height, Tools.Height));
}

/// <summary>[S-8117] The pomodoro's face as planned: its look, the time's step, its parts as estimated and the ring's diameter (0 off the Ring style).</summary>
public readonly record struct PomodoroPlan(PomodoroLook Look, TypeStep Step, PomodoroParts Parts, double Ring);

/// <summary>
/// [S-16603] Where every part stands in a room: the arrangement, the scale the parts are drawn at, and each part's box (its
/// natural box at <see cref="PomodoroRect.X"/>, <see cref="PomodoroRect.Y"/> drawn at the scale; none: not drawn). The bar
/// is laid at its drawn size (its length the room's, its thickness the track's at the scale), the ring at its drawn diameter.
/// </summary>
public readonly record struct PomodoroLayout(
    PomodoroArrangement Arrangement, double Scale, PomodoroRect Ring, PomodoroRect Time, PomodoroRect Caption, PomodoroRect Bar, PomodoroRect Main, PomodoroRect Tools)
{
    /// <summary>The Controls' box: the main action's and the icon buttons' together.</summary>
    public PomodoroRect Controls => Main.IsEmpty ? Tools : Tools.IsEmpty ? Main
        : new PomodoroRect(Math.Min(Main.X, Tools.X), Math.Min(Main.Y, Tools.Y), Math.Max(Main.Right, Tools.Right) - Math.Min(Main.X, Tools.X), Math.Max(Main.Bottom, Tools.Bottom) - Math.Min(Main.Y, Tools.Y));

    /// <summary>Whether the bar stands upright (<see cref="PomodoroArrangement.Column"/>).</summary>
    public bool BarUpright => Arrangement == PomodoroArrangement.Column;
}

/// <summary>
/// [S-1466] [S-8117] The pomodoro's face (owner, 2026-11-20: "the pomodoro timer should be designed more fluently not all
/// in just one column. especially the bar style."; 2026-11-23: "the pomodoro timer should generally show the time smaller"):
/// the time left as the Readout on <see cref="TimeStep"/>, the caption (the phase and its place in the cycle, [S-8116]), the
/// sectioned progress ([S-8115]) and the Controls row (Start or Pause worded, −, +, Reset, Skip), planned once at their own
/// size and laid in the card by one pure rule (<see cref="Lay"/>): the card's shape picks the arrangement
/// (<see cref="Choose"/>), the parts scale as one to fill it, the ring stands as large as the card's smaller side with
/// everything inside it, and the bar runs the card's whole length. No Size setting: the card is the size.
/// </summary>
public static class PomodoroFace
{
    /// <summary>[S-8117] From the time to the column beside it in a wide card.</summary>
    public const double ColumnGap = FamilySpace.S6;

    /// <summary>[S-16604] From the upright bar to the parts beside it.</summary>
    public const double BarGap = FamilySpace.S4;

    /// <summary>[S-16602] The room between the ring's inner edge and the parts inside it.</summary>
    public const double RingPad = FamilySpace.S2;

    /// <summary>
    /// [S-16600] The time's step in every style (owner, 2026-11-23: "the pomodoro timer should generally show the time
    /// smaller"): the larger Readout step, 40, never a Hero step (it was 68 stacked and 96 side by side), so it stands
    /// at <see cref="TimeRatio"/> of the caption and the controls' Body text and never dominates the card.
    /// </summary>
    public static TypeStep TimeStep => FamilyType.Big40;

    /// <summary>[S-16600] The most the time's size is of the caption's and the controls' Body text: 3 (40 over 14).</summary>
    public const double TimeRatio = 3;

    /// <summary>[S-12833] The least scale the face is drawn at: its Body text at 9, its 16 glyphs at 12.</summary>
    public static double Least => ScaleFloor.Least(FamilyType.Body.FontSize, FamilyShape.Icon);

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

    /// <summary>[S-8117] The Controls row's width: Start (or Pause, the longer) worded, then −, +, Reset and Skip as icon buttons, 8 apart.</summary>
    public static double ControlsWidth(Locale locale)
        => TimerFace.WordedWidth(MainLabel(locale)) + (4 * FamilyShape.IconButton) + (4 * ControlsRules.Gap);

    /// <summary>[S-16608] The main action's labels in <paramref name="locale"/>: Start and Pause, the one button held to the wider.</summary>
    public static IReadOnlyList<string> MainLabels(Locale locale)
        => [PomodoroStr.WidgetsC_Pomodoro_Start.Text(locale), PomodoroStr.WidgetsC_Pomodoro_Pause.Text(locale)];

    private static string MainLabel(Locale locale) => MainLabels(locale).MaxBy(l => l.Length) ?? string.Empty;

    /// <summary>[S-16600] The parts as the family's model sizes them (the sizing's; a view measures its own).</summary>
    public static PomodoroParts Estimate(PomodoroConfig config, Locale locale)
    {
        var worst = Worst(config);
        return new PomodoroParts(
            new SizeDip(ReadoutRules.Width(TimeStep, worst), TimeStep.LineHeight),
            LineInk.Of(TimeStep),
            new SizeDip(FamilyType.EstimateWidth(FamilyType.Body, WorstCaption(config, locale).Length), FamilyType.BodyLine),
            LineInk.Of(FamilyType.Body),
            new SizeDip(TimerFace.WordedWidth(MainLabel(locale)), FamilyShape.Button),
            new SizeDip((4 * FamilyShape.IconButton) + (3 * ControlsRules.Gap), FamilyShape.IconButton));
    }

    /// <summary>[S-8117] The face for a config: its look, the time's step, its parts as estimated and, on the Ring style, the ring that holds them.</summary>
    public static PomodoroPlan Plan(PomodoroConfig config, Locale locale)
    {
        var look = LookOf(config);
        var parts = Estimate(config, locale);
        return new PomodoroPlan(look, TimeStep, parts, look == PomodoroLook.Ring ? RingOf(parts) : 0);
    }

    // ---- the ring ([S-16601], [S-16602]) -------------------------------------------------------------------------

    /// <summary>One line of the parts inside the ring: its top and foot (from the time's capitals) and its width, centred.</summary>
    public readonly record struct RingRow(double Top, double Bottom, double Width);

    /// <summary>
    /// [S-16602] The parts inside the ring as lines of ink, top to bottom, from the time's capitals: the time (its figures'
    /// ink, as wide as its held box), the caption right under the time's line (its capitals to its line's foot), the
    /// controls 8 under it (their whole row).
    /// </summary>
    public static IReadOnlyList<RingRow> RingRows(PomodoroParts parts)
    {
        var timeInk = Math.Max(0, parts.Time.Height - parts.TimeInk.Over - parts.TimeInk.Under);
        var caption = timeInk + parts.TimeInk.Under;
        var controls = caption + parts.Caption.Height + FamilySpace.S2;
        return
        [
            new RingRow(0, timeInk, parts.Time.Width),
            new RingRow(caption + parts.CaptionInk.Over, caption + parts.Caption.Height, parts.Caption.Width),
            new RingRow(controls, controls + parts.Controls.Height, parts.Controls.Width),
        ];
    }

    /// <summary>
    /// [S-16602] The circle round <paramref name="rows"/> stacked and each centred across, their ink centred on its middle
    /// down too (a dial: as much ring over the time as under the controls): its radius, the farthest row corner's distance,
    /// and where the rows' top stands from its centre (negative: above it). Every row's corners stand on or inside it.
    /// </summary>
    public static (double Radius, double Top) Circle(IReadOnlyList<RingRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return (0, 0);
        }

        var (first, last) = (rows.Min(r => r.Top), rows.Max(r => r.Bottom));
        var shift = -(first + last) / 2;
        var far = 0.0;
        foreach (var row in rows)
        {
            var half = row.Width / 2;
            var y = Math.Max(Math.Abs(row.Top + shift), Math.Abs(row.Bottom + shift));
            far = Math.Max(far, (half * half) + (y * y));
        }

        return (Math.Sqrt(far), first + shift);
    }

    /// <summary>
    /// [S-16602] The ring's diameter (in fours) whose hole, less <see cref="RingPad"/>, holds the parts by
    /// <see cref="Circle"/>: the ring's line inks from its edge inwards by its stroke.
    /// </summary>
    public static double RingOf(PomodoroParts parts)
    {
        var (radius, _) = Circle(RingRows(parts));
        return Math.Max(FamilyShape.IconHuge, Math.Ceiling(2 * (radius + RingPad + FamilyShape.Stroke) / 4) * 4);
    }

    // ---- the arrangements ([S-16603]) ----------------------------------------------------------------------------

    /// <summary>
    /// The block of an arrangement at scale 1, from its first ink: its size and each part's natural box in it (the bar's
    /// box across or up the block; in the room it runs the room's whole length).
    /// </summary>
    private readonly record struct Block(SizeDip Size, PomodoroRect Ring, PomodoroRect Time, PomodoroRect Caption, PomodoroRect Bar, PomodoroRect Main, PomodoroRect Tools)
    {
        /// <summary>A block whose Controls stand in one row from (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public static Block Row(SizeDip size, PomodoroRect ring, PomodoroRect time, PomodoroRect caption, PomodoroRect bar, PomodoroParts parts, double x, double y)
            => new(size, ring, time, caption, bar,
                new PomodoroRect(x, y, parts.Main.Width, parts.Main.Height),
                new PomodoroRect(x + parts.Main.Width + ControlsRules.Gap, y, parts.Tools.Width, parts.Tools.Height));
    }

    /// <summary>
    /// [S-16603] An arrangement's block at scale 1 (from its first ink to its last; the bar's length not counted, it runs the
    /// room's): its width and height.
    /// </summary>
    public static SizeDip BlockOf(PomodoroArrangement arrangement, PomodoroLook look, PomodoroParts parts) => Stand(arrangement, look, parts).Size;

    private static Block Stand(PomodoroArrangement arrangement, PomodoroLook look, PomodoroParts parts)
    {
        var bar = look == PomodoroLook.Bar;
        var (time, caption, controls) = (parts.Time, parts.Caption, parts.Controls);
        var (main, tools) = (parts.Main, parts.Tools);
        var (tOver, tUnder) = (parts.TimeInk.Over, parts.TimeInk.Under);
        var timeInk = Math.Max(0, time.Height - tOver - tUnder);
        switch (arrangement)
        {
            case PomodoroArrangement.Ring:
            {
                var d = RingOf(parts);
                var rows = RingRows(parts);
                var (_, top) = Circle(rows);
                var y = (d / 2) + top;
                var captionTop = y + rows[1].Top - parts.CaptionInk.Over;
                return Block.Row(
                    new SizeDip(d, d),
                    new PomodoroRect(0, 0, d, d),
                    new PomodoroRect((d - time.Width) / 2, y - tOver, time.Width, time.Height),
                    new PomodoroRect((d - caption.Width) / 2, captionTop, caption.Width, caption.Height),
                    PomodoroRect.None,
                    parts,
                    (d - controls.Width) / 2,
                    y + rows[2].Top);
            }

            case PomodoroArrangement.Side:
            {
                // The time's figures and the column's ink (the caption's capitals to the controls' foot) are one band.
                var column = Math.Max(caption.Width, controls.Width);
                var columnInk = caption.Height - parts.CaptionInk.Over + FamilySpace.S2 + controls.Height;
                var band = Math.Max(timeInk, columnInk);
                var columnTop = ((band - columnInk) / 2) - parts.CaptionInk.Over;
                var left = time.Width + ColumnGap;
                var width = left + column;
                return Block.Row(
                    new SizeDip(width, band + (bar ? FamilySpace.S2 + FamilyShape.Track : 0)),
                    PomodoroRect.None,
                    new PomodoroRect(0, ((band - timeInk) / 2) - tOver, time.Width, time.Height),
                    new PomodoroRect(left, columnTop, caption.Width, caption.Height),
                    bar ? new PomodoroRect(0, band + FamilySpace.S2, width, FamilyShape.Track) : PomodoroRect.None,
                    parts,
                    left,
                    columnTop + caption.Height + FamilySpace.S2);
            }

            case PomodoroArrangement.Column:
            {
                // [S-16608] Upright, the icon buttons stand 8 under the main action: the stack is as narrow as its widest
                // line, so it fills more of a tall card beside the bar.
                var left = FamilyShape.Track + BarGap;
                var captionTop = timeInk + tUnder;
                var mainTop = captionTop + caption.Height + FamilySpace.S2;
                var toolsTop = mainTop + main.Height + FamilySpace.S2;
                var height = toolsTop + tools.Height;
                var widest = new[] { time.Width, caption.Width, main.Width, tools.Width }.Max();
                return new Block(
                    new SizeDip(left + widest, height),
                    PomodoroRect.None,
                    new PomodoroRect(left, -tOver, time.Width, time.Height),
                    new PomodoroRect(left, captionTop, caption.Width, caption.Height),
                    new PomodoroRect(0, 0, FamilyShape.Track, height),
                    new PomodoroRect(left, mainTop, main.Width, main.Height),
                    new PomodoroRect(left, toolsTop, tools.Width, tools.Height));
            }

            default:
            {
                var width = Math.Max(time.Width, Math.Max(caption.Width, controls.Width));
                var captionTop = timeInk + tUnder;
                var barTop = captionTop + caption.Height + FamilySpace.S2;
                var controlsTop = (bar ? barTop + FamilyShape.Track : captionTop + caption.Height) + FamilySpace.S2;
                return Block.Row(
                    new SizeDip(width, controlsTop + controls.Height),
                    PomodoroRect.None,
                    new PomodoroRect((width - time.Width) / 2, -tOver, time.Width, time.Height),
                    new PomodoroRect((width - caption.Width) / 2, captionTop, caption.Width, caption.Height),
                    bar ? new PomodoroRect(0, barTop, width, FamilyShape.Track) : PomodoroRect.None,
                    parts,
                    (width - controls.Width) / 2,
                    controlsTop);
            }
        }
    }

    /// <summary>[S-16603] The arrangements a look may take: the Ring style its ring; the Bar style stacked, side by side or upright beside its bar; Digits stacked or side by side.</summary>
    public static IReadOnlyList<PomodoroArrangement> Arrangements(PomodoroLook look) => look switch
    {
        PomodoroLook.Ring => [PomodoroArrangement.Ring],
        PomodoroLook.Bar => [PomodoroArrangement.Column, PomodoroArrangement.Stacked, PomodoroArrangement.Side],
        _ => [PomodoroArrangement.Stacked, PomodoroArrangement.Side],
    };

    /// <summary>
    /// [S-8117] [S-16603] Which arrangement a room <paramref name="width"/> × <paramref name="height"/> (the card less its
    /// inset) takes (owner, 2026-11-23: "right now all the components are placed underneath each other but they can also be
    /// placed side by side. (the progress bar would then be vertical for example)"): the Ring style its ring; the Bar style
    /// upright beside its bar where the room is taller than wide; else stacked or side by side by the room's aspect, side by
    /// side where it is wider than the geometric mean of the two blocks' aspects. Where the one chosen would fall under the
    /// least scale and another would not fall as far, that other. An unmeasured room stands side by side, as a new card does.
    /// </summary>
    public static PomodoroArrangement Choose(PomodoroLook look, PomodoroParts parts, double width, double height)
    {
        if (look == PomodoroLook.Ring)
        {
            return PomodoroArrangement.Ring;
        }

        var (stacked, side) = (BlockOf(PomodoroArrangement.Stacked, look, parts), BlockOf(PomodoroArrangement.Side, look, parts));
        if (!(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height) || !Usable(stacked) || !Usable(side))
        {
            return PomodoroArrangement.Side;
        }

        var threshold = Math.Sqrt(stacked.Width / stacked.Height * (side.Width / side.Height));
        var preferred = look == PomodoroLook.Bar && height > width ? PomodoroArrangement.Column
            : width / height >= threshold ? PomodoroArrangement.Side : PomodoroArrangement.Stacked;
        var mine = Scale(preferred, look, parts, width, height);
        if (mine >= Least - 1e-9)
        {
            return preferred;
        }

        var best = Arrangements(look).MaxBy(a => Scale(a, look, parts, width, height));
        return Scale(best, look, parts, width, height) > mine ? best : preferred;
    }

    /// <summary>
    /// [S-16603] The scale <paramref name="arrangement"/>'s block fills a room at: the smaller of the two axes' (the bar's
    /// length is the room's, so it never limits it); the ring by the room's smaller side.
    /// </summary>
    public static double Scale(PomodoroArrangement arrangement, PomodoroLook look, PomodoroParts parts, double width, double height)
    {
        var size = BlockOf(arrangement, look, parts);
        return Usable(size) ? Math.Min(width / size.Width, height / size.Height) : 1;
    }

    /// <summary>
    /// [S-16601] [S-16603] [S-16604] Every part's box in a room <paramref name="width"/> × <paramref name="height"/>: the
    /// arrangement <see cref="Choose"/> picks, its block scaled as one until one axis stops it (WR-7: the reading, its caption
    /// and its controls scale as one) and centred on the other ("Centred is fine"), the ring as large as the room's smaller
    /// side with every part inside its hole, the bar across the room's whole width (stacked, side by side) or up its whole
    /// height (upright), its thickness the track's at the scale.
    /// </summary>
    public static PomodoroLayout Lay(PomodoroLook look, PomodoroParts parts, double width, double height)
    {
        var arrangement = Choose(look, parts, width, height);
        var block = Stand(arrangement, look, parts);
        if (!Usable(block.Size) || !(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return new PomodoroLayout(arrangement, 1, PomodoroRect.None, PomodoroRect.None, PomodoroRect.None, PomodoroRect.None, PomodoroRect.None, PomodoroRect.None);
        }

        var scale = Math.Min(width / block.Size.Width, height / block.Size.Height);
        var (x, y) = ((width - (block.Size.Width * scale)) / 2, (height - (block.Size.Height * scale)) / 2);
        PomodoroRect At(PomodoroRect r) => r.IsEmpty ? PomodoroRect.None : new PomodoroRect(x + (r.X * scale), y + (r.Y * scale), r.Width * scale, r.Height * scale);

        var bar = block.Bar.IsEmpty ? PomodoroRect.None
            : arrangement == PomodoroArrangement.Column ? new PomodoroRect(x, 0, FamilyShape.Track * scale, height)
            : new PomodoroRect(0, y + (block.Bar.Y * scale), width, FamilyShape.Track * scale);
        return new PomodoroLayout(arrangement, scale, At(block.Ring), At(block.Time), At(block.Caption), bar, At(block.Main), At(block.Tools));
    }

    /// <summary>
    /// [S-8117] [S-12833] [S-16606] The pomodoro's sizing ([S-2438]): the least card is the stacked block (the ring) scaled
    /// down to its floor (WR-14), so every card it may be made holds an arrangement at a scale it can be read at; a new card
    /// is the side-by-side block (the ring) in its inset, grown as one where the least card is taller, so it keeps the
    /// block's own shape and the block fills it (a side-by-side block raised to the least height alone stood with a band
    /// over and under it). The descriptor relaxes the maximum ([S-12411]).
    /// </summary>
    public static WidgetSizing Sizing(PomodoroConfig config, Locale locale)
    {
        var plan = Plan(config, locale);
        var (natural, least) = plan.Look == PomodoroLook.Ring
            ? (BlockOf(PomodoroArrangement.Ring, plan.Look, plan.Parts), BlockOf(PomodoroArrangement.Ring, plan.Look, plan.Parts))
            : (BlockOf(PomodoroArrangement.Side, plan.Look, plan.Parts), BlockOf(PomodoroArrangement.Stacked, plan.Look, plan.Parts));

        // The block scales; the inset round it does not.
        var floor = Box(new SizeDip(least.Width * Least, least.Height * Least));
        var min = new SizeDip(Math.Max(WidgetSizing.LeastScaled, floor.Width), Math.Max(WidgetSizing.LeastScaled, floor.Height));
        var inset = (2 * FamilySpace.Inset) + LayoutMath.Slack;
        var grow = natural.Height > 0 ? Math.Max(1, (min.Height - inset) / natural.Height) : 1;
        var box = Box(new SizeDip(natural.Width * grow, natural.Height * grow));
        var nat = new SizeDip(Math.Max(box.Width, min.Width), Math.Max(box.Height, min.Height));
        return WidgetSizing.Range(min, nat, nat);
    }

    /// <summary>A block's card: the block in the inset on every side, and the slack.</summary>
    public static SizeDip Box(SizeDip block)
        => new(Math.Ceiling(block.Width) + (2 * FamilySpace.Inset) + LayoutMath.Slack, Math.Ceiling(block.Height) + (2 * FamilySpace.Inset) + LayoutMath.Slack);

    private static bool Usable(SizeDip size) => size.Width > 0 && size.Height > 0 && double.IsFinite(size.Width) && double.IsFinite(size.Height);
}
