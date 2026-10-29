using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>[S-6715] The three looks of a Tile.</summary>
public enum TileLook
{
    /// <summary>The glyph centred with its label and state line under it; on, the whole tile is the accent.</summary>
    Tile,

    /// <summary>The glyph in a circle (the accent when on) with its label and state line under it.</summary>
    Round,

    /// <summary>A Row whose value is a switch: the label, the state under it, the switch at the end.</summary>
    Switch,
}

/// <summary>[S-6715] The roles a Tile's parts are drawn in; <see cref="Fill"/> null for none (the card's own).</summary>
public readonly record struct TileRoles(ColourRole? Fill, ColourRole? Circle, ColourRole Glyph, ColourRole Label, ColourRole State);

/// <summary>
/// [S-6715] The Tile block, for the one-readout widgets (the thirteen quick toggles, the seven power
/// buttons): the glyph centred on the Readout and Hero steps, one Body label under it 8 below, one
/// Caption state line. On is the whole tile in the accent (text and glyph on it), never a tinted
/// glyph; Round puts the glyph in a circle instead; Switch is a Row with a switch as its value. A
/// power button's arm ring is the Tile's ring, the Chart's stroke. A part missing on this PC is drawn
/// in the disabled role, never the accent ([S-1940]).
/// </summary>
public static class TileRules
{
    /// <summary>The glyph's share of the box's width at most: a glyph wider than this crowds its label.</summary>
    public const double WidthShare = 0.6;

    /// <summary>The label and state lines under a Tile's glyph, with the gap above them.</summary>
    public static double Below(bool label, bool state)
        => (label || state ? FamilySpace.S2 : 0) + (label ? FamilyType.Body.LineHeight : 0) + (state ? FamilyType.Caption.LineHeight : 0);

    /// <summary>
    /// [S-6715] The glyph's step in a <paramref name="width"/> × <paramref name="height"/> box (the tile
    /// less its inset): the largest Readout or Hero step that fits above the label and state lines and
    /// takes at most <see cref="WidthShare"/> of the width, never below Big 28.
    /// </summary>
    public static TypeStep GlyphStep(double width, double height, bool label = true, bool state = true)
    {
        var chosen = FamilyType.Big28;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return chosen;
        }

        foreach (var step in FamilyType.Big.Concat(FamilyType.Hero))
        {
            if (step.FontSize + Below(label, state) <= height && step.FontSize <= width * WidthShare)
            {
                chosen = step;
            }
        }

        return chosen;
    }

    /// <summary>
    /// [S-6715] A Round tile's glyph inside its circle of <paramref name="circle"/>: the largest of the
    /// family's glyph sizes (16, 32, a Readout or Hero step) at most half the circle, 16 at least.
    /// </summary>
    public static double RoundGlyph(double circle)
    {
        var best = FamilyShape.Icon;
        foreach (var size in FamilyShape.Icons.Concat(FamilyType.Big.Concat(FamilyType.Hero).Select(s => s.FontSize)))
        {
            if (size <= circle / 2 && size > best)
            {
                best = size;
            }
        }

        return best;
    }

    /// <summary>[S-6715] The roles of a tile of <paramref name="look"/>, on or off, available or not.</summary>
    public static TileRoles Roles(TileLook look, bool on, bool available = true)
    {
        if (!available)
        {
            return new TileRoles(null, look == TileLook.Round ? ColourRole.Subtle : null, ColourRole.TextDisabled, ColourRole.TextDisabled, ColourRole.TextDisabled);
        }

        return (look, on) switch
        {
            (TileLook.Tile, true) => new TileRoles(ColourRole.Accent, null, ColourRole.TextOnAccent, ColourRole.TextOnAccent, ColourRole.TextOnAccentSecondary),
            (TileLook.Round, true) => new TileRoles(null, ColourRole.Accent, ColourRole.TextOnAccent, ColourRole.TextPrimary, ColourRole.TextSecondary),
            (TileLook.Round, false) => new TileRoles(null, ColourRole.Subtle, ColourRole.TextPrimary, ColourRole.TextPrimary, ColourRole.TextSecondary),
            _ => new TileRoles(null, null, ColourRole.TextPrimary, ColourRole.TextPrimary, ColourRole.TextSecondary),
        };
    }
}
