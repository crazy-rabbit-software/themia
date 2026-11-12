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

/// <summary>[S-6744] The Date style as planned: the day's step, which words show and whether in their short names.</summary>
public readonly record struct ClockDatePlan(TypeStep Step, bool Weekday, bool ShortWeekday, bool Month, bool ShortMonth);

/// <summary>
/// The clock's faces on the family's steps (one-readout, [S-6737]): the step its Size names, which its
/// sizing made the card for ([S-2449]), measured in the content box (the card less its inset of 12, the
/// clock has no header). The box never changes what the face holds ([S-11800]).
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

    /// <summary>[S-6744] The longest wide weekday name ("segunda-feira") and month ("październik"), and their short forms, in characters.</summary>
    public const int WeekdayCharacters = 13, MonthCharacters = 11, ShortCharacters = 5;

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

    /// <summary>
    /// [S-6744] The Date style: the day of the month a Readout ("88") on the largest step that fits, the weekday
    /// over it and the month under it, both on the step's line under, secondary. A word too wide for its line in
    /// its wide name takes the short one; where the lines do not fit even the floor step, the month gives way,
    /// then the weekday.
    /// </summary>
    public static ClockDatePlan Date(double width, double height)
    {
        var number = new ReadoutText(Figures, OneReadout: true);

        var chosen = FamilyType.Big28;
        foreach (var step in ReadoutRules.Steps(oneReadout: true))
        {
            var under = FamilyType.Under(step);
            if (ReadoutRules.Width(step, number) <= width && step.LineHeight + (2 * under.LineHeight) <= height)
            {
                chosen = step;
            }
        }

        var line = FamilyType.Under(chosen);
        var weekday = chosen.LineHeight + line.LineHeight <= height;
        var month = chosen.LineHeight + (2 * line.LineHeight) <= height;
        return new ClockDatePlan(
            chosen,
            weekday,
            Words(line, WeekdayCharacters) > width,
            month && weekday,
            Words(line, MonthCharacters) > width);
    }

    private static double Words(TypeStep step, int characters) => FamilyType.EstimateWidth(step, characters);

    /// <summary>
    /// The room a face is given past its own size, so a box rounded to the device's pixels still holds
    /// the chosen step; far under the next step's growth, so the planners never pick a larger one.
    /// </summary>
    public const double Slack = 2;

    /// <summary>[S-2449] The analog dial at each size: three times the readout's 32, 48 and 64 DIP grid.</summary>
    public static double DialAt(ReadoutSize size) => size switch
    {
        ReadoutSize.Small => 96,
        ReadoutSize.Large => 192,
        _ => 144,
    };

    /// <summary>[S-2449] [S-6024] The words' step at each size: a sentence stops at 68, so one step below the readout's.</summary>
    public static TypeStep WordsAt(ReadoutSize size) => size switch
    {
        ReadoutSize.Small => FamilyType.Big28,
        ReadoutSize.Large => FamilyType.Hero68,
        _ => FamilyType.Big40,
    };

    /// <summary>[S-2449] The words' column at each size: about a dozen characters of its step to a line.</summary>
    public static double WordsWidth(ReadoutSize size) => FamilyType.EstimateWidth(WordsAt(size), 12);

    /// <summary>
    /// [S-2449] The clock's sizing ([S-2438]): its face at the step its Size names (40, 68, 96; the words
    /// 28, 40, 68; the dial 96, 144, 192), with the line under it where the face has one, in the card's
    /// inset of 12; never larger. The time faces (Digital, Minimal, Stacked, Flip) may be widened to twice
    /// their face, the line under cut to what fits, at the one height; the dial, the words and the date
    /// are one size. <paramref name="meridiem"/> is the widest AM/PM of the language when the region writes
    /// 12-hour time, else empty: the face is as wide as its widest time.
    /// </summary>
    public static WidgetSizing Sizing(ClockConfig config, string meridiem)
    {
        ArgumentNullException.ThrowIfNull(config);

        var size = ReadoutSizes.Normalise(config.Size);
        var step = ReadoutSizes.Step(size);
        var unit = string.IsNullOrEmpty(meridiem) ? string.Empty : " " + meridiem;
        var under = config.Style != ClockStyle.Minimal && (config.IsWorldClock || config.ShowDate || config.OffsetMode != ClockOffsetMode.None);
        var underLine = under ? FamilyType.Under(step).LineHeight : 0;
        var seconds = config.DrawsSeconds;

        (double W, double H, bool Widens) face = config.Style switch
        {
            ClockStyle.Stacked => (
                ReadoutRules.Width(step, new ReadoutText(Figures, unit, OneReadout: true)),
                ((seconds ? 3 : 2) * step.LineHeight) + underLine,
                true),
            ClockStyle.Flip => (
                FlipRules.RowWidth(step, seconds ? 3 : 2, Figures) + (unit.Length == 0 ? 0 : FamilySpace.S2 + (HeroFit.Ems(meridiem) * FamilyType.Unit(step).FontSize)),
                step.LineHeight + underLine,
                true),
            ClockStyle.Analog => (DialAt(size), DialAt(size) + (under ? DialGap + FamilyType.Body.LineHeight : 0), false),
            ClockStyle.Word => Sentence(size, under),
            ClockStyle.Date => (
                Math.Max(ReadoutRules.Width(step, new ReadoutText(Figures, OneReadout: true)), Words(FamilyType.Under(step), WeekdayCharacters)),
                step.LineHeight + (2 * FamilyType.Under(step).LineHeight),
                false),
            _ => (
                ReadoutRules.Width(step, new ReadoutText(seconds ? "88:88:88" : "88:88", unit, OneReadout: true)),
                step.LineHeight + underLine,
                true),
        };

        var box = new SizeDip(
            Math.Ceiling(face.W) + (2 * FamilySpace.Inset) + Slack,
            Math.Ceiling(face.H) + (2 * FamilySpace.Inset) + Slack);
        return face.Widens
            ? WidgetSizing.Range(box, box, box with { Width = 2 * box.Width })
            : WidgetSizing.Fixed(box);
    }

    /// <summary>[S-6024] The words' face: its column, as many of its lines as the language's longest sentence wraps to, the line under.</summary>
    private static (double W, double H, bool Widens) Sentence(ReadoutSize size, bool under)
    {
        var step = WordsAt(size);
        var width = WordsWidth(size);
        var longest = WordClock.Longest(Strings.Current);
        var height = (WordClock.Lines(longest, width * WordClock.WidthMargin, step) * step.LineHeight) + (under ? FamilyType.Under(step).LineHeight : 0);
        return (width, height, false);
    }

    /// <summary>
    /// [S-6741] The one line under a clock's face: the city of a world clock, the date and the offset, " · "
    /// between them, as the line's room (<paramref name="width"/> on <paramref name="step"/>) holds them: the long
    /// date, else the short one, else without the offset, else the city alone (or the short date); the line
    /// is cut with an ellipsis past that. Null when there is nothing to say.
    /// </summary>
    public static string? Under(double width, TypeStep step, string? label, string? longDate, string? shortDate, string? offset)
    {
        string? Join(params string?[] parts)
        {
            var shown = parts.Where(p => !string.IsNullOrEmpty(p)).ToArray();
            return shown.Length == 0 ? null : string.Join(" · ", shown);
        }

        string?[] candidates =
        [
            Join(label, longDate, offset),
            Join(label, shortDate, offset),
            Join(label, shortDate),
            Join(label) ?? Join(shortDate),
        ];

        foreach (var candidate in candidates)
        {
            if (candidate is not null && HeroFit.Ems(candidate) * step.FontSize <= width)
            {
                return candidate;
            }
        }

        return candidates.LastOrDefault(c => c is not null);
    }
}
