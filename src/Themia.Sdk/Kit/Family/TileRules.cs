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
/// [S-11882] What a Tile or Round tile draws in its box: its glyph's step, whether its name (and a
/// toggle's state line) stand under it, and their steps. [S-12806] Under the inset's room for the 28 glyph the
/// glyph's box is <see cref="Mark"/> (28 or 16, the family's smallest glyph) in an inset of <see cref="Inset"/>
/// (down to 4): 0 for <see cref="Mark"/> is the step's own size.
/// </summary>
public readonly record struct TileFace(TypeStep Glyph, bool Texted, TypeStep Name, TypeStep State, double Mark = 0, double Inset = FamilySpace.Inset)
{
    /// <summary>The glyph's box: <see cref="Mark"/>, or the step's size.</summary>
    public double MarkSize => Mark > 0 ? Mark : Glyph.FontSize;
}

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
    /// <summary>[S-6736] A ringed glyph's share of its ring at most: the glyph stands clear of the ring's stroke.</summary>
    public const double RingShare = 0.75;

    /// <summary>[S-6736] The switch look's switch: Windows' toggle switch's track, 40 × 20.</summary>
    public const double SwitchWidth = 40;

    /// <summary>The label and state lines under a Tile's glyph, with the gap above them.</summary>
    public static double Below(bool label, bool state)
        => (label || state ? FamilySpace.S2 : 0) + (label ? FamilyType.Body.LineHeight : 0) + (state ? FamilyType.Caption.LineHeight : 0);

    /// <summary>
    /// [S-6715] A Round tile's glyph inside its circle of <paramref name="circle"/>: the largest of the
    /// family's glyph sizes (16, 32, a Readout or Hero step) at most half the circle, 16 at least.
    /// </summary>
    public static double RoundGlyph(double circle) => Largest(circle / 2);

    /// <summary>
    /// [S-15740] A ringed tile's arm ring (a power button's) round its glyph of <paramref name="glyph"/>: the glyph is
    /// <see cref="RingShare"/> of the ring, so the ring stands clear of it. The ring is drawn over the glyph's box, past
    /// it on every side, never laid out: the glyph is the face's mark at its full step as every tile's, its name 8 under it
    /// (WR-7: "Keep them together"; the ring's room had stood as a band between them, a 16 glyph in a 28 mark, the name 34
    /// under a 27 glyph at 300 × 100); armed, the ring is the face's ink and the face is centred on it with its sentence.
    /// </summary>
    public static double RingAround(double glyph) => glyph > 0 && double.IsFinite(glyph) ? glyph / RingShare : 0;

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

    // ---- the free tile ([S-11882]) -----------------------------------------------------------

    /// <summary>[S-11804] The lines a tile's name may take before it is cut with its ellipsis.</summary>
    public const int NameLines = 2;

    /// <summary>
    /// [S-11882] The glyph's steps a Tile or Round tile climbs as its box grows (a round tile's circle, a
    /// ringed tile's ring): the Readout steps, then the Hero steps up to 136 ([S-6737]: a one-readout widget).
    /// </summary>
    public static IReadOnlyList<TypeStep> GlyphSteps => [FamilyType.Big28, FamilyType.Big40, FamilyType.Hero68, FamilyType.Hero96, FamilyType.Hero136];

    /// <summary>
    /// [S-11882] The room a name needs beside nothing, on <paramref name="name"/>'s step: a short name
    /// ("Wi-Fi", "Lock") on one line, as the 100-wide tile always held at Body (76 in the inset), grown
    /// with the name's step.
    /// </summary>
    public static double NameRoom(TypeStep name) => 76 * name.FontSize / FamilyType.BodySize;

    /// <summary>
    /// [S-11882] The name's and the state line's steps under a glyph of <paramref name="glyph"/>: Body
    /// and Caption under the Readout steps and 68, Title (regular) and Body under the larger Hero steps,
    /// so the words grow with the glyph.
    /// </summary>
    public static (TypeStep Name, TypeStep State) TextAt(TypeStep glyph)
        => FamilyType.BaseOf(glyph) > 68 ? (FamilyType.Regular(FamilyType.Title), FamilyType.Body) : (FamilyType.Body, FamilyType.Caption);

    /// <summary>
    /// [S-11882] What a Tile or Round tile draws in a box of <paramref name="width"/> × <paramref name="height"/>
    /// DIPs (its card, the inset all round): the largest glyph step whose glyph, its name on
    /// <see cref="NameLines"/> lines and (<paramref name="stateLine"/>: a toggle's) its state line fit, the
    /// words on <see cref="TextAt"/>'s steps; where no step fits with its words, the glyph alone (the name
    /// is the tooltip, the state the fill, as Windows' own quick-settings buttons) on the largest step at
    /// most a third of the inset box's short side, never under 28, so a box that grows into its words
    /// never sees its glyph shrink. Centred both ways in the box by the block.
    /// </summary>
    public static TileFace Face(double width, double height, bool stateLine = true, int nameLines = NameLines, double nameWidth = 0, int stateLines = 1, bool ringed = false)
    {
        // [S-15755] The name's lines as it stands at the inset's width (1 or 2): a short name in a wide card takes one.
        nameLines = Math.Clamp(nameLines, 1, NameLines);
        // [S-12806] A box too small for the 28 glyph in the inset: the glyph alone, as large as it can be (28, else the
        // family's smallest, 16) with the inset given up down to 4 round it; under the 16's room (24) the glyph is two
        // thirds of the box, the inset the rest, down to the floor's box.
        var side = Math.Min(double.IsFinite(width) ? width : 0, double.IsFinite(height) ? height : 0);
        if (side < GlyphAloneSide)
        {
            var mark = side - (2 * TightInset) >= FamilyType.Big28.FontSize ? FamilyType.Big28.FontSize
                : side - (2 * TightInset) >= FamilyShape.Icon ? FamilyShape.Icon
                : Math.Max(0, Math.Floor(side * 2 / 3));
            var inset = Math.Clamp(Math.Floor((side - mark) / 2), 0, FamilySpace.Inset);
            var (name, state) = TextAt(FamilyType.Big28);
            return new TileFace(FamilyType.Big28, false, name, state, mark, inset);
        }

        // [S-15763] One plan at every size: the 28 glyph, its name on Body and its state line on Caption, one block the tile's
        // ReadingFit scales as one (WR-7: "A reading and its caption stay together and scale up"). The glyph had climbed the
        // Hero steps faster than its words (a caption a fifth of its glyph at 520 x 600, a third at 350 x 70). The words stay
        // while they are read at 9 (WR-14); under that the glyph stands alone.
        var w = (double.IsFinite(width) ? width : 0) - (2 * FamilySpace.Inset);
        var h = (double.IsFinite(height) ? height : 0) - (2 * FamilySpace.Inset);
        return new TileFace(FamilyType.Big28, WordsKept(w, h, stateLine, nameLines, nameWidth, stateLines, ringed), FamilyType.Body, FamilyType.Caption);
    }

    /// <summary>
    /// [S-15741] Whether the 28 glyph, its name on <see cref="NameLines"/> lines and (<paramref name="stateLine"/>) its state
    /// line, scaled down as one block into an inset box <paramref name="w"/> × <paramref name="h"/>, draw their smallest
    /// text at <see cref="ScaleFloor.Text"/> or more.
    /// </summary>
    public static bool WordsKept(double w, double h, bool stateLine, int nameLines = NameLines, double nameWidth = 0, int stateLines = 1, bool ringed = false)
    {
        stateLines = Math.Clamp(stateLines, 1, NameLines);
        nameLines = Math.Clamp(nameLines, 1, NameLines);
        var (name, state) = TextAt(FamilyType.Big28);

        // [S-15763] The words' own width where the view measured it (a short "Restart" in a 70 wide card), else a short name's.
        // [S-15764] A ringed glyph's mark is its ring, always laid out; the state line planned on the lines its longest
        // sentence takes.
        var mark = ringed ? RingAround(FamilyType.Big28.FontSize) : FamilyType.Big28.FontSize;
        var blockWidth = Math.Max(mark, nameWidth > 0 && double.IsFinite(nameWidth) ? nameWidth : NameRoom(name));
        var blockHeight = mark + FamilySpace.S2 + (nameLines * name.LineHeight) + (stateLine ? stateLines * state.LineHeight : 0);
        var smallest = stateLine ? state.FontSize : name.FontSize;
        var scale = Math.Min(1, Math.Min(w / blockWidth, h / blockHeight));
        return w > 0 && h > 0 && scale * smallest >= ScaleFloor.Text - 1e-9;
    }

    /// <summary>[S-11882] The box that holds the 28 glyph alone in the family's inset, 52: under it the inset gives way ([S-12806]).</summary>
    public static double GlyphAloneSide => (2 * FamilySpace.Inset) + FamilyType.Big28.FontSize;

    /// <summary>[S-12806] The least inset round a glyph in a box too small for the family's: 4.</summary>
    public const double TightInset = FamilySpace.S1;

    /// <summary>
    /// [S-12806] The smallest a Tile or Round tile may be (owner, 2026-11-17: "the buttons like the night mode toggle
    /// should be able to be smaller than they are now, they should not really have a smallest size at all"; and WR-14,
    /// 2026-11-19: "for the widgets that can scale there should basically be no lower limit except for one that makes
    /// sense so you can still see the widget"): the grid's least widget, 32 (<see cref="WidgetSizing.LeastScaled"/>), as
    /// every face that scales. <see cref="Face"/> draws the glyph in whatever box that leaves, the inset and then the glyph
    /// giving way.
    /// </summary>
    public const double MinSide = WidgetSizing.LeastScaled;

    /// <summary>
    /// [S-11882] [S-11804] Where a new Tile or Round tile lands: the 28 glyph, its name on two lines and (a
    /// toggle's) the state line in the inset, on the 4 grid, at least 100: a quick toggle 116, a power
    /// button 100.
    /// </summary>
    public static double NaturalSide(bool stateLine = true)
    {
        var (name, state) = TextAt(FamilyType.Big28);
        var needed = (2 * FamilySpace.Inset) + FamilyType.Big28.FontSize + FamilySpace.S2 + (NameLines * name.LineHeight) + (stateLine ? state.LineHeight : 0);
        return Math.Max(100, Math.Ceiling(needed / FamilySpace.S1) * FamilySpace.S1);
    }

    /// <summary>[S-11882] The largest a Tile or Round tile grows to on either axis.</summary>
    public const double MaxSide = 480;

    /// <summary>[S-11307] The Switch look's height: the name over the state line in the inset.</summary>
    public const double SwitchHeight = (2 * FamilySpace.Inset) + FamilyType.BodyLine + FamilyType.CaptionLine;

    /// <summary>[S-11882] The Switch look's narrowest row: its glyph and the switch alone in the inset (<see cref="Switch"/>).</summary>
    public const double SwitchMinWidth = (2 * FamilySpace.Inset) + FamilyShape.Icon + FamilySpace.GlyphGap + SwitchWidth;

    /// <summary>[S-11882] The Switch look's new row: its glyph, a sentence's room for its name and state, and the switch.</summary>
    public const double SwitchNaturalWidth = 240;

    /// <summary>[S-11882] The Switch look's widest and tallest row; the row stays centred in a taller box.</summary>
    public static SizeDip SwitchMax => new(640, 3 * SwitchHeight);

    /// <summary>
    /// [S-11882] A quick toggle's or a power button's card (owner, 2026-11-15: "widgets like the toggle
    /// buttons should not have size limitations"): resized freely on both axes, from the glyph alone
    /// (<see cref="MinSide"/>) to <see cref="MaxSide"/>, landing at <see cref="NaturalSide"/>
    /// (<paramref name="stateLine"/>: a toggle's, which says on or off under its name); the Switch look a
    /// row from its glyph and switch alone to 640 wide. What it draws in its box is <see cref="Face"/>.
    /// </summary>
    public static WidgetSizing Sizing(TileLook look, bool stateLine = true)
        => look == TileLook.Switch
            ? WidgetSizing.Range(new(SwitchMinWidth, SwitchHeight), new(SwitchNaturalWidth, SwitchHeight), SwitchMax) with { Room = StretchRoom.Picture }
            : WidgetSizing.Range(new(MinSide, MinSide), new(NaturalSide(stateLine), NaturalSide(stateLine)), new(MaxSide, MaxSide)) with { Room = StretchRoom.Picture };

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
