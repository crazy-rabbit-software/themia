using Themia.Core.Widgets;
using Themia.Sdk;

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

/// <summary>[S-6736] What a Tile says by its colours: the state of what it switches or runs.</summary>
public enum TileState
{
    /// <summary>Off, or a button at rest: the item layer's tile, the text primary.</summary>
    Off,

    /// <summary>On: the whole tile (or the round look's circle) in the accent.</summary>
    On,

    /// <summary>Asked, not answered yet: every part secondary, nothing filled.</summary>
    Unknown,

    /// <summary>Missing on this PC ([S-1940]): every part disabled.</summary>
    Unavailable,

    /// <summary>What the next click does is grave, or the last one failed (a power button armed or failed): the glyph, the text and the ring critical.</summary>
    Critical,
}

/// <summary>
/// [S-6715] The roles a Tile's parts are drawn in. <see cref="Fill"/> and <see cref="Circle"/> are the
/// accent or null; null is the item layer's own fill (a row's for the tile, a tile's for the round
/// look's circle, [S-6513]), so a tile that is not on reads as every other item on the card.
/// </summary>
public readonly record struct TileRoles(ColourRole? Fill, ColourRole? Circle, ColourRole Glyph, ColourRole Label, ColourRole State);

/// <summary>[S-6736] Which of the switch look's parts its row shows beside the switch.</summary>
public readonly record struct SwitchParts(bool Glyph, bool Lines);

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

    /// <summary>[S-6736] A ringed glyph's share of its ring at most: the glyph stands clear of the ring's stroke.</summary>
    public const double RingShare = 0.75;

    /// <summary>[S-6736] The switch look's switch: Windows' toggle switch's track, 40 × 20.</summary>
    public const double SwitchWidth = 40;

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
    public static double RoundGlyph(double circle) => Largest(circle / 2);

    /// <summary>
    /// [S-6736] A ringed tile's glyph (a power button's, whose arm ring is drawn round it): the largest
    /// of the family's glyph sizes at most <see cref="RingShare"/> of the ring, 16 at least, so arming
    /// draws the ring and moves nothing.
    /// </summary>
    public static double RingGlyph(double ring) => Largest(ring * RingShare);

    /// <summary>
    /// [S-6736] The switch look's row in <paramref name="width"/>: [glyph 16] 8 [name over state] 12
    /// [switch]. The name and state need <see cref="StatusRules.MinSentence"/> (the room a sentence is
    /// worth); the glyph gives way first, then the lines, so a narrow tile keeps the glyph and the switch.
    /// </summary>
    public static SwitchParts Switch(double width)
    {
        var room = (double.IsFinite(width) ? width : 0) - SwitchWidth - FamilySpace.ColumnGap;
        if (room - FamilyShape.Icon - FamilySpace.GlyphGap >= StatusRules.MinSentence)
        {
            return new SwitchParts(Glyph: true, Lines: true);
        }

        return room >= StatusRules.MinSentence ? new SwitchParts(Glyph: false, Lines: true) : new SwitchParts(Glyph: true, Lines: false);
    }

    /// <summary>[S-6715] [S-6736] The roles of a tile of <paramref name="look"/> in <paramref name="state"/>.</summary>
    public static TileRoles Roles(TileLook look, TileState state) => (look, state) switch
    {
        (_, TileState.Unavailable) => new TileRoles(null, null, ColourRole.TextDisabled, ColourRole.TextDisabled, ColourRole.TextDisabled),
        (_, TileState.Unknown) => new TileRoles(null, null, ColourRole.TextSecondary, ColourRole.TextSecondary, ColourRole.TextSecondary),
        (_, TileState.Critical) => new TileRoles(null, null, ColourRole.Critical, ColourRole.Critical, ColourRole.Critical),
        (TileLook.Tile, TileState.On) => new TileRoles(ColourRole.Accent, null, ColourRole.TextOnAccent, ColourRole.TextOnAccent, ColourRole.TextOnAccentSecondary),
        (TileLook.Round, TileState.On) => new TileRoles(null, ColourRole.Accent, ColourRole.TextOnAccent, ColourRole.TextPrimary, ColourRole.TextSecondary),
        _ => new TileRoles(null, null, ColourRole.TextPrimary, ColourRole.TextPrimary, ColourRole.TextSecondary),
    };

    // ---- the sized tile ([S-11307]) ---------------------------------------------------------

    /// <summary>[S-11307] The glyph's step at each size: 28, 40, 68 (a round tile's circle, a ringed tile's ring).</summary>
    public static TypeStep GlyphAt(ReadoutSize size) => ReadoutSizes.Normalise(size) switch
    {
        ReadoutSize.Small => FamilyType.Big28,
        ReadoutSize.Large => FamilyType.Hero68,
        _ => FamilyType.Big40,
    };

    /// <summary>[S-11307] A Tile or Round tile's square at each size: 100, 120, 160 (the glyph, its label and state lines, the inset all round, and room for the name).</summary>
    public static double SideAt(ReadoutSize size) => ReadoutSizes.Normalise(size) switch
    {
        ReadoutSize.Small => 100,
        ReadoutSize.Large => 160,
        _ => 120,
    };

    /// <summary>
    /// [S-11307] The Switch look's row at each size: 200, 240, 280 wide (at the least its glyph, a
    /// sentence's room for its name and state, and the switch, <see cref="Switch"/>), 60 tall.
    /// </summary>
    public static double SwitchWidthAt(ReadoutSize size) => ReadoutSizes.Normalise(size) switch
    {
        ReadoutSize.Small => 200,
        ReadoutSize.Large => 280,
        _ => 240,
    };

    /// <summary>[S-11307] The Switch look's height: the name over the state line in the inset.</summary>
    public const double SwitchHeight = (2 * FamilySpace.Inset) + FamilyType.BodyLine + FamilyType.CaptionLine;

    /// <summary>
    /// [S-11307] A quick toggle's or a power button's card: fixed both ways by its look and Size, the Tile
    /// and Round looks a square, the Switch look a row; nothing in it changes with the size but its scale.
    /// </summary>
    public static WidgetSizing Sizing(TileLook look, ReadoutSize size)
        => WidgetSizing.Fixed(look == TileLook.Switch
            ? new SizeDip(SwitchWidthAt(size), SwitchHeight)
            : new SizeDip(SideAt(size), SideAt(size)));

    /// <summary>The largest of the family's glyph sizes at most <paramref name="room"/>, 16 at least.</summary>
    private static double Largest(double room)
    {
        var best = FamilyShape.Icon;
        foreach (var size in FamilyShape.Icons.Concat(FamilyType.Big.Concat(FamilyType.Hero).Select(s => s.FontSize)))
        {
            if (size <= room && size > best)
            {
                best = size;
            }
        }

        return best;
    }
}
