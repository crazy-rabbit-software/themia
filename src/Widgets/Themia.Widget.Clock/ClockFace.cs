using Themia.Core.Localization;
using Themia.Sdk;
using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-6741] The digital readout as planned: its step, whether it stacks the hours over the minutes, whether the meridiem and the seconds are drawn, and whether its line under shows.</summary>
public readonly record struct ClockReadoutPlan(TypeStep Step, bool Stacked, bool Meridiem, bool Seconds, bool Under)
{
    /// <summary>How many lines the value takes: one, or the hours over the minutes (over the seconds).</summary>
    public int Lines => Stacked ? (Seconds ? 3 : 2) : 1;
}

/// <summary>[S-6743] The analog dial as planned: its diameter and whether its line stands under it.</summary>
public readonly record struct ClockDialPlan(double Dial, bool Line);

/// <summary>
/// The clock's faces on the family's steps (one-readout, [S-6737]): the family's one-readout step, which its
/// sizing made the card's natural size for (no Size setting: the card's size is the user's, [S-2449] retired),
/// measured in the content box (the card less its inset of 12, the clock has no header). The box never changes what
/// the face holds ([S-11800]).
/// </summary>
public static class ClockFace
{
    /// <summary>[S-6743] Under this a dial is a smear of hairlines; it never draws smaller.</summary>
    public const double DialFloor = 40;

    /// <summary>[S-6743] The least dial that keeps its line under it.</summary>
    public const double DialWithLine = 96;

    /// <summary>[S-6743] The gap between a dial and its line under it.</summary>
    public const double DialGap = FamilySpace.S2;

    /// <summary>A part of the time at its widest: two figures.</summary>
    public const string Figures = "88";

    /// <summary>
    /// [S-6741] The digital, stacked and minimal readout: the value "88:88" (":88" more with seconds) and the
    /// meridiem as its unit, on the largest step whose lines (and the line under) fit. The Stacked style always
    /// sets the hours over the minutes (over the seconds), the meridiem after the last line; the others stack where
    /// that draws a larger step, or where the row is too wide even on the floor step and the stack is not. Where
    /// neither fits the floor step, the meridiem gives way first, then the seconds: the figures are the widget.
    /// </summary>
    public static ClockReadoutPlan Readout(double width, double height, ClockStyle style, bool seconds, string meridiem, bool under)
    {
        var unit = string.IsNullOrEmpty(meridiem) ? string.Empty : " " + meridiem;
        foreach (var (withSeconds, withMeridiem) in Folds(seconds, unit.Length > 0))
        {
            var row = new ReadoutText(withSeconds ? "88:88:88" : "88:88", withMeridiem ? unit : string.Empty, LineUnder: under, OneReadout: true);
            var lines = withSeconds ? 3 : 2;
            var stacked = new ReadoutText(Figures, row.Unit, LineUnder: under, OneReadout: true);
            var rowFits = FitsWidth(FamilyType.Big28, row, width);
            var stackFits = FitsWidth(FamilyType.Big28, stacked, width);
            var last = !withSeconds && !withMeridiem;

            if (style == ClockStyle.Stacked)
            {
                if (stackFits || last)
                {
                    return Plan(StackedStep(width, height, stacked, lines), true, withMeridiem, withSeconds, under, height, lines);
                }

                continue;
            }

            if (!rowFits && !stackFits && !last)
            {
                continue;
            }

            var rowStep = ReadoutRules.Step(width, height, row);
            var stackStep = StackedStep(width, height, stacked, lines);
            var stack = FamilyType.BaseOf(stackStep) > FamilyType.BaseOf(rowStep) || (!rowFits && stackFits);
            return stack
                ? Plan(stackStep, true, withMeridiem, withSeconds, under, height, lines)
                : Plan(rowStep, false, withMeridiem, withSeconds, under, height, 1);
        }

        return new ClockReadoutPlan(FamilyType.Big28, false, false, false, false);
    }

    private static IEnumerable<(bool Seconds, bool Meridiem)> Folds(bool seconds, bool meridiem)
    {
        yield return (seconds, meridiem);
        if (meridiem)
        {
            yield return (seconds, false);
        }

        if (seconds)
        {
            yield return (false, false);
        }
    }

    private static bool FitsWidth(TypeStep step, ReadoutText text, double width) => ReadoutRules.Width(step, text) <= width;

    private static ClockReadoutPlan Plan(TypeStep step, bool stacked, bool meridiem, bool seconds, bool under, double height, int lines)
        => new(step, stacked, meridiem, seconds, under && (lines * step.LineHeight) + FamilyType.Under(step).LineHeight <= height);

    /// <summary>[S-6741] The largest step on which <paramref name="lines"/> lines of the figures, the unit after the last, and the line under fit.</summary>
    public static TypeStep StackedStep(double width, double height, ReadoutText text, int lines)
    {
        var chosen = FamilyType.Big28;
        foreach (var step in ReadoutRules.Steps(oneReadout: true))
        {
            var tall = (lines * step.LineHeight) + (text.LineUnder ? FamilyType.Under(step).LineHeight : 0);
            if (ReadoutRules.Width(step, text) <= width && tall <= height)
            {
                chosen = step;
            }
        }

        return chosen;
    }

    /// <summary>
    /// [S-6742] The Flip style: one card per part of the time ("88"), the meridiem 8 after the last card on
    /// the step's unit, the line under the cards. The cards stand one above the other (8 apart) where that draws
    /// them on a larger step (a tall column), or where a row would not fit even on the floor step. A meridiem that
    /// would not fit beside one floor step's card gives way.
    /// </summary>
    public static (FlipPlan Plan, bool Vertical, bool Meridiem) Flip(double width, double height, int cards, string meridiem, bool under)
    {
        double Beside(TypeStep step) => string.IsNullOrEmpty(meridiem) ? 0 : FamilySpace.S2 + (HeroFit.Ems(meridiem) * FamilyType.Unit(step).FontSize);

        var withMeridiem = !string.IsNullOrEmpty(meridiem)
            && FlipRules.RowWidth(FamilyType.Big28, 1, Figures) + Beside(FamilyType.Big28) <= width;
        Func<TypeStep, double>? beside = withMeridiem ? Beside : null;
        var row = FlipRules.Plan(width, height, cards, Figures, beside, under);

        // One above the other: each card's line and the gaps between them, the line under once.
        var chosen = FamilyType.Big28;
        foreach (var step in ReadoutRules.Steps(oneReadout: true))
        {
            var wide = FlipRules.CardWidth(step, Figures) + (beside?.Invoke(step) ?? 0);
            var tall = (cards * step.LineHeight) + ((cards - 1) * FlipRules.Gap) + (under ? FamilyType.Under(step).LineHeight : 0);
            if (wide <= width && tall <= height)
            {
                chosen = step;
            }
        }

        // A row too wide for even the floor step's cards stands them one above the other where that fits.
        var rowFits = FlipRules.RowWidth(row.Step, cards, Figures) + (beside?.Invoke(row.Step) ?? 0) <= width;
        var columnFits = FlipRules.CardWidth(chosen, Figures) + (beside?.Invoke(chosen) ?? 0) <= width;
        if (FamilyType.BaseOf(chosen) > FamilyType.BaseOf(row.Step) || (!rowFits && columnFits))
        {
            var fits = !under || (cards * chosen.LineHeight) + ((cards - 1) * FlipRules.Gap) + FamilyType.Under(chosen).LineHeight <= height;
            return (new FlipPlan(chosen, FlipRules.CardWidth(chosen, Figures), under && fits), true, withMeridiem);
        }

        return (row, false, withMeridiem);
    }

    /// <summary>
    /// [S-6743] The analog dial: its line under it, 8 apart, while the dial keeps <see cref="DialWithLine"/>,
    /// else the dial alone. Never under <see cref="DialFloor"/>.
    /// </summary>
    public static ClockDialPlan Dial(double width, double height, bool line)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return new ClockDialPlan(0, false);
        }

        var withLine = Math.Floor(Math.Min(width, height - DialGap - FamilyType.Body.LineHeight));
        if (line && withLine >= DialWithLine)
        {
            return new ClockDialPlan(withLine, true);
        }

        return new ClockDialPlan(Math.Floor(Math.Max(DialFloor, Math.Min(width, height))), false);
    }

    /// <summary>The time faces' step: the family's one-readout step, 68 ([S-6702]).</summary>
    public static TypeStep Step => FamilyType.Hero68;

    /// <summary>The analog dial: three times the readout's 48 DIP grid.</summary>
    public const double DialSide = 144;

    /// <summary>[S-6024] The words' step: a sentence stops one step below the readout's.</summary>
    public static TypeStep Words => FamilyType.Big40;

    /// <summary>The words' column: about a dozen characters of its step to a line.</summary>
    public static double WordsWidth => FamilyType.EstimateWidth(Words, 12);

    /// <summary>
    /// The clock's sizing ([S-2438]): its face on the family's one-readout step (68; the words 40; the dial 144), with the
    /// line under it where the face has one, in the card's inset of 12. The time faces (Digital, Minimal, Stacked, Flip)
    /// may be widened to twice their face, the line under cut to what fits, at the one height. A larger box (the relaxed
    /// range, [S-12411]) scales the face up as one to fill it ([S-12804]). <paramref name="meridiem"/> is the widest AM/PM of the language when the region writes
    /// 12-hour time, else empty: the face is as wide as its widest time.
    /// <para>
    /// [S-12833] A smaller box scales it down as one (owner, 2026-11-19: "you should be able to make the clock even smaller";
    /// "the clock has a min height that is way to big"): its least box is its figures (or words) alone at the legibility
    /// floor in the card's inset (<see cref="FaceFloor"/>), the meridiem and the line under having given way on the way
    /// down (the view's fit decides on the face as drawn); the dial's is its floor, <see cref="DialFloor"/>, in the inset.
    /// </para>
    /// </summary>
    public static WidgetSizing Sizing(ClockConfig config, string meridiem)
    {
        ArgumentNullException.ThrowIfNull(config);

        var face = Face(config, meridiem, meridiemShown: true, underShown: true);
        var box = new SizeDip(
            Math.Ceiling(face.Width) + (2 * FamilySpace.Inset) + LayoutMath.Slack,
            Math.Ceiling(face.Height) + (2 * FamilySpace.Inset) + LayoutMath.Slack);
        var widens = config.Style is not (ClockStyle.Analog or ClockStyle.Word);
        var least = config.Style == ClockStyle.Analog
            ? new SizeDip(DialFloor, DialFloor)
            : FaceFloor.LeastBox(PartSizes(config, meridiem), floor => Plan(config, meridiem, floor));
        return WidgetSizing.Range(box, box, widens ? box with { Width = 2 * box.Width } : box)
            .ScalesDownTo(new SizeDip(least.Width + (2 * FamilySpace.Inset), least.Height + (2 * FamilySpace.Inset)));
    }

    /// <summary>
    /// [S-12833] The text sizes of a clock's face (<see cref="FaceFloor"/>): its main reading (the figures on their step, the
    /// words on theirs) and the smaller parts it draws (the meridiem on the step's unit, the line under on the step's
    /// line under). The dial is no text face: none.
    /// </summary>
    public static IReadOnlyList<double> PartSizes(ClockConfig config, string meridiem)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == ClockStyle.Analog)
        {
            return [];
        }

        var step = MainStep(config);
        var sizes = new List<double>(3) { step.FontSize };
        if (Meridiem(config, meridiem))
        {
            sizes.Add(FamilyType.Unit(step).FontSize);
        }

        if (Under(config))
        {
            sizes.Add(FamilyType.Under(step).FontSize);
        }

        return sizes;
    }

    /// <summary>[S-12833] The face's plan drawing its parts of at least <paramref name="floor"/> DIP: its width and its ink's height, no inset.</summary>
    public static FacePlan Plan(ClockConfig config, string meridiem, double floor)
    {
        ArgumentNullException.ThrowIfNull(config);
        var step = MainStep(config);
        var face = Face(config, meridiem, FamilyType.Unit(step).FontSize >= floor, FamilyType.Under(step).FontSize >= floor);
        return new FacePlan(face.Width, face.Ink);
    }

    /// <summary>The step of the face's main reading: the words' for the words, the figures' for the rest.</summary>
    private static TypeStep MainStep(ClockConfig config) => config.Style == ClockStyle.Word ? Words : Step;

    /// <summary>Whether the face draws a meridiem: a time face in a region that writes 12-hour time.</summary>
    private static bool Meridiem(ClockConfig config, string meridiem)
        => !string.IsNullOrEmpty(meridiem) && config.Style is not (ClockStyle.Analog or ClockStyle.Word);

    /// <summary>Whether the face has a line under: a world clock's city or the offset, never on the minimal clock ([S-6014]).</summary>
    private static bool Under(ClockConfig config)
        => config.Style != ClockStyle.Minimal && (config.IsWorldClock || config.OffsetMode != ClockOffsetMode.None);

    /// <summary>
    /// The face at its step with the parts asked for: its width, its height (its lines and the line under) and its ink
    /// (the height less the leading over its first ink and under its last: the figures' capitals and baseline, a flip
    /// card's whole box, the line under's baseline), no inset.
    /// </summary>
    private static (double Width, double Height, double Ink) Face(ClockConfig config, string meridiem, bool meridiemShown, bool underShown)
    {
        var step = MainStep(config);
        var withMeridiem = meridiemShown && Meridiem(config, meridiem);
        var unit = withMeridiem ? " " + meridiem : string.Empty;
        var under = underShown && Under(config);
        var underStep = FamilyType.Under(step);
        var underLine = under ? underStep.LineHeight : 0;
        var seconds = config.DrawsSeconds;

        // The leading over the first ink and under the last: a text line's, none over a flip card (its box is its ink).
        double Ink(double height, bool cards) => height - (cards ? 0 : LineInk.Of(step).Over) - (under ? LineInk.Of(underStep).Under : cards ? 0 : LineInk.Of(step).Under);

        switch (config.Style)
        {
            case ClockStyle.Stacked:
                var stacked = ((seconds ? 3 : 2) * step.LineHeight) + underLine;
                return (ReadoutRules.Width(step, new ReadoutText(Figures, unit, OneReadout: true)), stacked, Ink(stacked, cards: false));
            case ClockStyle.Flip:
                var flip = step.LineHeight + underLine;
                var beside = withMeridiem ? FamilySpace.S2 + (HeroFit.Ems(meridiem) * FamilyType.Unit(step).FontSize) : 0;
                return (FlipRules.RowWidth(step, seconds ? 3 : 2, Figures) + beside, flip, Ink(flip, cards: true));
            case ClockStyle.Analog:
                var dial = DialSide;
                var tall = dial + (under ? DialGap + FamilyType.Body.LineHeight : 0);
                return (dial, tall, tall);
            case ClockStyle.Word:
                var width = WordsWidth;
                var words = (WordClock.Lines(WordClock.Longest(Strings.Current), width * WordClock.WidthMargin, step) * step.LineHeight) + underLine;
                return (width, words, Ink(words, cards: false));
            default:
                var line = step.LineHeight + underLine;
                return (ReadoutRules.Width(step, new ReadoutText(seconds ? "88:88:88" : "88:88", unit, OneReadout: true)), line, Ink(line, cards: false));
        }
    }

    /// <summary>
    /// [S-6741] The one line under a clock's face: the city of a world clock and the offset, " · " between them,
    /// as the line's room (<paramref name="width"/>) holds them as they are drawn (<paramref name="drawn"/>: a line's width,
    /// measured by the view), else the city alone; the line is cut with an ellipsis past that. Null when there is nothing
    /// to say. The date is the Date widget's ([S-12805]).
    /// </summary>
    public static string? Under(double width, Func<string, double> drawn, string? label, string? offset)
    {
        string? Join(params string?[] parts)
        {
            var shown = parts.Where(p => !string.IsNullOrEmpty(p)).ToArray();
            return shown.Length == 0 ? null : string.Join(" · ", shown);
        }

        string?[] candidates =
        [
            Join(label, offset),
            Join(label) ?? Join(offset),
        ];

        foreach (var candidate in candidates)
        {
            if (candidate is not null && drawn(candidate) <= width)
            {
                return candidate;
            }
        }

        return candidates.LastOrDefault(c => c is not null);
    }
}
