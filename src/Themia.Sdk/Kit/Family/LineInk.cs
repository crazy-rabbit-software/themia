using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-12807] The empty room a part's box holds over its first ink (<see cref="Over"/>) and under its last
/// (<see cref="Under"/>): a text line's leading over its capitals and under its baseline, a glyph's room
/// in the line it is centred on. A list or a readout stands that ink, not the box, on the insets.
/// </summary>
public readonly record struct LineInk(double Over, double Under)
{
    /// <summary>No room: the box's edge is its ink (a chart, a bar, a filled tile).</summary>
    public static LineInk None => default;

    /// <summary>
    /// The family's face (Segoe UI Variable): its capitals' height and where a line of the family's
    /// (<c>LineStackingStrategy.BlockLineHeight</c>) puts its baseline, both fractions (of the font size and
    /// of the line). Measured on the renders: a Body line (14/20) inks its capitals from 6 under its top
    /// and stands its baseline 4 over its bottom.
    /// </summary>
    public const double FaceCap = 0.7;

    public const double FaceBaseline = 0.8;

    /// <summary>
    /// [S-12813] Where a dash ("--", a reading's placeholder) inks its top over the baseline, a fraction of the font
    /// size: measured on the renders, the dash stands at about a third of the capitals' height.
    /// </summary>
    public const double FaceDash = 0.31;

    /// <summary>
    /// [S-12813] A line on <paramref name="step"/> that draws a placeholder of dashes and, beside it, a unit of
    /// <paramref name="unitSize"/> (0: none): its ink starts at the dash's top or the unit's capitals, whichever
    /// stands higher, not at the capitals the line's step would draw.
    /// </summary>
    public static LineInk Placeholder(TypeStep step, double unitSize)
    {
        var line = Of(step);
        if (line == None)
        {
            return None;
        }

        var baseline = step.LineHeight * FaceBaseline;
        var reach = Math.Max(step.FontSize * FaceDash, unitSize > 0 ? unitSize * FaceCap : 0);
        return new LineInk(Math.Max(line.Over, baseline - reach), line.Under);
    }

    /// <summary>Whether <paramref name="text"/> draws no capital or figure: dashes, dots and spaces (a placeholder).</summary>
    public static bool Capless(string? text)
        => !string.IsNullOrEmpty(text) && text.All(c => c is '-' or '‒' or '–' or '—' or '−' or '.' or '·' or ' ');

    /// <summary>A text line on <paramref name="step"/>: its leading over its capitals and under its baseline.</summary>
    public static LineInk Of(TypeStep step)
    {
        if (!(step.LineHeight > 0) || !(step.FontSize > 0))
        {
            return None;
        }

        var baseline = step.LineHeight * FaceBaseline;
        return new LineInk(Math.Max(0, baseline - (step.FontSize * FaceCap)), step.LineHeight - baseline);
    }

    /// <summary>
    /// A part whose ink is this text's or a glyph's of <paramref name="glyph"/> centred on the text's
    /// <paramref name="height"/>, whichever reaches further (a row's lead beside its lines).
    /// </summary>
    public LineInk With(double glyph, double height)
    {
        if (!(glyph > 0) || !double.IsFinite(height))
        {
            return this;
        }

        var room = (height - glyph) / 2;
        return new LineInk(Math.Min(Over, room), Math.Min(Under, room));
    }

    /// <summary>The ink of a family row of <paramref name="lines"/> lines (a Body name, Captions under it) led by a <paramref name="lead"/> (0: none).</summary>
    public static LineInk Row(int lines, double lead = 0)
    {
        var clamped = Math.Clamp(lines, 1, 3);
        var text = new LineInk(Of(FamilyType.Body).Over, Of(clamped == 1 ? FamilyType.Body : FamilyType.Caption).Under);
        return text.With(lead, FamilyType.BodyLine + ((clamped - 1) * FamilyType.CaptionLine));
    }
}
