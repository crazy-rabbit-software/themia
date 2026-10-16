using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>[S-659] A clock's digit row as fitted: the time's step and the step of the meridiem (or seconds) beside it.</summary>
public readonly record struct ClockRow(TypeStep Time, TypeStep Tail);

/// <summary>
/// [S-659] The fill rule for a readout that is the whole tile (the clock's time, the temperature):
/// as large as the box holds it, the card's padding the only gap.
/// <para>
/// The ramp's estimate (<see cref="FluentType.Fit"/>, 0.58em a character) is right for the
/// digits and wrong for everything round them: a colon is less than half a digit, and the
/// meridiem was counted as three full-size characters although it is drawn at under half the
/// digits' size. A 12-hour clock was laid out for "12:34 PMx" at full size and drew "10:42" in
/// 61 % of its width. So a hero is measured in ems by what its glyphs are (<see cref="Ems"/>, the
/// advances of Segoe UI Variable, the widest weight and optical size), and once it is past the
/// ramp its figures are set solid (<see cref="Leading"/>): digits have no descenders, and the
/// ramp's 1.3 leading spent a third of a strip's height on air.
/// </para>
/// </summary>
public static class HeroFit
{
    /// <summary>The line height of figures past the ramp, as a multiple of their size: digits stand 0.72em tall.</summary>
    public const double Leading = 1.15;

    /// <summary>The meridiem's line against the time's size (the tail is fitted to it), as the clock always set it.</summary>
    public const double MeridiemScale = 0.45;

    /// <summary>Stacked digits' line height, as a multiple of their size: two lines of figures set solid.</summary>
    public const double StackedLeading = 1.05;

    /// <summary>A stacked clock's tail (seconds, meridiem) against the digits' size.</summary>
    public const double StackedTailScale = 0.4;

    /// <summary>
    /// How wide <paramref name="text"/> is, in ems of its own size: Segoe UI Variable's advances by
    /// glyph class, the widest over its weights and optical sizes, so a fit on them never clips.
    /// A digit is 0.58 (tabular, as every hero sets them), a colon or a stop 0.25, a capital 0.72
    /// ("M" and "W" 0.95), a small letter 0.56; a script the table does not know counts a full em
    /// a character, and a combining mark nothing.
    /// </summary>
    public static double Ems(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var ems = 0.0;

        foreach (var c in text)
        {
            ems += Advance(c);
        }

        return ems;
    }

    private static double Advance(char c)
    {
        if (c is >= '0' and <= '9')
        {
            return 0.58;
        }

        switch (c)
        {
            case ':' or '.' or ',' or ';' or '\'' or '!' or '|':
                return 0.25;
            case ' ' or ' ' or ' ' or ' ':
                return 0.28;
            case '-':
                return 0.41;
            case '−' or '+':
                return 0.7;
            case '°':
                return 0.39;
            case '%':
                return 0.85;
            case 'M' or 'W' or 'm' or 'w':
                return 0.95;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(c);

        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        if (c <= 'ɏ')
        {
            return category == UnicodeCategory.UppercaseLetter ? 0.72 : 0.56;
        }

        return 1.0;
    }

    /// <summary>
    /// The figures' step in a box: a ramp step while the box is small (under TitleLarge, with the
    /// ramp's own line heights), then the size at which <paramref name="ems"/> fills the width or a
    /// <see cref="Leading"/> line fills the height, whichever comes first. Continuous past the ramp,
    /// so the readout never stands in a band of nothing.
    /// </summary>
    public static TypeStep Figures(double width, double height, double ems)
    {
        if (!Usable(width) || !Usable(height) || !Usable(ems))
        {
            return FluentType.Subtitle;
        }

        var size = Math.Min(width / ems, height / Leading);

        if (size < FluentType.TitleLarge.FontSize)
        {
            return FluentType.FitWidth(width, height, ems, FluentType.Caption, FluentType.TitleLarge);
        }

        return new TypeStep(size, size * Leading, true);
    }

    /// <summary>The widest time a clock draws: "88:88", or "88:88:88" with the seconds.</summary>
    public static string WidestTime(bool seconds) => seconds ? "88:88:88" : "88:88";

    /// <summary>
    /// [S-659] The digital clock's row: the time and, beside it after <paramref name="gap"/>, the
    /// <paramref name="meridiem"/> (empty on a 24-hour clock) at the largest ramp step whose line
    /// is within <see cref="MeridiemScale"/> of the time's size. The meridiem's width depends on the
    /// time's size, so the time is fitted beside each step the meridiem could take, and the largest
    /// that draws its meridiem at that step or smaller is the row.
    /// </summary>
    public static ClockRow Row(double width, double height, bool seconds, string meridiem, double gap)
    {
        var ems = Ems(WidestTime(seconds));
        var full = Figures(width, height, ems);

        if (string.IsNullOrEmpty(meridiem) || !Usable(width) || !Usable(height))
        {
            return new ClockRow(full, Tail(width, full));
        }

        // Each step the meridiem may take bounds the time it can stand beside; the largest time
        // whose own meridiem is no wider than the bound it was fitted to wins.
        var tailEms = Ems(meridiem);
        var time = FluentType.Caption;

        foreach (var bound in FluentType.Ramp)
        {
            if (bound.FontSize > FluentType.Title.FontSize)
            {
                break;
            }

            var room = width - (tailEms * bound.FontSize) - gap;
            if (!Usable(room))
            {
                continue;
            }

            var candidate = Figures(room, height, ems);
            if (Tail(width, candidate).FontSize <= bound.FontSize && candidate.FontSize > time.FontSize)
            {
                time = candidate;
            }
        }

        return new ClockRow(time, Tail(width, time));
    }

    /// <summary>The meridiem's step beside a time: the largest ramp step, Caption to Title, whose line is within <see cref="MeridiemScale"/> of it.</summary>
    public static TypeStep Tail(double width, TypeStep time)
        => FluentType.Fit(width, time.FontSize * MeridiemScale, 2, FluentType.Caption, FluentType.Title);

    /// <summary>
    /// [S-659] [S-1483] The stacked clock's digits: the hours over the minutes, each line
    /// <see cref="StackedLeading"/> of the size, as wide as "88" and, after <paramref name="gap"/>,
    /// the <paramref name="tail"/> beside the minutes (the meridiem, or the seconds as "88"; empty
    /// for none) at <see cref="StackedTailScale"/> of the size. Never under Caption.
    /// </summary>
    public static TypeStep Stacked(double width, double height, string tail, double gap)
    {
        if (!Usable(width) || !Usable(height))
        {
            return new TypeStep(FluentType.Caption.FontSize, FluentType.Caption.FontSize * StackedLeading, true);
        }

        var tailEms = Ems(tail) * StackedTailScale;
        var room = width - (tailEms > 0 ? gap : 0);
        var size = Math.Min(room / (Ems("88") + tailEms), height / 2 / StackedLeading);
        size = Math.Max(FluentType.Caption.FontSize, size);
        return new TypeStep(size, size * StackedLeading, true);
    }

    /// <summary>
    /// Where a block-line-height line puts its baseline, as a share of the line from its top: the
    /// font's ascent over its ascent and descent (Segoe UI Variable: 1.079 over 1.330).
    /// </summary>
    private const double BaselineShare = 0.81;

    /// <summary>
    /// [S-659] How far above the bottom of its line a step's baseline stands. Two bottom-aligned
    /// lines of different heights (the time and the meridiem beside it) share a baseline when the
    /// shorter is lifted by the difference. Not half the leading: that is only right when both lines
    /// have the ramp's ratio, and set solid the meridiem sat a fifth of an em under the digits.
    /// </summary>
    public static double BaselineDrop(TypeStep step) => step.LineHeight * (1 - BaselineShare);

    private static bool Usable(double value) => double.IsFinite(value) && value > 0;
}
