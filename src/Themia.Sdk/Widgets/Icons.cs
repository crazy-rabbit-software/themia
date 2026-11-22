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
    /// [S-15710] Whether a card says why under its glyph while it cannot be read (<see cref="WhyLine"/>): its inset box holds
    /// the 28 glyph over the why's band and a sentence's room across. A 76 square does not (the why its tooltip).
    /// </summary>
    public static bool SaysWhy(double width, double height)
    {
        if (!double.IsFinite(Side(width, height)))
        {
            return false;
        }

        var inset = Inset(width, height);
        return WhyLine.Fits(width - (2 * inset), height - (2 * inset), FamilyType.Big28.FontSize);
    }

    /// <summary>
    /// [S-15002] The step the glyph is planned on in a card: the largest of <see cref="Steps"/> whose size its room holds
    /// (28 when none does), so the face's fit scales it up by less than one step's ratio and it is drawn sharp at any size.
    /// [S-15710] Over the why's line (<paramref name="why"/>) its room is the inset box less the line's band.
    /// </summary>
    public static TypeStep Step(double width, double height, bool why = false)
    {
        var room = why && double.IsFinite(Side(width, height))
            ? Math.Max(0, Math.Min(width, height - WhyLine.Band) - (2 * Inset(width, height)))
            : Room(width, height);
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
