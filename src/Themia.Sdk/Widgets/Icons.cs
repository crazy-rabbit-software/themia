using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-15001] What one read of an icon widget puts on its face: its glyph, what its colours say (<see cref="TileState"/>: at
/// rest, on as the accent, unavailable in the disabled role), its name (the tooltip and the screen reader's, "Play",
/// "Partly cloudy"), why it is unavailable (the tooltip then), the problems its status glyph says (a sky kept through a failed
/// refresh) and the app it came from (the player a click opens).
/// </summary>
public sealed record IconShown(string Glyph, TileState State = TileState.Off)
{
    /// <summary>[S-15001] What the icon is, in words: its tooltip while it can be read.</summary>
    public string? Name { get; init; }

    /// <summary>[S-15001] Why it is unavailable: its tooltip then.</summary>
    public string? Why { get; init; }

    /// <summary>
    /// [S-15001] The status glyph's problems (a reading kept through a failed refresh); [S-15711] while it cannot be read,
    /// the failure that left it so (a service out of reach), never an idle player's or a place not set.
    /// </summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>The app the icon came from (a media session's player); null for none.</summary>
    public string? Owner { get; init; }

    /// <summary>[S-15002] Nothing to show: <paramref name="glyph"/> (the kind's own) unavailable, <paramref name="why"/> its tooltip.</summary>
    public static IconShown None(string glyph, string? why = null) => new(glyph, TileState.Unavailable) { Why = why };

    /// <summary>[S-15001] Its tooltip: its name while it can be read, else why it cannot (its name with no reason given).</summary>
    public string? Tooltip => State == TileState.Unavailable ? (Why is { Length: > 0 } why ? why : Name) : Name;
}

/// <summary>
/// [S-15004] What a click on an icon widget opens, declared on its kind (<see cref="IconKind.Opens"/>): the target for the
/// card's config and what it shows now (null: nothing now, a place not set) and the card menu's line that opens it.
/// </summary>
public sealed record IconOpen(LocalText Label, Func<WidgetConfig, IconShown, CardOpen?> Target);

/// <summary>[S-15004] The targets the icon families open: each the one their family's own widgets open.</summary>
public static class IconOpens
{
    /// <summary>[S-2006] MSN Weather's forecast for the card's place, as the weather's metrics and texts open it.</summary>
    public static IconOpen Weather { get; } = new(MetricOpens.Weather.Label, (config, _) => MetricOpens.Weather.Target(config));
}

/// <summary>
/// [S-15000] [S-15002] The icon widget's rules, pure, one base for every widget whose face is one icon (owner, 2026-11-21:
/// "there should be a shared widget backend for widgets that show just an icon"): its size (a square that lands with its
/// glyph on the 40 step's line in the family's inset and goes down to the grid's least widget, the inset giving way under the 28
/// glyph's room), the step its glyph is planned on in a box (the face's fit scales its ink the rest of the way, until it
/// fills the inset's height or the width stops it) and the roles it is drawn in.
/// </summary>
public static class IconRules
{
    /// <summary>[S-15000] The steps a glyph is planned on as its box grows: the tile's (28, 40, then the Hero steps to 136).</summary>
    public static IReadOnlyList<TypeStep> Steps => TileRules.GlyphSteps;

    /// <summary>[S-15000] Where a new icon lands: the 40 step's line in the family's inset all round, 76 square (a desktop cell's width).</summary>
    public static double NaturalSide => (2 * FamilySpace.Inset) + FamilyType.Big40.LineHeight;

    /// <summary>
    /// [S-15000] The least an icon may be (WR-14: "a glyph 12, never under the grid's least widget"): the glyph at its floor
    /// in the least inset, never under <see cref="WidgetSizing.LeastScaled"/>, 32.
    /// </summary>
    public static double LeastSide => Math.Max(WidgetSizing.LeastScaled, ScaleFloor.Glyph + (2 * TileRules.TightInset));

    /// <summary>[S-15000] The largest the content asks for on either axis, a tile's (relaxed past it, [S-12411]).</summary>
    public const double MostSide = TileRules.MaxSide;

    /// <summary>
    /// [S-15000] An icon's card ([S-2438]): resized freely on both axes from <see cref="LeastSide"/>, landing a
    /// <see cref="NaturalSide"/> square, a picture's stretch (the glyph grows with the card, [S-12411]).
    /// </summary>
    public static WidgetSizing Sizing()
        => WidgetSizing.Range(new(LeastSide, LeastSide), new(NaturalSide, NaturalSide), new(MostSide, MostSide)) with { Room = StretchRoom.Picture };

    /// <summary>
    /// [S-15002] The inset round the glyph in a <paramref name="width"/> × <paramref name="height"/> card: the family's (12)
    /// while the box's short side holds the 28 glyph in it, else what that side leaves the 28 glyph on each side, down to
    /// the least inset (4), so a small card's glyph keeps the room it can (a tile's rule, [S-12806]). Unmeasured: the family's.
    /// </summary>
    public static double Inset(double width, double height)
    {
        var side = Side(width, height);
        if (!double.IsFinite(side))
        {
            return FamilySpace.Inset;
        }

        return Math.Clamp(Math.Floor((side - FamilyType.Big28.FontSize) / 2), TileRules.TightInset, FamilySpace.Inset);
    }

    /// <summary>[S-15002] The glyph's room in a card: its short side less the inset on both sides (never under 0).</summary>
    public static double Room(double width, double height)
    {
        var side = Side(width, height);
        return double.IsFinite(side) ? Math.Max(0, side - (2 * Inset(width, height))) : NaturalSide - (2 * FamilySpace.Inset);
    }

    /// <summary>
    /// [S-15716] Where a card says why beside its glyph (first where it stands whole there, [S-15722]): the glyph on the
    /// inset box's height, the why after it a column's gap on, at most what the width leaves (<paramref name="sentence"/>: its
    /// own width), the two one block centred across the card, clear of the status mark (<paramref name="marked"/>: its room
    /// kept at both ends, so the block stays centred); none
    /// where the card is under a Caption line's height, the glyph under its floor or the room under <see cref="WhyLine.MinBeside"/>.
    /// The glyph's ink fills the inset box's height: its box is as wide as its ink is then (<paramref name="aspect"/>: its
    /// ink's width over its height, a cloud's wider than tall), so the block is centred on what it draws.
    /// </summary>
    public static IconBeside? Beside(double width, double height, double sentence, bool marked, double aspect = 1)
    {
        if (!double.IsFinite(Side(width, height)) || height < WhyLine.BesideHeight)
        {
            return null;
        }

        var inset = Inset(width, height);
        var tall = height - (2 * inset);
        var glyph = tall * (aspect > 0 && double.IsFinite(aspect) ? aspect : 1);
        var across = width - (2 * inset) - (marked ? 2 * MarkRoom : 0);
        var room = across - glyph - FamilySpace.ColumnGap;
        if (tall < ScaleFloor.Glyph || room < WhyLine.MinBeside)
        {
            return null;
        }

        var text = Math.Min(double.IsFinite(sentence) ? Math.Max(0, sentence) : room, room);
        var left = (width - glyph - FamilySpace.ColumnGap - text) / 2;
        return new IconBeside(left, glyph, tall, left + glyph + FamilySpace.ColumnGap, text);
    }

    /// <summary>
    /// [S-15722] The why's compact line under the glyph: one line on Caption (12), shrunk as far as the floor (9) to stand whole
    /// in the card's width less 4 a side (<paramref name="sentence"/>: its width on Caption), its size its own, never the
    /// glyph's; none where it would stand whole only under the floor, or the inset box would hold under the 28 glyph over
    /// its band (a gap and its line). "Nothing playing" fits a 76 square at about 10; "Can't reach the service" does not.
    /// </summary>
    public static IconWhy? Under(double width, double height, double sentence)
    {
        if (!double.IsFinite(Side(width, height)) || !(sentence > 0) || !double.IsFinite(sentence))
        {
            return null;
        }

        var caption = FamilyType.Caption;
        var font = Math.Min(caption.FontSize, caption.FontSize * (width - (2 * FamilySpace.S1)) / sentence);
        if (font < ScaleFloor.Text)
        {
            return null;
        }

        var band = FamilySpace.S2 + (font * caption.LineHeight / caption.FontSize);
        return height - (2 * Inset(width, height)) - band >= FamilyType.Big28.FontSize ? new IconWhy(font, band) : null;
    }

    /// <summary>[S-15716] What the status mark takes at the card's end: its glyph and the gap before it.</summary>
    public static double MarkRoom => FamilyShape.Icon + FamilySpace.S2;

    /// <summary>
    /// [S-15717] The glyph's box in a card and where the status mark stands (<paramref name="marked"/>: a mark shows). The inset
    /// all round and the mark 4 in from the top end corner, over the inset ([S-15160]); but where the inset has given way
    /// (a card under 52), the mark would stand over the glyph itself (a stale cloud's upper end at 32 × 32): there the glyph
    /// takes the largest square clear of it, under it or before it, the mark moving to the card's very corner where 4 in
    /// leaves the glyph under its floor (12).
    /// </summary>
    public static IconBox Box(double width, double height, bool marked)
    {
        var inset = Inset(width, height);
        var plain = new IconBox(new(inset, inset, inset, inset), FamilySpace.S1);
        if (!marked || inset >= FamilySpace.Inset || !double.IsFinite(Side(width, height)))
        {
            return plain;
        }

        foreach (var at in new[] { FamilySpace.S1, 0.0 })
        {
            var corner = at + FamilyShape.Icon;
            var under = Math.Min(width - (2 * inset), height - inset - corner);
            var before = Math.Min(width - inset - corner, height - (2 * inset));
            if (Math.Max(under, before) >= ScaleFloor.Glyph || at == 0)
            {
                return under >= before
                    ? new IconBox(new(inset, corner, inset, inset), at)
                    : new IconBox(new(inset, inset, corner, inset), at);
            }
        }

        return plain;
    }

    /// <summary>
    /// [S-15002] The step the glyph is planned on in a card: the largest of <see cref="Steps"/> whose size its room holds
    /// (28 when none does), so the face's fit scales it up by less than one step's ratio and it is drawn sharp at any size.
    /// </summary>
    public static TypeStep Step(double width, double height)
    {
        var room = Room(width, height);
        var best = Steps[0];
        foreach (var step in Steps)
        {
            if (step.FontSize <= room)
            {
                best = step;
            }
        }

        return best;
    }

    /// <summary>
    /// [S-15001] The role the glyph is drawn in for <paramref name="state"/>: the item's text role at rest (so the widget's
    /// Item text colour, [S-6540], colours it as every glyph face's), on the accent's text on the accent, else the tile's.
    /// </summary>
    public static ColourRole Role(TileState state) => TileRules.Roles(TileLook.Tile, state).Glyph;

    /// <summary>A side of a card, or infinity while it is not measured.</summary>
    private static double Side(double width, double height)
        => width > 0 && height > 0 && double.IsFinite(width) && double.IsFinite(height) ? Math.Min(width, height) : double.PositiveInfinity;
}

/// <summary>[S-15716] A why beside the glyph: the glyph's box from the card's start, its width and height, the why's start and width.</summary>
public readonly record struct IconBeside(double GlyphLeft, double Glyph, double GlyphHeight, double TextLeft, double TextWidth);

/// <summary>[S-15722] The why's line under the glyph: its font size and its band (the gap over it and its line).</summary>
public readonly record struct IconWhy(double FontSize, double Band);

/// <summary>[S-15717] The glyph's box in a card (its distance from each edge) and where the status mark stands in from the top end corner.</summary>
public readonly record struct IconBox(IconEdges Glyph, double Mark);

/// <summary>[S-15717] Distances from a card's left, top, right and bottom edges.</summary>
public readonly record struct IconEdges(double Left, double Top, double Right, double Bottom);
