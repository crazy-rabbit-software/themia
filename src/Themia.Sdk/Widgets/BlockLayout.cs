using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-12814] [S-14280] How a block of parts fills its room: the <see cref="Scale"/> it is drawn at, the
/// <see cref="Height"/> it is laid out in (before the scale: its parts packed, never the room's, so they stay together)
/// and where its box's <see cref="Top"/> goes (the leading over the first ink above the room, or the band the width leaves).
/// </summary>
public readonly record struct BlockFill(double Scale, double Height, double Top);

/// <summary>
/// [S-12808] [S-12833] A part of a one-readout block as it is measured at its own size (its margin included): its box
/// (<see cref="Width"/> × <see cref="Height"/>), the room in it over its first ink and under its last (<see cref="Ink"/>),
/// before its first ink and after its last across (<see cref="Left"/>, <see cref="Right"/>: a text's side bearings), whether
/// it takes the room's whole width (<see cref="Spans"/>: a bar, a slider's row, a line it cuts), how much wider its cut
/// lines would ask to stand whole (<see cref="Uncut"/>, [S-14791]), the smallest text and glyph it draws
/// (<see cref="Text"/>, <see cref="Glyph"/>: infinite, none) and the text size it gives way at as a whole
/// (<see cref="GivesWayAt"/>: 0, never; [S-12833]).
/// </summary>
public readonly record struct PartBox(
    double Width,
    double Height,
    LineInk Ink = default,
    double Left = 0,
    double Right = 0,
    bool Spans = false,
    double Uncut = 0,
    double Text = double.PositiveInfinity,
    double Glyph = double.PositiveInfinity,
    double GivesWayAt = 0)
{
    /// <summary>[S-14301] Its ink's width: what of it fills the room's width (a text's side bearings are no part of it).</summary>
    public double Own => Math.Max(0, Width - Left - Right);

    /// <summary>Whether it is drawn where the parts of at least <paramref name="floor"/> are (<see cref="FaceParts.Keeps"/>).</summary>
    public bool Keeps(double floor) => GivesWayAt <= 0 || GivesWayAt >= floor - 1e-9;
}

/// <summary>[S-12833] What a block's face states for its give-way order: its main reading's text size and every size its pieces give way at.</summary>
public sealed record BlockFace(double Main, IReadOnlyList<double> Sizes)
{
    /// <summary>The face its kind states as rows (<see cref="FaceParts"/>), the same its sizing's least box is made of.</summary>
    public static BlockFace Of(FaceParts parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return new BlockFace(parts.Main, parts.Sizes);
    }

    /// <summary>A face of these text sizes, the largest its main reading; none: null (every part drawn).</summary>
    public static BlockFace? Of(IReadOnlyList<double> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        var usable = sizes.Where(s => s > 0 && double.IsFinite(s)).ToList();
        return usable.Count == 0 ? null : new BlockFace(usable.Max(), usable);
    }

    /// <summary>A face stated as rows is a face (none: none).</summary>
    public static implicit operator BlockFace?(FaceParts? parts) => parts is null ? null : Of(parts);

    /// <summary>Whether a piece of text <paramref name="size"/> is drawn where the parts of at least <paramref name="floor"/> are.</summary>
    public bool Keeps(double size, double floor) => size <= 0 || size >= Main || size >= floor - 1e-9;

    /// <summary>The same face: its main reading and its sizes, in order.</summary>
    public bool Equals(BlockFace? other) => other is not null && Main.Equals(other.Main) && Sizes.SequenceEqual(other.Sizes);

    public override int GetHashCode() => HashCode.Combine(Main, Sizes.Count);
}

/// <summary>[S-14301] Where a shown part stands, in the block's own units (before the scale, the room's origin at 0, 0): its box's top left and the width it is laid at.</summary>
public readonly record struct PartPlace(double X, double Y, double Width);

/// <summary>
/// [S-12808] A block laid in its room: the face's floor (<see cref="Floor"/>, the parts under it gave way, 0 none), the
/// fill (scale, packed height, top) and every input part's place (<see cref="Places"/>; null where it gave way). A place is
/// in the block's own units with the room's origin at 0, 0: drawn at <see cref="BlockFill.Scale"/> about that origin, a part
/// stands at (<c>X</c>, <c>Y</c>) times the scale.
/// </summary>
public sealed record BlockSolution(double Floor, BlockFill Fill, IReadOnlyList<PartPlace?> Places)
{
    /// <summary>Whether the part at <paramref name="index"/> is drawn.</summary>
    public bool Shown(int index) => Places[index] is not null;
}

/// <summary>
/// [S-12808] The one rule for a one-readout block (owner, 2026-11-19: "Keep them together"; WR-7, "All together, scaled";
/// WR-14). Its parts stand packed top to bottom, a <c>spacing</c> apart, and the block scales as one until its ink fills
/// the room's height or its parts' own width fills the room's width, whichever first (<see cref="Fill"/>); where the width
/// stops it, it stands centred on its ink in the height left ([S-12808]; on its foot instead, <c>foot</c>, [S-14319]);
/// across, a part that spans runs the room's width and every other stands its ink where the alignment puts it ([S-14301]).
/// A smaller room scales it down as one to its smallest drawn text's floor ([S-12833], <see cref="ScaleFloor"/>); with its
/// face stated (<see cref="BlockFace"/>) its main reading sets that floor and its smaller pieces give way first, smallest
/// first and in their kind's order ([S-14790], <see cref="Decide"/>). A line it cuts never lets it grow past where that line
/// stands whole ([S-14791], <see cref="Uncut"/>). Pure: the panel that draws a block (<c>ReadingFit</c>) measures its parts
/// and lays them where this says.
/// </summary>
public static class BlockLayout
{
    /// <summary>
    /// [S-12808] Lay a block whose parts, measured with the pieces of at least a floor drawn, are <paramref name="partsAt"/>
    /// of that floor (the same parts, in order, at every floor: a part that gave way as a whole has no box, a
    /// <see cref="PartBox"/> of 0 height and <see cref="PartBox.GivesWayAt"/> over the floor) in a room
    /// <paramref name="width"/> × <paramref name="height"/>.
    /// </summary>
    public static BlockSolution Solve(double width, double height, Func<double, IReadOnlyList<PartBox>> partsAt, BlockFace? face, ContentAlign align, double spacing = 0, bool foot = false)
    {
        ArgumentNullException.ThrowIfNull(partsAt);
        var floor = face is null ? 0 : Decide(width, height, face, partsAt, spacing);
        var parts = partsAt(floor);
        var fill = Fill(width, height, Measure(parts, floor, face, spacing), foot);
        return new BlockSolution(floor, fill, Place(width, parts, floor, fill, align, spacing));
    }

    /// <summary>[S-12833] <see cref="Solve(double, double, Func{double, IReadOnlyList{PartBox}}, BlockFace?, ContentAlign, double, bool)"/> for parts that give way as a whole only (<see cref="PartBox.GivesWayAt"/>).</summary>
    public static BlockSolution Solve(double width, double height, IReadOnlyList<PartBox> parts, BlockFace? face, ContentAlign align, double spacing = 0, bool foot = false)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return Solve(width, height, _ => parts, face, align, spacing, foot);
    }

    /// <summary>
    /// The block measured: the widest shown part's own width, the parts packed, their first ink's room over and last ink's
    /// room under, the least scale (their smallest text at 9, glyph at 12; none drawn, the face's main reading's), and their
    /// widest row with its cut lines standing whole ([S-14791]).
    /// </summary>
    public readonly record struct Measured(double Intrinsic, double Packed, LineInk Ink, double Least, double Whole);

    /// <summary>Measure the shown parts of <paramref name="parts"/> at <paramref name="floor"/>.</summary>
    public static Measured Measure(IReadOnlyList<PartBox> parts, double floor, BlockFace? face, double spacing)
    {
        ArgumentNullException.ThrowIfNull(parts);
        double intrinsic = 0, packed = 0, whole = 0, text = double.PositiveInfinity, glyph = double.PositiveInfinity;
        PartBox? first = null, last = null;
        var shown = 0;
        foreach (var part in parts)
        {
            if (!Drawn(part, floor))
            {
                continue;
            }

            shown++;
            first ??= part;
            last = part;
            packed += Room(part.Height);
            intrinsic = Math.Max(intrinsic, part.Own);

            // [S-14791] The part with the line it cuts standing whole.
            whole = Math.Max(whole, part.Own + Room(part.Uncut));
            text = Math.Min(text, part.Text);
            glyph = Math.Min(glyph, part.Glyph);
        }

        packed += Math.Max(0, shown - 1) * Room(spacing);

        // [S-12813] Sized and placed by the ink it draws (a placeholder's dashes too: WR-1, WR-4).
        var ink = first is { } f && last is { } l ? new LineInk(f.Ink.Over, l.Ink.Under) : LineInk.None;
        var anyText = double.IsFinite(text) || double.IsFinite(glyph);
        var least = anyText || face is null
            ? ScaleFloor.Least(double.IsFinite(text) ? text : 0, double.IsFinite(glyph) ? glyph : 0)
            : ScaleFloor.Least(face.Main);
        return new Measured(intrinsic, packed, ink, least, whole);
    }

    /// <summary>
    /// [S-12808] The fill of a measured block in a room <paramref name="width"/> × <paramref name="height"/>; [S-14319] on its
    /// <paramref name="foot"/>; [S-14324] never over <paramref name="most"/> (a line that keeps its own size, centred on its ink).
    /// </summary>
    public static BlockFill Fill(double width, double height, Measured block, bool foot = false, double most = double.PositiveInfinity)
    {
        most = Math.Min(most > 0 && !double.IsNaN(most) ? most : double.PositiveInfinity, block.Whole > block.Intrinsic ? Uncut(width, block.Whole) : double.PositiveInfinity);
        var fill = Fill(width, height, block.Intrinsic, block.Packed, block.Ink, block.Least, most);

        // [S-14319] Stood on its foot: where the width stops it, the height left stands over its first ink alone.
        return foot ? fill with { Top = (2 * fill.Top) + (Math.Max(0, block.Ink.Over) * fill.Scale) } : fill;
    }

    /// <summary>
    /// [S-14301] Every part's place in a room <paramref name="width"/> wide, drawn at <paramref name="fill"/>: packed from
    /// the block's top, a <paramref name="spacing"/> apart; across, a part that spans from the room's start to its end, every
    /// other its ink (its box less its side bearings) where <paramref name="align"/> puts it: at the start, centred, or at the
    /// end (owner, 2026-11-20, of a reading that fills one axis: "Centred is fine"). A part that gave way has none.
    /// </summary>
    public static IReadOnlyList<PartPlace?> Place(double width, IReadOnlyList<PartBox> parts, double floor, BlockFill fill, ContentAlign align, double spacing = 0)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var scale = fill.Scale > 0 && double.IsFinite(fill.Scale) ? fill.Scale : 1;
        var room = double.IsFinite(width) && width > 0 ? width / scale : 0;
        var share = align switch
        {
            ContentAlign.Left => 0.0,
            ContentAlign.Right => 1.0,
            _ => 0.5,
        };

        var places = new PartPlace?[parts.Count];
        var y = fill.Top / scale;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (!Drawn(part, floor))
            {
                continue;
            }

            places[i] = part.Spans
                ? new PartPlace(0, y, room)
                : new PartPlace((share * (room - part.Own)) - part.Left, y, part.Width);
            y += Room(part.Height) + Room(spacing);
        }

        return places;
    }

    /// <summary>
    /// [S-12814] The rule for a one-readout block (owner, 2026-11-19: "the drive widget still has a fucking space at the
    /// top, maybe also at the bottom. make sure that this does not happen anywhere"). Only the parts with a width of
    /// their own count against the room's width (<paramref name="intrinsicWidth"/>: the reading, its caption, an icon;
    /// a bar, a slider, a graph spans whatever it is given). The block scales up as one to the largest size at which
    /// those parts fit the width and the parts packed together (<paramref name="packedHeight"/>, their ink
    /// <paramref name="ink"/> inside it) fit the height: where the height stops it, its first ink stands on the room's
    /// top and its last on its foot. [S-14280] WR-7 (owner, 2026-11-19: "Keep them together"; "All together, scaled"):
    /// it is laid out in its parts' own height, so they always touch; where the width stops it, the height left stands
    /// alike over its first ink and under its last ([S-12808]), never between its parts. A room shorter than the parts
    /// keeps them packed from the top, the first ink on it. [S-12813] A block showing a placeholder ("--") is sized and
    /// placed by the ink it draws, as any (WR-1, WR-4: its dashes, lower than capitals, on the top where the height stops it
    /// and its last ink on the foot; sized by the reading it stood for, it left the difference as a band at the foot).
    /// [S-12833] A smaller room scales it down as one, never under its least scale (<paramref name="least"/>: its smallest
    /// text at the legibility floor, <see cref="ScaleFloor"/>).
    /// </summary>
    /// <remarks>[S-14791] Never over <paramref name="most"/> (<see cref="Uncut"/>: where a line it cuts stands whole).</remarks>
    public static BlockFill Fill(double width, double height, double intrinsicWidth, double packedHeight, LineInk ink = default, double least = 1, double most = double.PositiveInfinity)
    {
        var over = Room(ink.Over);
        var under = Room(ink.Under);
        if (!(packedHeight > 0) || !double.IsFinite(packedHeight) || !(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return new BlockFill(1, double.IsFinite(packedHeight) ? Math.Max(0, packedHeight) : 0, -over);
        }

        var span = Math.Max(1e-6, packedHeight - over - under);
        var byWidth = intrinsicWidth > 0 && double.IsFinite(intrinsicWidth) ? width / intrinsicWidth : double.PositiveInfinity;
        var fill = Math.Min(byWidth, height / span);
        var scale = Math.Max(ScaleFloor.Clamp(least), most > 0 && !double.IsNaN(most) ? Math.Min(most, fill) : fill);

        // [S-14280] WR-7: laid in its parts' own height, never the room's, so nothing goes between them. The height the
        // scale leaves (the width stopped it) stands alike over the reading's first ink and under its last ([S-12808]);
        // a room shorter than the parts keeps them from the top.
        var top = Math.Max(0, (height - (span * scale)) / 2);
        return new BlockFill(scale, packedHeight, top - (over * scale));
    }

    /// <summary>
    /// [S-14791] The most a block holding a line it cuts is drawn at (<see cref="Fill(double, double, double, double, LineInk, double, double)"/>'s
    /// <c>most</c>): the scale at which that line, and what stands beside it, stands whole in a room <paramref name="width"/>
    /// wide (<paramref name="whole"/>: the block's widest row with the line uncut, before the scale). A cut line never holds
    /// the block under its floor (it is cut there, [S-14261]) and never lets it grow past where it would stand whole (the
    /// next meeting's title alone at 370 × 80, filling the height at 2.8 times, drawn "Design re…" at 56 DIP). Nothing to
    /// hold: no most.
    /// </summary>
    public static double Uncut(double width, double whole)
        => whole > 0 && double.IsFinite(whole) && width > 0 && double.IsFinite(width) ? width / whole : double.PositiveInfinity;

    /// <summary>
    /// [S-12833] [S-14243] The face's floor in a room <paramref name="width"/> × <paramref name="height"/>, decided on its
    /// parts as they are drawn at each floor (<paramref name="partsAt"/>; owner, 2026-11-19, WR-7: "Keep them together";
    /// CLAUDE.md: size never decides content): its pieces give way smallest first only where the scale the block fills the
    /// room at would draw them under the legibility floor (<see cref="Decide(double, double, IReadOnlyCollection{double}, Func{double, FacePlan})"/>).
    /// </summary>
    public static double Decide(double width, double height, BlockFace face, Func<double, IReadOnlyList<PartBox>> partsAt, double spacing = 0)
    {
        ArgumentNullException.ThrowIfNull(face);
        ArgumentNullException.ThrowIfNull(partsAt);
        return Decide(width, height, face.Sizes, floor =>
        {
            var block = Measure(partsAt(floor), floor, face, spacing);
            return new FacePlan(block.Intrinsic, Math.Max(0, block.Packed - Room(block.Ink.Over) - Room(block.Ink.Under)));
        }).Floor;
    }

    /// <summary>
    /// [S-12833] The one rule for how small a face that scales as one goes (owner, 2026-11-19: "for the widgets that can
    /// scale there should basically be no lower limit except for one that makes sense so you can still see the widget"; "the
    /// clock has a min height that is way to big"). A face is its <b>main reading</b> (its largest text,
    /// <paramref name="sizes"/>' largest) and smaller parts. The main reading alone sets how small the face goes: until it is
    /// drawn at the legibility floor (<see cref="ScaleFloor.Text"/>, 9 DIP). A smaller part is drawn while the face's scale
    /// keeps it at that floor too and gives way under it, the smallest first, so no text is ever drawn under 9 and none holds
    /// the reading large; a part that went never comes back in a smaller room. [S-14318] What is left fills the room
    /// (<see cref="FaceFit.Scale"/>). <paramref name="plan"/>: the face with the parts of at least the size it is given.
    /// </summary>
    public static FaceFit Decide(double width, double height, IReadOnlyCollection<double> sizes, Func<double, FacePlan> plan)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        ArgumentNullException.ThrowIfNull(plan);
        var levels = Levels(sizes);
        if (levels.Count == 0 || !(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return FaceFit.All;
        }

        var main = levels[^1];
        var least = ScaleFloor.Least(main);
        var none = true;
        foreach (var floor in levels)
        {
            var fill = Scale(width, height, plan(floor));
            if (floor >= main || floor * fill >= ScaleFloor.Text - FaceFloor.Hair)
            {
                return new FaceFit(none ? 0 : floor, Math.Max(least, fill));
            }

            // It gives way here: the parts left are planned without it, and fill the room.
            none = false;
        }

        return new FaceFit(main, least);
    }

    /// <summary>The scale a plan fills a room at: its width or its ink's height stops it, whichever first (<see cref="Fill(double, double, double, double, LineInk, double, double)"/>'s rule).</summary>
    public static double Scale(double width, double height, FacePlan plan)
    {
        var byWidth = plan.Width > 0 && double.IsFinite(plan.Width) ? width / plan.Width : double.PositiveInfinity;
        var byHeight = plan.Ink > 0 && double.IsFinite(plan.Ink) ? height / plan.Ink : double.PositiveInfinity;
        var scale = Math.Min(byWidth, byHeight);
        return double.IsFinite(scale) ? scale : 1;
    }

    /// <summary>[S-12833] The smallest text size a face draws at <paramref name="floor"/> (infinite where only its main reading is left).</summary>
    public static double Kept(BlockFace face, double floor)
    {
        ArgumentNullException.ThrowIfNull(face);
        var kept = face.Sizes.Where(s => s > 0 && double.IsFinite(s) && s >= floor - 1e-9).DefaultIfEmpty(face.Main).Min();
        return kept >= face.Main ? double.PositiveInfinity : kept;
    }

    /// <summary>Whether a part stands in the block at <paramref name="floor"/>: it gave no way and has a box.</summary>
    private static bool Drawn(PartBox part, double floor) => part.Keeps(floor) && part.Height > 0;

    /// <summary>The distinct usable sizes, smallest first.</summary>
    private static List<double> Levels(IReadOnlyCollection<double> sizes)
        => [.. sizes.Where(s => s > 0 && double.IsFinite(s)).Distinct().Order()];

    private static double Room(double v) => double.IsFinite(v) ? Math.Max(0, v) : 0;
}
