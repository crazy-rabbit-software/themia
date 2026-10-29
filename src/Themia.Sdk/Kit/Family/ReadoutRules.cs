using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-6702] What a Readout is asked to hold, as its widest case: the value ("188.8"), the unit set
/// after it (" KB/s", "%", empty for none, a space included where one is drawn), whether a glyph
/// leads it, whether a line stands under it, and whether the widget is a one-readout widget (a
/// clock, a timer, a quick toggle, a power button), which alone may use the Hero steps.
/// </summary>
public readonly record struct ReadoutText(string Value, string Unit = "", bool Lead = false, bool LineUnder = false, bool OneReadout = false);

/// <summary>[S-6707] How a Readout with a delta folds: whether its unit is drawn, and whether the delta stands beside the value or on the line under it.</summary>
public readonly record struct ReadoutFold(bool Unit, bool DeltaBeside);

/// <summary>
/// [S-6702] The Readout's step: the largest step whose line (and the line under it) fits the box and
/// whose value, unit and lead fit its width, never below Big 28.
/// <para>
/// A data widget's number climbs 28 → 40 → 68 with the tile and stops there: its ceiling is 28 while
/// the area is compact both ways or regular one way only (XS, S), 40 from regular both ways or
/// expanded one way (M, Wide, Tall), 68 once it is expanded both ways (L, XL). Room left at L and XL
/// goes to more of the widget's own content, never a bigger number ([S-666], concise first). A
/// one-readout widget is its readout, so it climbs the Hero steps (96, 136, 192) as far as they fit,
/// on fixed steps, never filling the box.
/// </para>
/// <para>
/// Widths come from <see cref="HeroFit.Ems"/>, the advances of Segoe UI Variable by glyph class, so a
/// colon or a degree sign is not counted as a digit; the unit is measured at its own step.
/// </para>
/// </summary>
public static class ReadoutRules
{
    /// <summary>The Readout step for <paramref name="text"/> in a <paramref name="width"/> × <paramref name="height"/> box of a view given <paramref name="area"/>.</summary>
    public static TypeStep Step(WidgetArea area, double width, double height, ReadoutText text)
    {
        var floor = FamilyType.Big28;
        if (!Usable(width) || !Usable(height))
        {
            return floor;
        }

        var ceiling = text.OneReadout ? double.PositiveInfinity : Ceiling(area);
        var chosen = floor;

        IEnumerable<TypeStep> steps = text.OneReadout ? [.. FamilyType.Big, .. FamilyType.Hero] : FamilyType.Big;
        foreach (var step in steps)
        {
            if (FamilyType.BaseOf(step) > ceiling)
            {
                continue;
            }

            if (Width(step, text) <= width && Height(step, text) <= height)
            {
                chosen = step;
            }
        }

        return chosen;
    }

    /// <summary>
    /// [S-6702] The largest base step a data widget's number may take in <paramref name="area"/>: 28,
    /// 40 or 68 by the area's classes (their sum: 0 or 1 is 28, from 2 it is 40, expanded both ways 68).
    /// </summary>
    public static double Ceiling(WidgetArea area)
    {
        if (area.AtLeast(SizeClass.Expanded, SizeClass.Expanded))
        {
            return 68;
        }

        return (int)area.WidthClass + (int)area.HeightClass >= 2 ? 40 : 28;
    }

    /// <summary>How wide the readout is on <paramref name="step"/>: the lead glyph and its gap, the value, the unit at its step.</summary>
    public static double Width(TypeStep step, ReadoutText text)
    {
        var lead = text.Lead ? step.FontSize + FamilySpace.GlyphGap : 0;
        var unit = string.IsNullOrEmpty(text.Unit) ? 0 : HeroFit.Ems(text.Unit) * FamilyType.Unit(step).FontSize;
        return lead + (HeroFit.Ems(text.Value) * step.FontSize) + unit;
    }

    /// <summary>How tall the readout is on <paramref name="step"/>: its line, and the line under it when it has one.</summary>
    public static double Height(TypeStep step, ReadoutText text)
        => step.LineHeight + (text.LineUnder ? FamilyType.Under(step).LineHeight : 0);

    /// <summary>
    /// [S-6707] Where a Readout's delta (a price's or a rate's change, Body) and its unit stand on
    /// <paramref name="step"/> in <paramref name="width"/>: the value, the unit and the delta on one line
    /// where they fit; else the unit gives way and the delta stays beside the value; else the delta takes
    /// the line under the value (the unit back where the value and it fit). The delta is measured on its
    /// widest case, so the fold does not change as the reading moves.
    /// </summary>
    public static ReadoutFold Fold(TypeStep step, ReadoutText text, string delta, double width)
    {
        var deltaWidth = DeltaWidth(delta);
        var withUnit = Width(step, text);
        var bare = Width(step, text with { Unit = string.Empty });
        var hasUnit = !string.IsNullOrEmpty(text.Unit);

        if (withUnit + deltaWidth <= width)
        {
            return new ReadoutFold(Unit: hasUnit, DeltaBeside: true);
        }

        if (hasUnit && bare + deltaWidth <= width)
        {
            return new ReadoutFold(Unit: false, DeltaBeside: true);
        }

        return new ReadoutFold(Unit: hasUnit && withUnit <= width, DeltaBeside: false);
    }

    /// <summary>A delta's width beside the value: the en space before it (half an em) and its text, Body.</summary>
    public static double DeltaWidth(string? delta)
        => string.IsNullOrEmpty(delta) ? 0 : (0.5 + HeroFit.Ems(delta)) * FamilyType.Body.FontSize;

    /// <summary>
    /// [S-3860] A formatted reading as the Readout draws it: the figure, and the unit after it with the
    /// space that stood between them ("62%" is "62" and "%"; "2.4 MB/s" is "2.4" and " MB/s"). A text
    /// that does not start with a figure ("--", Turkish "%42", a direction mark) or that holds a second
    /// figure ("1 234 ms", "3/4") is all value and no unit, so nothing is reordered or lost.
    /// </summary>
    public static ReadoutText Split(string? text)
    {
        if (string.IsNullOrEmpty(text) || !char.IsDigit(text[0]))
        {
            return new ReadoutText(text ?? string.Empty);
        }

        var end = 0;

        // The figure: digits of any script and the separators a number is written with.
        while (end < text.Length && (char.IsDigit(text[end]) || IsSeparator(text[end])))
        {
            end++;
        }

        var unitAt = end;
        while (unitAt < text.Length && IsSpace(text[unitAt]))
        {
            unitAt++;
        }

        var unit = text[unitAt..];
        if (unit.Length == 0 || unit.Any(char.IsDigit) || IsSeparator(text[end - 1]))
        {
            return new ReadoutText(text);
        }

        return new ReadoutText(text[..end], text[end..]);
    }

    private static bool IsSeparator(char c) => c is '.' or ',' or '٫' or '٬';

    private static bool IsSpace(char c) => c is ' ' or ' ' or ' ' or ' ';

    private static bool Usable(double value) => double.IsFinite(value) && value > 0;
}
