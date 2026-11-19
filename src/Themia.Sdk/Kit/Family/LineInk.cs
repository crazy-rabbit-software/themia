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
