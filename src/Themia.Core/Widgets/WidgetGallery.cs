using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using G = Themia.Sdk.Kit.ShellMetrics.WidgetGallery;

namespace Themia.Core.Widgets;

/// <summary>[S-14912] A run of whole grid cells: a card's box, counted from the sheet's top-left cell.</summary>
public readonly record struct CellBox(int X, int Y, int W, int H)
{
    public int Right => X + W;

    public int Bottom => Y + H;

    /// <summary>Whether the two share any cell (touching edges do not).</summary>
    public bool Overlaps(CellBox other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    /// <summary>Whether <paramref name="inner"/> lies wholly inside this box.</summary>
    public bool Holds(CellBox inner) => inner.X >= X && inner.Y >= Y && inner.Right <= Right && inner.Bottom <= Bottom;

    /// <summary>The box's middle, in cells.</summary>
    public (double X, double Y) Middle => (X + (W / 2.0), Y + (H / 2.0));
}

/// <summary>
/// [S-14912] One whole thing of the gallery: a widget that stands alone (<see cref="Kind"/>) or a ready-made group
/// (<see cref="Group"/>: several widgets laid as one panel). Never a small part of a family: those are the add page's.
/// </summary>
/// <param name="Key">The same in every language: the add page's tile key (<c>kind:clock</c>, <c>group:nowPlaying</c>).</param>
public sealed record GalleryPiece(string Key, WidgetId? Kind, ReadyMadeGroup? Group)
{
    /// <summary>[S-16726] The widget in this style (Quick settings shows the toggles in each); null: the style it has new.</summary>
    public int? Style { get; init; }

    /// <summary>[S-16562] A quick setting shown at its least (beside it at the size it has new): kept at that size.</summary>
    public bool Least { get; init; }

    /// <summary>[S-16685] The preset of <see cref="Kind"/> it shows, a piece of its own; null for the kind itself.</summary>
    public string? Preset { get; init; }

    /// <summary>[S-16685] What it shows and places: the kind or its preset; null for a group or the words' card.</summary>
    public WidgetPick? Pick => Kind is { } kind ? new WidgetPick(kind, Preset) : null;

    /// <summary>[S-16724] Drawn square (a quick setting's tile, a power button): as many DIPs down as across, kept at that size.</summary>
    public bool Square { get; init; }
}

/// <summary>[S-16725] A section of a category (Quick settings' toggles in each style, its power actions): its heading over its pieces.</summary>
public sealed record GallerySection(LocalText Heading, IReadOnlyList<GalleryPiece> Pieces);

/// <summary>[S-16725] A section's heading on a sheet: its words and its line, in cells from the sheet's corner.</summary>
public sealed record GalleryHeading(LocalText Text, CellBox Box);

/// <summary>[S-14912] A category of the gallery: an add page subject (or More), its whole pieces in the add page's order.</summary>
public sealed record GalleryCategory(AddFolderId Id, LocalText Title, string Glyph, IReadOnlyList<GalleryPiece> Pieces)
{
    /// <summary>[S-16725] Its pieces in sections under headings, laid in their order (Quick settings); null: one column bento.</summary>
    public IReadOnlyList<GallerySection>? Sections { get; init; }
}

/// <summary>
/// [S-14912] [S-16551] What a piece asks of the bento, in whole cells of the display's grid: its natural size, the least
/// and the most it may be, and on which axes it may be made another size (a widget on the axes its sizing leaves free; a
/// ready-made group across only, and only to the widths its common step lands on, <see cref="Widths"/>).
/// </summary>
public readonly record struct PieceSize(int W, int H, int MinW, int MinH, bool Wider, bool Taller)
{
    public int MaxW { get; init; } = int.MaxValue;

    public int MaxH { get; init; } = int.MaxValue;

    /// <summary>The widths it can be made, ascending (a ready-made group's); null: any from its least to its most.</summary>
    public IReadOnlyList<int>? Widths { get; init; }
}

/// <summary>[S-14912] A piece on a sheet: the box it is drawn at, in cells from the sheet's top-left corner.</summary>
public sealed record GalleryTile(GalleryPiece Piece, CellBox Box);

/// <summary>
/// [S-14912] A category laid as a bento: its tiles and how many rows of cells it is tall. [S-16550] Its
/// <see cref="Columns"/>: each column's cells across, from its top to its foot (the row under its last card).
/// </summary>
public sealed record GallerySheet(AddFolderId Category, LocalText Name, int Rows, IReadOnlyList<GalleryTile> Tiles)
{
    public IReadOnlyList<CellBox> Columns { get; init; } = [];

    /// <summary>[S-16721] Rows over each card its title takes, inside its tile's box (0: no titles, Quick settings).</summary>
    public int Title { get; init; }

    /// <summary>[S-16725] Its sections' headings (Quick settings); none for a column bento.</summary>
    public IReadOnlyList<GalleryHeading> Headings { get; init; } = [];

    /// <summary>[S-16721] The box a tile's card stands in: its tile's box under its title.</summary>
    public CellBox CardOf(GalleryTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return tile.Box with { Y = tile.Box.Y + Title, H = Math.Max(1, tile.Box.H - Title) };
    }
}

/// <summary>
/// [S-14911] [S-16400] [S-16401] The gallery's window on a display, in DIPs: the window, its head (the categories' tabs,
/// the hint and the search box) and the well the cards stand in; and <see cref="View"/>, the well's room in whole cells of
/// the display's grid.
/// </summary>
public sealed record GalleryFrame(DipRect Window, DipRect Head, DipRect Well, CellBox View);

/// <summary>[S-16566] Which hint the head shows: the whole sentence, the short one, or none.</summary>
public enum GalleryHint
{
    None,
    Short,
    Long,
}

/// <summary>[S-16407] A key that moves the pick across the bento.</summary>
public enum GalleryMove
{
    Left,
    Right,
    Up,
    Down,
}

/// <summary>[S-16408] What follows a placement off the gallery.</summary>
public enum GalleryAfter
{
    /// <summary>Given up: the gallery is back as it stood.</summary>
    Back,

    /// <summary>Landed: the gallery closes and the placed widgets' settings page opens.</summary>
    Settings,
}

/// <summary>
/// [S-14910]..[S-14917] [S-16400]..[S-16410] [S-16550]..[S-16564] The widget gallery's arrangement, pure (owner,
/// 2026-11-21: "for now lets just show complete widgets / groups, not the small atoms ... they should be shown as a bento";
/// 2026-11-23: "start with a column layout, the columns dont have to be all the same width, then place all the widgets in
/// them, optionally you can also put widgets besides each other at the same height in the column. the widgets size should
/// be defined by the columns"). A category is its whole pieces (<see cref="Of"/>), a search the first pieces that match
/// (<see cref="Find"/>), laid as one column bento (<see cref="Lay"/>): columns first, of whole cells and different widths,
/// filling the width; every piece in a column, as wide as it (or, side by side with others at one height, a share of it),
/// as tall as its sizing makes it there; the cards of a column one cell apart from its top to its foot, so a column has no
/// hole (<see cref="Holes"/>), and the columns' feet as even as the pieces' heights let them (<see cref="Ragged"/>), each
/// piece under its title ([S-16721]); Quick settings in sections under headings (<see cref="LaySections"/>, [S-16725]).
/// Deterministic; no two pieces share or touch a cell.
/// </summary>
public static class WidgetGallery
{
    /// <summary>[S-16403] Between two pieces, in cells: never touching, so no two pieces join ([S-2240]).</summary>
    public const int Gap = 1;

    /// <summary>[S-14913] How far one notch of the wheel scrolls a sheet, in rows of cells.</summary>
    public const int Step = 8;

    /// <summary>[S-16557] How many pieces a search shows at the most, best first: enough to browse, few enough to stay instant.</summary>
    public const int MostFound = 8;

    /// <summary>
    /// [S-16565] The families whose widgets the gallery shows whole in Quick settings, each alone at its two sizes, and whose
    /// ready-made group it leaves out (owner, 2026-11-23: "the power actions should be moved to the quick settings page").
    /// </summary>
    public static readonly IReadOnlySet<AddFolderId> InQuickSettings = new HashSet<AddFolderId> { AddFolderId.PowerButtons };

    /// <summary>[S-16550] How many of the ways that cost least are settled (rows moved between columns) before one is kept.</summary>
    private const int Settled = 3;

    /// <summary>[S-16551] How many pieces stand side by side in a column at the most (small ones that keep their size, a row of them).</summary>
    public const int MostSide = 16;

    /// <summary>
    /// [S-16723] Whether the gallery shows the ready-made groups (owner, 2026-11-23: "remove all widget compositions for now
    /// and only show the individual widgets that are available"): off, the one switch; their data stays (<see cref="ReadyMadeGroups"/>).
    /// </summary>
    public static readonly bool ShowsGroups = false;

    /// <summary>[S-16726] The power actions' order in Quick settings: lock first, the screen off last.</summary>
    public static readonly IReadOnlyList<string> PowerOrder = ["lock", "signOut", "sleep", "hibernate", "restart", "shutdown", "screenOff"];

    /// <summary>
    /// [S-14912] The categories of <paramref name="page"/>: every subject on its rail (and More), each its own widgets as the
    /// add page has them ([S-13611]); [S-16723] its ready-made groups only while <see cref="ShowsGroups"/>. A family's small
    /// parts (a fact, a button, a line of text) are left out. Start and desktop items stay the add page's. A subject with no
    /// whole piece is left out. [S-16724]..[S-16726] Quick settings is in sections under headings, never one bento: its
    /// toggles in each of their <paramref name="styles"/> (the first, the style a toggle has new, each at the size it has new
    /// and at its least, [S-16562]; each other style once), then the power actions (<see cref="InQuickSettings"/>, a family
    /// elsewhere, in <see cref="PowerOrder"/>, each at its two sizes), every one square where its sizing lets it be.
    /// </summary>
    public static IReadOnlyList<GalleryCategory> Of(AddPage page, Func<WidgetId, IReadOnlyList<StyleOption>>? styles = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        styles ??= WidgetStyles.For;
        var categories = new List<GalleryCategory>();
        var moved = page.Rail.SelectMany(r => page.Sections(r.Id))
            .Where(s => s.Kind == AddSectionKind.Family && s.Family is { } f && InQuickSettings.Contains(f.Id))
            .SelectMany(s => s.Tiles.OfType<AddWidgetTile>())
            .DistinctBy(t => t.Key)
            .OrderBy(t => PowerRank(t.Kind.Value))
            .ToList();
        foreach (var item in page.Rail)
        {
            if (item.Id == AddFolderId.Start)
            {
                continue;
            }

            var pieces = new List<GalleryPiece>();
            foreach (var section in page.Sections(item.Id))
            {
                switch (section.Kind)
                {
                    case AddSectionKind.ReadyMade when ShowsGroups:
                        pieces.AddRange(section.Tiles.OfType<AddGroupTile>().Where(t => !InQuickSettings.Contains(t.Group.Home)).Select(t => new GalleryPiece(t.Key, null, t.Group)));
                        break;
                    case AddSectionKind.Widgets:
                        pieces.AddRange(section.Tiles.OfType<AddWidgetTile>().Select(tile => new GalleryPiece(tile.Key, tile.Kind, null) { Preset = tile.Preset }));
                        break;
                }
            }

            if (item.Id == AddFolderId.QuickSettings)
            {
                categories.Add(QuickSettings(item, pieces, moved, styles));
            }
            else if (pieces.Count > 0)
            {
                categories.Add(new GalleryCategory(item.Id, item.Title, item.Glyph, pieces));
            }
        }

        return categories;
    }

    /// <summary>[S-16726] Where a power action stands in <see cref="PowerOrder"/>; any other after them.</summary>
    private static int PowerRank(string id)
    {
        for (var i = 0; i < PowerOrder.Count; i++)
        {
            if (string.Equals(PowerOrder[i], id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <summary>[S-16724]..[S-16726] Quick settings' sections: the toggles by style, then the power actions.</summary>
    private static GalleryCategory QuickSettings(AddRailItem item, List<GalleryPiece> toggles, List<AddWidgetTile> power, Func<WidgetId, IReadOnlyList<StyleOption>> styles)
    {
        var sections = new List<GallerySection>();
        var looks = toggles.Count == 0 ? [] : styles(toggles[0].Kind!.Value);
        if (looks.Count == 0)
        {
            sections.Add(new GallerySection(item.Title, [.. toggles.SelectMany(Twice)]));
        }
        else
        {
            for (var i = 0; i < looks.Count; i++)
            {
                var look = looks[i];
                sections.Add(new GallerySection(look.Label, i == 0
                    ? [.. toggles.SelectMany(Twice)]
                    : [.. toggles.Select(t => new GalleryPiece($"style:{t.Key}:{look.Value}", t.Kind, null) { Style = look.Value, Square = true, Preset = t.Preset })]));
            }
        }

        if (power.Count > 0)
        {
            sections.Add(new GallerySection(Str.AddMenu_Family_PowerButtons.Local(), [.. power.Select(t => new GalleryPiece(t.Key, t.Kind, null) { Preset = t.Preset }).SelectMany(Twice)]));
        }

        sections.RemoveAll(s => s.Pieces.Count == 0);
        return new GalleryCategory(item.Id, item.Title, item.Glyph, [.. sections.SelectMany(s => s.Pieces)]) { Sections = sections };

        static IEnumerable<GalleryPiece> Twice(GalleryPiece piece)
        {
            yield return piece with { Square = true };
            yield return new GalleryPiece("least:" + piece.Key, piece.Kind, null) { Least = true, Square = true, Preset = piece.Preset };
        }
    }

    /// <summary>
    /// [S-16402] [S-16557] What a search shows in place of a category: the add page's search over every widget and group
    /// (<see cref="AddPage.Find"/>, best first, the groups before the widgets), kept to the gallery's whole pieces (a family's
    /// small part it finds is left out), each once, the first <paramref name="most"/> of them; null while
    /// <paramref name="query"/> searches nothing. Laid as a category is.
    /// </summary>
    public static GalleryCategory? Find(AddPage page, IReadOnlyList<GalleryCategory> categories, string? query, int most = MostFound)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(categories);
        if (!AddPage.Searching(query))
        {
            return null;
        }

        var whole = new Dictionary<string, GalleryPiece>(StringComparer.Ordinal);
        foreach (var piece in categories.SelectMany(c => c.Pieces).Where(p => !p.Least))
        {
            whole.TryAdd(piece.Key, piece);
        }

        var found = new List<GalleryPiece>();
        foreach (var tile in page.Find(query).SelectMany(s => s.Tiles))
        {
            if (found.Count >= most)
            {
                break;
            }

            if (whole.TryGetValue(tile.Key, out var piece) && !found.Contains(piece))
            {
                found.Add(piece);
            }
        }

        return new GalleryCategory(AddFolderId.Browse, Str.WidgetGallery_Results.Local(), Themia.App.Shell.Glyphs.Search, found);
    }

    /// <summary>
    /// [S-16550]..[S-16553] <paramref name="category"/> as a column bento <paramref name="width"/> cells wide. Columns come
    /// first: a few, of whole cells and different widths, filling the width (as far as the pieces can be that wide); each
    /// way of cutting the width into columns is filled and the one that fits the pieces best is kept (<see cref="Plans"/>).
    /// Filling: the shortest column (then the left-most) takes next the piece, or the pieces side by side at one height (at
    /// most <see cref="MostSide"/>), that its width suits best, the larger first; a piece is as wide as its column (or its share
    /// of it) and as tall as its sizing makes it at that width (its natural height scaled with its width, within its least and
    /// most). The cards of a column stand one cell apart from its top down, so there is no hole in a column; then rows move
    /// between columns (<see cref="Settle"/>), a column whose foot stays high goes on as two under its last card
    /// (<see cref="Divide"/>), and the rows are made taller or shorter, within what their pieces take, so the columns' feet come
    /// as even as they can (<see cref="Even"/>).
    /// </summary>
    /// <para>
    /// [S-16721] Every piece's title stands over its card inside its tile: <paramref name="title"/> rows more down, its width
    /// the card's, so the columns count it and no card stands over another's title. [S-16725] A category in sections is laid
    /// by <see cref="LaySections"/> (its headings, <paramref name="heading"/> rows each, and no titles).
    /// </para>
    public static GallerySheet Lay(GalleryCategory category, int width, Func<GalleryPiece, PieceSize> size, int title = 0, int heading = 1)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(size);
        if (category.Sections is { } sections)
        {
            return LaySections(category, sections, width, size, heading);
        }

        var cols = Math.Max(1, width);
        title = Math.Max(0, title);
        var shapes = category.Pieces.Select(p => new Shape(size(p), cols, title)).ToList();
        if (shapes.Count == 0)
        {
            return new GallerySheet(category.Id, category.Title, 0, []) { Title = title };
        }

        // Every way filled and its feet evened; the few that cost least settled (rows moved between columns) and evened again.
        var solved = new Solver(shapes);
        var ways = new List<(int[] Plan, Filled Filled)>();
        foreach (var plan in Plans(shapes, cols))
        {
            var filled = Fill(solved, Columns(plan, 0, 0), ordered: false);
            ways.Add((plan, filled with { Cost = Cost(Evened(filled, solved), shapes, cols) }));
        }

        Filled? best = null;
        foreach (var (_, filled) in ways.OrderBy(w => w.Filled.Cost).Take(Settled))
        {
            Settle(filled.Columns, solved);
            Divide(filled.Columns, solved);
            var done = Evened(filled, solved);
            done = done with { Cost = Cost(done, shapes, cols) };
            if (best is null || done.Cost < best.Cost - 1e-9)
            {
                best = done;
            }
        }

        return Sheet(category, best!, 0) with { Title = title };
    }

    /// <summary>
    /// [S-16725] A category in sections (Quick settings, owner, 2026-11-23: "you should just add headers to the columns or
    /// individual sections there ... order them as well so that the actions dont interweave with the toggle buttons"): each
    /// section its heading's line (<paramref name="heading"/> rows, the sheet's width) and under it its pieces in their order,
    /// left to right one cell apart, a new row where the width ends (a row as tall as its tallest), every piece at the size it
    /// asks; one cell between a section's last row and the next heading. No piece of one section among another's.
    /// </summary>
    public static GallerySheet LaySections(GalleryCategory category, IReadOnlyList<GallerySection> sections, int width, Func<GalleryPiece, PieceSize> size, int heading)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(size);
        var cols = Math.Max(1, width);
        heading = Math.Max(1, heading);
        var tiles = new List<GalleryTile>();
        var headings = new List<GalleryHeading>();
        var y = 0;
        foreach (var section in sections)
        {
            if (section.Pieces.Count == 0)
            {
                continue;
            }

            if (headings.Count > 0)
            {
                y += Gap;
            }

            headings.Add(new GalleryHeading(section.Heading, new CellBox(0, y, cols, heading)));
            y += heading;
            var (x, tallest) = (0, 0);
            foreach (var piece in section.Pieces)
            {
                var asked = size(piece);
                var (w, h) = (Math.Clamp(asked.W, 1, cols), Math.Max(1, asked.H));
                if (x > 0 && x + w > cols)
                {
                    y += tallest + Gap;
                    (x, tallest) = (0, 0);
                }

                tiles.Add(new GalleryTile(piece, new CellBox(x, y, w, h)));
                x += w + Gap;
                tallest = Math.Max(tallest, h);
            }

            y += tallest;
        }

        return new GallerySheet(category.Id, category.Title, y, tiles) { Headings = headings };
    }

    // ---- [S-16550] the columns ------------------------------------------------------------------------------------------

    /// <summary>
    /// [S-16550] The ways of cutting <paramref name="width"/> cells into columns. A few columns: from three fifths to half as
    /// many again as pieces of the middle width fill the width (never more than eight, nor than the pieces; few pieces may
    /// stand in one): for each count the width cut evenly, cut after the pieces' own widths (the widest first, scaled to
    /// fill), and the same held to the most a piece may be (few pieces cannot fill it); then one column as wide as a width a
    /// piece keeps (or a row of such pieces, or the widest piece's least) and the rest cut evenly; and the fewest widths that
    /// take every piece alone (<see cref="Stab"/>), with or without more columns in what is left. A way with no column the
    /// widest piece's least fits in is left out. Each once, in this order.
    /// </summary>
    private static List<int[]> Plans(List<Shape> shapes, int width)
    {
        var plans = new List<int[]>();
        void Add(int[] plan)
        {
            if (plan.Length > 0 && plan.All(w => w >= 1) && plan.Sum() + ((plan.Length - 1) * Gap) <= width
                && plan.Max() >= shapes.Max(s => s.LeastW) && !plans.Exists(p => p.SequenceEqual(plan)))
            {
                plans.Add(plan);
            }
        }

        // A few columns: about as many as pieces of the middle width fill the width (three fifths to half as many again),
        // never more than there are pieces, so a piece is near its own width in its column.
        var natural = shapes.Select(s => s.W).OrderDescending().ToList();
        var narrowest = shapes.Select(s => s.LeastW).Order().ElementAt(shapes.Count / 2);
        var fits = (width + Gap) / (double)(natural[natural.Count / 2] + Gap);
        var most = Math.Clamp((int)Math.Ceiling(fits * 1.5), 1, Math.Min(8, shapes.Count));
        var fewest = Math.Clamp((int)Math.Ceiling(fits * 0.6), 1, Math.Max(1, Math.Min(most, shapes.Count / 2)));
        var widest = shapes.Max(s => s.MostW);

        // The widths pieces keep (and rows of several of one such size): a column of exactly that takes them without a hole;
        // and the least the widest piece may be, so a column takes it.
        var kept = new SortedSet<int> { shapes.Max(s => s.LeastW) };
        foreach (var group in shapes.Where(s => s.Kept).GroupBy(s => (s.W, s.H)))
        {
            for (var n = 1; n <= Math.Min(group.Count(), 16); n++)
            {
                kept.Add((n * (group.Key.W + Gap)) - Gap);
            }
        }

        for (var k = fewest; k <= most; k++)
        {
            Add(Split(width, k));

            // After the pieces' own widths: k runs of the widths (widest first), each column the run's middle, scaled to fill.
            var wanted = Enumerable.Range(0, k).Select(i => natural[Math.Min(natural.Count - 1, ((2 * i) + 1) * natural.Count / (2 * k))]).ToArray();
            Add(Scaled(wanted, width));

            // Few pieces: the columns no wider than a piece may be, or as the pieces are (the width not filled: they cannot be that wide).
            Add([.. Split(width, k).Select(w => Math.Min(w, widest))]);
            Add([.. Scaled(wanted, width).Select(w => Math.Min(w, widest))]);
            Add(wanted);
        }

        // A column as wide as a kept width, the rest cut evenly into one to as many columns as the rest of the width holds.
        foreach (var w in kept.Where(w => w <= width))
        {
            var left = width - w - Gap;
            var room = left < 1 ? 0 : Math.Min(most - 1, (left + Gap) / (Math.Max(1, narrowest) + Gap));
            if (room == 0)
            {
                Add([w]);
            }

            for (var k = Math.Max(1, Math.Min(room, fewest - 2)); k <= room; k++)
            {
                Add([w, .. Split(left, k)]);
            }
        }

        // Every piece a column it may be as wide as: the fewest widths that take each piece alone (the narrowest first, each
        // the most the next piece left may be), the rest of the width as one to three more columns, or not at all.
        if (Stab(shapes, width) is { Count: > 1 } stab)
        {
            var left = width - stab.Sum() - (stab.Count * Gap);
            Add([.. stab]);
            for (var k = 1; k <= 3 && left >= k * Math.Max(1, narrowest); k++)
            {
                Add([.. stab, .. Split(left, k)]);
            }
        }

        if (plans.Count == 0)
        {
            plans.Add([width]);
        }

        return plans;
    }

    /// <summary>
    /// [S-16550] The fewest column widths that take every piece alone (each a width it may be; the narrowest piece first, its
    /// most, then each as narrow as the pieces only it takes allow), widest first; null where they do not fit in
    /// <paramref name="width"/> together.
    /// </summary>
    private static List<int>? Stab(List<Shape> shapes, int width)
    {
        var widths = new List<int>();
        foreach (var shape in shapes.OrderBy(s => s.MostW).ThenBy(s => s.LeastW))
        {
            if (widths.Exists(shape.Takes))
            {
                continue;
            }

            widths.Add(shape.Below(shape.MostW));
        }

        // Each width made as narrow as the pieces only it takes allow: the least they all may be.
        for (var i = 0; i < widths.Count; i++)
        {
            var at = i;
            var only = shapes.Where(s => s.Takes(widths[at]) && !widths.Where((_, j) => j != at).Any(s.Takes)).ToList();
            if (only.Count == 0)
            {
                continue;
            }

            for (var w = only.Max(s => s.LeastW); w < widths[at]; w++)
            {
                if (only.TrueForAll(s => s.Takes(w)))
                {
                    widths[at] = w;
                    break;
                }
            }
        }

        widths.Sort((a, b) => b.CompareTo(a));
        return widths.Sum() + ((widths.Count - 1) * Gap) <= width ? widths : null;
    }

    /// <summary><paramref name="width"/> cells cut into <paramref name="k"/> columns one cell apart, as even as whole cells go (the wider first).</summary>
    private static int[] Split(int width, int k)
    {
        var room = width - ((k - 1) * Gap);
        return [.. Enumerable.Range(0, k).Select(i => (room / k) + (i < room % k ? 1 : 0))];
    }

    /// <summary><paramref name="wanted"/> widths scaled to fill <paramref name="width"/> with their gaps, in whole cells.</summary>
    private static int[] Scaled(int[] wanted, int width)
    {
        var room = width - ((wanted.Length - 1) * Gap);
        var sum = Math.Max(1, wanted.Sum());
        var plan = wanted.Select(w => Math.Max(1, (int)Math.Floor(w * room / (double)sum))).ToArray();
        for (var i = 0; plan.Sum() < room; i = (i + 1) % plan.Length)
        {
            plan[i]++;
        }

        return plan;
    }

    private static List<Col> Columns(int[] plan, int x, int top)
    {
        var columns = new List<Col>();
        foreach (var w in plan)
        {
            columns.Add(new Col(x, w, top));
            x += w + Gap;
        }

        return columns;
    }

    // ---- [S-16551] filling them -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-16551] The pieces into <paramref name="columns"/>. Not <paramref name="ordered"/> (a category): the shortest column
    /// takes next the row (one piece, or several side by side at one height) its width suits best, the pieces that keep their
    /// size first, then the larger.
    /// <paramref name="ordered"/> (a focus): the pieces in their order, each into the column it suits where it stands highest.
    /// A piece no column can take whole (its least wider than the room) is not left out: it stands at the most its column takes.
    /// </summary>
    private static Filled Fill(Solver solved, List<Col> columns, bool ordered)
    {
        var shapes = solved.Shapes;
        // A category: the pieces that keep their size first (the hardest to fit), then the larger first.
        var left = Enumerable.Range(0, shapes.Count)
            .OrderByDescending(i => !ordered && shapes[i].Kept)
            .ThenByDescending(i => ordered ? 0 : (long)shapes[i].W * shapes[i].H)
            .ThenBy(i => i)
            .ToList();
        var forced = 0;
        while (left.Count > 0)
        {
            Row? row = null;
            Col? into = null;
            if (ordered)
            {
                var best = double.MaxValue;
                foreach (var col in columns.Where(c => !c.Closed))
                {
                    foreach (var option in Options(left[0], col, solved, left))
                    {
                        var score = col.Next + option.H + (Fit * option.Strain * shapes[left[0]].H);
                        if (score < best - 1e-9)
                        {
                            (best, row, into) = (score, option, col);
                        }
                    }
                }
            }
            else
            {
                foreach (var col in columns.OrderBy(c => c.Foot).ThenBy(c => c.X))
                {
                    var best = double.MaxValue;
                    var seen = new HashSet<ShapeKey>();
                    for (var rank = 0; rank < left.Count; rank++)
                    {
                        // Of pieces that ask the same, the first alone: the rest would make the same rows.
                        if (!seen.Add(shapes[left[rank]].Key))
                        {
                            continue;
                        }

                        foreach (var option in Options(left[rank], col, solved, left))
                        {
                            var score = option.Strain + (Order * rank / left.Count) + (Side * (option.Members.Count - 1));
                            if (score < best - 1e-9)
                            {
                                (best, row, into) = (score, option, col);
                            }
                        }
                    }

                    if (row is not null)
                    {
                        break;
                    }
                }
            }

            if ((row is null || into is null) && ordered && columns.Count > 0)
            {
                // A focus: no room round the piece takes it: a column as wide as all of them under all of them, the rest
                // closed (nothing more stands where it could reach into it).
                var x0 = columns.Min(c => c.X);
                var under = new Col(x0, columns.Max(c => c.X + c.W) - x0, columns.Max(c => c.Rows.Count == 0 ? c.Top : c.Foot + Gap));
                foreach (var col in columns)
                {
                    col.Closed = true;
                }

                columns.Add(under);
                if (solved.Solve([left[0]], under.W) is { } whole)
                {
                    (row, into) = (whole, under);
                }
            }

            if ((row is null || into is null) && Join(left[0], columns, solved) is not null)
            {
                // No column takes it alone: it joins a row that takes it beside its pieces.
                left.RemoveAt(0);
                continue;
            }

            if (row is null || into is null)
            {
                // No column takes it at all: at the most the widest column takes, where it stands highest.
                var piece = left[0];
                into = columns.OrderByDescending(c => c.W).ThenBy(c => c.Foot).First();
                var w = shapes[piece].Below(into.W);
                var h = shapes[piece].HeightAt(w);
                row = new Row([(piece, w)], h, h, h, 1);
                forced += (into.W - w) * h;
            }

            into.Rows.Add(row);
            foreach (var (piece, _) in row.Members)
            {
                left.Remove(piece);
            }
        }

        return new Filled(columns, forced, 0);
    }

    /// <summary>
    /// [S-16551] <paramref name="piece"/> beside the pieces of a row already laid, the row laid again across its column with
    /// it (the one that strains least); null where no row takes it.
    /// </summary>
    private static Row? Join(int piece, List<Col> columns, Solver solved)
    {
        (Col Col, int At, Row Row)? best = null;
        foreach (var col in columns)
        {
            for (var i = 0; i < col.Rows.Count; i++)
            {
                if (solved.Solve([.. col.Rows[i].Members.Select(m => m.Piece), piece], col.W) is { } row && (best is null || row.Strain < best.Value.Row.Strain - 1e-9))
                {
                    best = (col, i, row);
                }
            }
        }

        if (best is not { } chosen)
        {
            return null;
        }

        chosen.Col.Rows[chosen.At] = chosen.Row;
        return chosen.Row;
    }

    /// <summary>[S-16552] What the room under the shorter columns' feet weighs against the pieces' strain: even feet first.</summary>
    private const double Uneven = 3;

    /// <summary>[S-16550] What the width left unfilled weighs, as a share of the pieces' own area: they fill it if they can.</summary>
    private const double Unfilled = 6;

    /// <summary>How much a piece's strain weighs against standing higher, for a focus.</summary>
    private const double Fit = 2;

    /// <summary>How much the larger first weighs against a better fit, for a category.</summary>
    private const double Order = 0.6;

    /// <summary>What standing beside another costs over standing alone: alone, where it suits as well.</summary>
    private const double Side = 0.15;

    /// <summary>A piece alone this near its own size (a third wider or narrower) is not looked at beside others.</summary>
    private const double Suits = 0.3;

    /// <summary>[S-16551] [S-16552] How much shorter and taller than its sizing makes it at a width a piece may stand, to even the feet.</summary>
    private const double Shortest = 0.6;

    private const double Tallest = 1.5;

    /// <summary>
    /// [S-16551] The rows <paramref name="piece"/> can stand in, in <paramref name="col"/>: alone as wide as it, and with the
    /// pieces still to place (those of a height like its own first) side by side, as long as their own widths leave room.
    /// </summary>
    private static IEnumerable<Row> Options(int piece, Col col, Solver solved, List<int> left)
    {
        var shapes = solved.Shapes;
        if (solved.Solve([piece], col.W) is { } alone)
        {
            yield return alone;

            // Alone near its own size: no row of several is looked for.
            if (alone.Strain <= Suits)
            {
                yield break;
            }
        }

        var me = shapes[piece];
        var partners = left.Where(q => q != piece).OrderBy(q => Math.Abs(shapes[q].H - me.H)).ThenBy(q => q).Take(MostSide).ToList();
        List<int> members = [piece];
        while (members.Count < MostSide && members.Sum(m => shapes[m].LeastW) + (members.Count * Gap) < col.W)
        {
            Row? best = null;
            var next = -1;
            foreach (var q in partners.Where(q => !members.Contains(q)))
            {
                if (solved.Solve([.. members, q], col.W) is { } row && (best is null || row.Strain < best.Strain - 1e-9))
                {
                    (best, next) = (row, q);
                }
            }

            if (best is null)
            {
                // None fits yet: the closest in height comes along, and the next may make the row.
                next = partners.FirstOrDefault(q => !members.Contains(q), -1);
                if (next < 0)
                {
                    yield break;
                }

                members.Add(next);
                continue;
            }

            members.Add(next);
            yield return best;
        }
    }

    /// <summary>
    /// [S-16551] <paramref name="members"/> side by side across <paramref name="width"/> cells, one cell apart, at one height:
    /// each as wide as its share of the room (its own width in proportion, held to what it may be), every cell taken; the
    /// height one each of them may be there, near what their sizing makes them. Null where no such widths or height exist.
    /// </summary>
    private static Row? Solve(List<int> members, int width, List<Shape> shapes)
    {
        var room = width - ((members.Count - 1) * Gap);
        if (room < members.Count || members.Sum(m => shapes[m].LeastW) > room || members.Sum(m => shapes[m].MostW) < room)
        {
            return null;
        }

        var natural = Math.Max(1, members.Sum(m => shapes[m].W));
        var ws = members.Select(m => shapes[m].Nearest((int)Math.Round(shapes[m].W * room / (double)natural))).ToArray();
        for (var guard = 0; guard < 4 * room && ws.Sum() != room; guard++)
        {
            var more = ws.Sum() < room;
            var pick = -1;
            var ratio = more ? double.MaxValue : double.MinValue;
            for (var i = 0; i < ws.Length; i++)
            {
                var shape = shapes[members[i]];
                if ((more ? shape.Above(ws[i]) : shape.Under(ws[i])) is not { } to || Math.Abs(to - ws[i]) > Math.Abs(room - ws.Sum()))
                {
                    continue;
                }

                var r = ws[i] / (double)shape.W;
                if (more ? r < ratio : r > ratio)
                {
                    (pick, ratio) = (i, r);
                }
            }

            if (pick < 0)
            {
                return null;
            }

            var s = shapes[members[pick]];
            ws[pick] = more ? s.Above(ws[pick])!.Value : s.Under(ws[pick])!.Value;
        }

        if (ws.Sum() != room)
        {
            return null;
        }

        var (lo, hi) = (int.MinValue, int.MaxValue);
        var wanted = 0.0;
        for (var i = 0; i < ws.Length; i++)
        {
            var (l, h) = shapes[members[i]].Heights(ws[i]);
            (lo, hi) = (Math.Max(lo, l), Math.Min(hi, h));
            wanted += shapes[members[i]].HeightAt(ws[i]);
        }

        if (lo > hi)
        {
            return null;
        }

        var height = Math.Clamp((int)Math.Round(wanted / ws.Length), lo, hi);
        var strain = 0.0;
        for (var i = 0; i < ws.Length; i++)
        {
            var shape = shapes[members[i]];
            strain += Math.Abs(Math.Log(ws[i] / (double)shape.W)) + (0.5 * Math.Abs(Math.Log(height / (double)Math.Max(1, shape.HeightAt(ws[i])))));
        }

        return new Row([.. members.Select((m, i) => (m, ws[i]))], height, lo, hi, strain / ws.Length);
    }

    // ---- [S-16552] even feet --------------------------------------------------------------------------------------------

    /// <summary>
    /// [S-16552] Rows moved between columns while that brings their feet closer: a row of a taller column (laid again at the
    /// other's width) into a shorter one, alone or beside a row there, or a lone piece of each swapped, whichever brings the feet nearest their mean; until
    /// none does. What keeps its height (ready-made groups) is evened this way; what stretches, by <see cref="Even"/>.
    /// </summary>
    private static void Settle(List<Col> columns, Solver solved)
    {
        if (columns.Count < 2)
        {
            return;
        }

        double Spread() => WidgetGallery.Spread(columns);

        for (var guard = 0; guard < 48; guard++)
        {
            var now = Spread();
            (double Spread, Action Apply)? best = null;
            void Try(Action change, Action undo)
            {
                change();
                var after = Spread();
                undo();
                if (after < now - 1e-9 && (best is null || after < best.Value.Spread - 1e-9))
                {
                    best = (after, change);
                }
            }

            foreach (var tall in columns.Where(c => !c.Closed))
            {
                foreach (var low in columns.Where(c => !c.Closed && c.Foot < tall.Foot))
                {
                    for (var i = 0; i < tall.Rows.Count; i++)
                    {
                        var row = tall.Rows[i];
                        var members = row.Members.Select(m => m.Piece).ToList();

                        // Beside a row of the shorter column, laid again across it with them.
                        for (var j = 0; j < low.Rows.Count; j++)
                        {
                            var other = low.Rows[j];
                            if (solved.Solve([.. other.Members.Select(m => m.Piece), .. members], low.W) is { } joined)
                            {
                                var (a, b) = (i, j);
                                Try(() => { tall.Rows.RemoveAt(a); low.Rows[b] = joined; }, () => { low.Rows[b] = other; tall.Rows.Insert(a, row); });
                            }
                        }

                        if (solved.Solve(members, low.W) is not { } there)
                        {
                            continue;
                        }

                        var at = i;
                        Try(() => { tall.Rows.RemoveAt(at); low.Rows.Add(there); }, () => { low.Rows.Remove(there); tall.Rows.Insert(at, row); });
                        if (members.Count > 1)
                        {
                            continue;
                        }

                        for (var j = 0; j < low.Rows.Count; j++)
                        {
                            var other = low.Rows[j];
                            if (other.Members.Count == 1 && other.H < row.H && solved.Solve([other.Members[0].Piece], tall.W) is { } here)
                            {
                                var b = j;
                                Try(() => { tall.Rows[at] = here; low.Rows[b] = there; }, () => { tall.Rows[at] = row; low.Rows[b] = other; });
                            }
                        }
                    }
                }
            }

            if (best is not { } chosen)
            {
                return;
            }

            chosen.Apply();
        }
    }

    /// <summary>How far the open columns' feet are from their mean: the sum of the squares.</summary>
    private static double Spread(List<Col> columns)
    {
        var open = columns.Where(c => !c.Closed).ToList();
        if (open.Count == 0)
        {
            return 0;
        }

        var mean = open.Average(c => c.Foot);
        return open.Sum(c => (c.Foot - mean) * (c.Foot - mean));
    }

    /// <summary>
    /// [S-16552] A column whose foot stands far above the others' (what is left fits it only in narrower widths) goes on as
    /// two columns side by side under its last row, and rows are settled into them (<see cref="Settle"/>), as long as that
    /// brings the feet nearer: a wide piece over two narrower columns, never a hole beside a column's last card.
    /// </summary>
    private static void Divide(List<Col> columns, Solver solved)
    {
        var narrowest = solved.Shapes.Min(s => s.LeastW);
        for (var guard = 0; guard < 4; guard++)
        {
            var before = Spread(columns);
            var low = columns.Where(c => !c.Closed && c.Rows.Count > 0 && c.W >= (2 * narrowest) + Gap).OrderBy(c => c.Foot).ThenBy(c => c.X).FirstOrDefault();
            if (low is null || before <= 1)
            {
                return;
            }

            var kept = columns.Select(c => (Col: c, Rows: c.Rows.ToList())).ToList();
            var halves = Columns(Split(low.W, 2), low.X, low.Next);
            low.Closed = true;
            columns.AddRange(halves);
            Settle(columns, solved);
            if (halves.Exists(h => h.Rows.Count > 0) && Spread(columns) < before - 1e-9)
            {
                continue;
            }

            // Nothing nearer: as it was.
            foreach (var half in halves)
            {
                columns.Remove(half);
            }

            foreach (var (col, rows) in kept)
            {
                col.Rows.Clear();
                col.Rows.AddRange(rows);
            }

            low.Closed = false;
            return;
        }
    }

    /// <summary>
    /// [S-16552] The columns' feet brought as even as their rows let them: the foot most columns can reach (each row made
    /// taller or shorter within what its pieces take there), and each column's rows grown or shrunk a cell at a time toward it,
    /// the row furthest from its own height first. A column of rows that keep their height (ready-made groups) keeps its foot.
    /// </summary>
    private static Filled Evened(Filled filled, Solver solved)
    {
        Even(filled.Columns, solved.Shapes);
        return filled;
    }

    private static void Even(List<Col> columns, List<Shape> shapes)
    {
        var used = columns.Where(c => !c.Closed && c.Rows.Count > 0).ToList();
        if (used.Count < 2)
        {
            return;
        }

        var low = used.Select(c => c.Top + c.Rows.Sum(r => r.Lo) + ((c.Rows.Count - 1) * Gap)).ToList();
        var high = used.Select(c => c.Top + c.Rows.Sum(r => r.Hi) + ((c.Rows.Count - 1) * Gap)).ToList();
        var feet = used.Select(c => c.Foot).ToList();

        // The foot: least room left over all columns, then the least change from where they stand.
        var (target, miss, moved) = (0, long.MaxValue, long.MaxValue);
        for (var t = low.Min(); t <= high.Max(); t++)
        {
            long m = 0, d = 0;
            for (var i = 0; i < used.Count; i++)
            {
                var reach = Math.Clamp(t, low[i], high[i]);
                m += (long)Math.Abs(t - reach) * used[i].W;
                d += Math.Abs(reach - feet[i]);
            }

            if (m < miss || (m == miss && d < moved))
            {
                (target, miss, moved) = (t, m, d);
            }
        }

        foreach (var col in used)
        {
            var want = Math.Clamp(target, col.Top + col.Rows.Sum(r => r.Lo) + ((col.Rows.Count - 1) * Gap), col.Top + col.Rows.Sum(r => r.Hi) + ((col.Rows.Count - 1) * Gap));
            while (col.Foot != want)
            {
                var grow = col.Foot < want;
                var row = col.Rows.Where(r => grow ? r.H < r.Hi : r.H > r.Lo)
                    .OrderBy(r => grow ? r.H / (double)Math.Max(1, r.Wanted(shapes)) : -(r.H / (double)Math.Max(1, r.Wanted(shapes))))
                    .First();
                row.H += grow ? 1 : -1;
            }
        }
    }

    /// <summary>
    /// [S-16550] What a filled way costs: the room left under the shorter columns' feet and beside the last column, the room
    /// no piece took (a piece no column took whole: far the most), how far the pieces are from their own sizes, and a little for
    /// pieces side by side (a column of one piece a row is the bento; side by side is the exception), each by the cells it weighs.
    /// </summary>
    private static double Cost(Filled filled, List<Shape> shapes, int width)
    {
        var all = filled.Columns.Where(c => c.Rows.Count > 0).ToList();
        var used = all.Where(c => !c.Closed).ToList();
        var foot = used.Count == 0 ? 0 : used.Max(c => c.Foot);
        var top = all.Where(c => !all.Exists(o => o.Closed && o.W > c.W && o.X <= c.X && o.X + o.W >= c.X + c.W)).ToList();
        var span = top.Sum(c => c.W) + (Math.Max(0, top.Count - 1) * Gap);
        var area = shapes.Sum(s => (double)s.W * s.H);
        var ragged = used.Sum(c => (double)(foot - c.Foot) * c.W) + (Unfilled * area * (width - span) / Math.Max(1, width));
        var strain = 0.0;
        var beside = 0.0;
        foreach (var row in all.SelectMany(c => c.Rows))
        {
            foreach (var (piece, w) in row.Members)
            {
                var shape = shapes[piece];
                strain += (Math.Abs(Math.Log(w / (double)shape.W)) + (0.5 * Math.Abs(Math.Log(row.H / (double)Math.Max(1, shape.HeightAt(w)))))) * shape.W * shape.H;
                beside += row.Members.Count > 1 ? w * row.H : 0;
            }
        }

        return (Uneven * ragged) + (1000.0 * filled.Forced) + (2.0 * strain) + (0.5 * beside);
    }

    private static GallerySheet Sheet(GalleryCategory category, Filled filled, int rows)
    {
        var tiles = Tiles(category.Pieces, filled);
        var used = filled.Columns.Where(c => c.Rows.Count > 0).ToList();
        var foot = Math.Max(rows, used.Count == 0 ? 0 : used.Max(c => c.Foot));
        return new GallerySheet(category.Id, category.Title, foot, tiles) { Columns = [.. used.Select(c => c.Box)] };
    }

    /// <summary>The pieces' tiles from their rows, in the pieces' order.</summary>
    private static List<GalleryTile> Tiles(IReadOnlyList<GalleryPiece> pieces, Filled filled)
    {
        var boxes = new CellBox[pieces.Count];
        foreach (var col in filled.Columns)
        {
            var y = col.Top;
            foreach (var row in col.Rows)
            {
                var x = col.X;
                foreach (var (piece, w) in row.Members)
                {
                    boxes[piece] = new CellBox(x, y, w, row.H);
                    x += w + Gap;
                }

                y += row.H + Gap;
            }
        }

        return [.. pieces.Select((p, i) => new GalleryTile(p, boxes[i]))];
    }

    /// <summary>
    /// [S-16552] The holes of <paramref name="sheet"/>: the cells of its columns, from each one's top to its foot, that no
    /// piece and no gap between two of its pieces takes. Zero is a column bento: its cards one cell apart from top to foot.
    /// </summary>
    public static int Holes(GallerySheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var holes = 0;
        foreach (var col in sheet.Columns)
        {
            var taken = new bool[col.W, col.H];
            var boxes = sheet.Tiles.Select(t => t.Box).ToList();
            foreach (var box in boxes)
            {
                // A piece, the gap under it and the gap to its right where a piece stands beside it, inside the column.
                var beside = boxes.Exists(b => b.X == box.Right + Gap && b.Y < box.Bottom && box.Y < b.Bottom);
                for (var x = Math.Max(col.X, box.X); x < Math.Min(col.Right, box.Right + (beside ? Gap : 0)); x++)
                {
                    for (var y = Math.Max(col.Y, box.Y); y < Math.Min(col.Bottom, box.Bottom + Gap); y++)
                    {
                        taken[x - col.X, y - col.Y] = true;
                    }
                }
            }

            foreach (var cell in taken)
            {
                holes += cell ? 0 : 1;
            }
        }

        return holes;
    }

    /// <summary>
    /// [S-16552] How uneven the columns' feet of <paramref name="sheet"/> are: the lowest foot less the highest, in rows of
    /// cells (a column that goes on as narrower ones under it has its foot in theirs).
    /// </summary>
    public static int Ragged(GallerySheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var feet = sheet.Columns.Where(c => !sheet.Columns.Any(d => d.Y == c.Bottom + Gap && d.X >= c.X && d.Right <= c.Right)).Select(c => c.Bottom).ToList();
        return feet.Count == 0 ? 0 : feet.Max() - feet.Min();
    }

    /// <summary>A column being filled: its cells across, its top and its rows.</summary>
    private sealed class Col(int x, int w, int top)
    {
        public int X { get; } = x;

        public int W { get; } = w;

        public int Top { get; } = top;

        /// <summary>No more rows: a column laid under it would be reached into.</summary>
        public bool Closed { get; set; }

        public List<Row> Rows { get; } = [];

        public int Foot => Top + Rows.Sum(r => r.H) + (Math.Max(0, Rows.Count - 1) * Gap);

        /// <summary>Where the next row would start.</summary>
        public int Next => Rows.Count == 0 ? Top : Foot + Gap;

        public CellBox Box => new(X, Top, W, Foot - Top);
    }

    /// <summary>A row of a column: its pieces side by side (each its width), its height and the least and most it may be.</summary>
    private sealed class Row(List<(int Piece, int W)> members, int h, int lo, int hi, double strain)
    {
        public List<(int Piece, int W)> Members { get; } = members;

        public int H { get; set; } = h;

        public int Lo { get; } = lo;

        public int Hi { get; } = hi;

        /// <summary>How far its pieces are from their own sizes, on average (logs of the ratios).</summary>
        public double Strain { get; } = strain;

        /// <summary>The height its pieces' sizing makes them at their widths, on average.</summary>
        public int Wanted(List<Shape> shapes) => (int)Math.Round(Members.Average(m => shapes[m.Piece].HeightAt(m.W)));
    }

    private sealed record Filled(List<Col> Columns, int Forced, double Cost);

    /// <summary>The pieces' shapes and the rows already solved for them, by their pieces and width: one lay asks the same often.</summary>
    private sealed class Solver(List<Shape> shapes)
    {
        private readonly Dictionary<string, Row?> _known = new(StringComparer.Ordinal);

        public List<Shape> Shapes { get; } = shapes;

        /// <summary>The row <see cref="WidgetGallery.Solve"/> lays, a fresh one each time (its height is evened apart).</summary>
        public Row? Solve(List<int> members, int width)
        {
            var key = string.Join(',', members) + "|" + width.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!_known.TryGetValue(key, out var row))
            {
                row = WidgetGallery.Solve(members, width, Shapes);
                _known[key] = row;
            }

            return row is null ? null : new Row([.. row.Members], row.H, row.Lo, row.Hi, row.Strain);
        }
    }

    private readonly record struct ShapeKey(int W, int H, int MinW, int MaxW, bool Taller, int MinH, int MaxH, int Widths, int Natural);

    /// <summary>
    /// [S-16551] A piece's sizing held to a sheet <paramref name="width"/> cells wide: the widths it may be (any from its
    /// least to its most, a group's own widths, or its one width) and the height its sizing makes it at a width: its natural
    /// height scaled with its width, within its least and most; at one width it may stand from three quarters of that to half
    /// as tall again, within its least and most (a piece that keeps its height, its height alone).
    /// </summary>
    private readonly struct Shape
    {
        private readonly PieceSize _size;
        private readonly int[]? _widths;
        private readonly int _minW;
        private readonly int _maxW;

        private readonly int _title;

        public Shape(PieceSize size, int width, int title = 0)
        {
            _size = size;
            _title = title;
            W = Math.Clamp(size.W, 1, width);
            H = Math.Max(1, size.H) + title;
            if (size.Widths is { Count: > 0 } widths)
            {
                var fit = widths.Where(w => w >= 1 && w <= width).Order().ToArray();
                _widths = fit.Length > 0 ? fit : [Math.Min(width, widths.Min())];
                (_minW, _maxW) = (_widths[0], _widths[^1]);
            }
            else if (size.Wider)
            {
                _minW = Math.Clamp(size.MinW, 1, width);
                _maxW = Math.Clamp(size.MaxW, _minW, width);
            }
            else
            {
                (_minW, _maxW) = (W, W);
            }
        }

        /// <summary>Its natural width (held to the sheet) and height.</summary>
        public int W { get; }

        public int H { get; }

        /// <summary>The least and the most it may be across.</summary>
        public int LeastW => _minW;

        public int MostW => _maxW;

        /// <summary>What it asks, whole: two pieces of one key lay alike.</summary>
        public ShapeKey Key => new(W, H, _minW, _maxW, _size.Taller, _size.MinH, _size.MaxH, _widths?.Length ?? 0, _size.W);

        /// <summary>Whether it keeps its size: one width and one height.</summary>
        public bool Kept => _minW == _maxW && !_size.Taller;

        /// <summary>The height its sizing makes it at <paramref name="w"/> cells across.</summary>
        public int HeightAt(int w)
            => _size.Taller ? Card(w) + _title : H;

        /// <summary>[S-16551] The card's own height at <paramref name="w"/> cells across (its title not counted).</summary>
        private int Card(int w)
            => Math.Clamp((int)Math.Round((H - _title) * w / (double)Math.Max(1, _size.W)), Math.Max(1, _size.MinH), Math.Max(_size.MinH, _size.MaxH));

        /// <summary>The heights it may stand at <paramref name="w"/> cells across, its title's rows counted.</summary>
        public (int Lo, int Hi) Heights(int w)
        {
            if (!_size.Taller)
            {
                return (H, H);
            }

            var at = Card(w);
            var most = Math.Max(_size.MinH, _size.MaxH);
            return (Math.Clamp((int)Math.Ceiling(at * Shortest), Math.Max(1, _size.MinH), at) + _title, Math.Clamp((int)Math.Floor(at * Tallest), at, most) + _title);
        }

        /// <summary>The width it may be nearest <paramref name="want"/> (the narrower of two as near).</summary>
        public int Nearest(int want)
        {
            if (_widths is null)
            {
                return Math.Clamp(want, _minW, _maxW);
            }

            var best = _widths[0];
            foreach (var w in _widths)
            {
                if (Math.Abs(w - want) < Math.Abs(best - want))
                {
                    best = w;
                }
            }

            return best;
        }

        /// <summary>Whether it may be exactly <paramref name="w"/> across.</summary>
        public bool Takes(int w) => _widths is null ? w >= _minW && w <= _maxW : Array.IndexOf(_widths, w) >= 0;

        /// <summary>The widest it may be within <paramref name="room"/> (its least where even that is wider).</summary>
        public int Below(int room)
        {
            if (_widths is null)
            {
                return Math.Clamp(room, _minW, _maxW);
            }

            var fits = _widths.Where(w => w <= room).ToList();
            return fits.Count > 0 ? fits[^1] : _widths[0];
        }

        /// <summary>The next width it may be over <paramref name="w"/>, or null.</summary>
        public int? Above(int w)
            => _widths is null ? (w < _maxW ? w + 1 : null) : _widths.Where(x => x > w).Select(x => (int?)x).FirstOrDefault();

        /// <summary>The next width it may be under <paramref name="w"/>, or null.</summary>
        public int? Under(int w)
            => _widths is null ? (w > _minW ? w - 1 : null) : _widths.Where(x => x < w).Select(x => (int?)x).LastOrDefault();
    }

    /// <summary>
    /// [S-16407] The tile an arrow key moves the pick to from <paramref name="from"/> (null: none picked, the first tile):
    /// the nearest whose middle lies that way, the way it goes counted before the way across; the same tile where none does.
    /// </summary>
    public static int Next(GallerySheet sheet, int? from, GalleryMove move)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (sheet.Tiles.Count == 0)
        {
            return -1;
        }

        if (from is not { } at || at < 0 || at >= sheet.Tiles.Count)
        {
            return 0;
        }

        var (ox, oy) = sheet.Tiles[at].Box.Middle;
        var best = at;
        var score = double.MaxValue;
        for (var i = 0; i < sheet.Tiles.Count; i++)
        {
            var (x, y) = sheet.Tiles[i].Box.Middle;
            var (along, across) = move switch
            {
                GalleryMove.Right => (x - ox, Math.Abs(y - oy)),
                GalleryMove.Left => (ox - x, Math.Abs(y - oy)),
                GalleryMove.Down => (y - oy, Math.Abs(x - ox)),
                _ => (oy - y, Math.Abs(x - ox)),
            };
            if (i == at || along <= 0)
            {
                continue;
            }

            var s = along + (2 * across);
            if (s < score)
            {
                (best, score) = (i, s);
            }
        }

        return best;
    }

    /// <summary>
    /// [S-16566] The head's line shared, tabs first (the hint never at the tabs' cost): on a line <paramref name="line"/> DIPs
    /// wide the tabs (<paramref name="tabs"/> wide, all of them) take what they need; then the search box, <paramref name="search"/>
    /// wide, and the hint (its whole sentence <paramref name="hintLong"/> or its short one <paramref name="hintShort"/> wide), each
    /// <paramref name="gap"/> after what stands before it, share what is left: the whole hint where the search keeps its width,
    /// else the short one; else the short one with the search narrowed, never under <paramref name="searchLeast"/>; else no
    /// hint and the search at its width (the tabs scroll when even with the search alone they do not fit).
    /// </summary>
    public static (GalleryHint Hint, double Search) Head(double line, double tabs, double search, double searchLeast, double hintLong, double hintShort, double gap)
    {
        double Room(double hint) => line - tabs - gap - hint - gap;
        if (Room(hintLong) >= search)
        {
            return (GalleryHint.Long, search);
        }

        if (Room(hintShort) >= search)
        {
            return (GalleryHint.Short, search);
        }

        return Room(hintShort) >= searchLeast ? (GalleryHint.Short, Math.Floor(Room(hintShort))) : (GalleryHint.None, search);
    }

    /// <summary>[S-16408] After a placement off the gallery: landed, the settings of what landed; given up, the gallery back.</summary>
    public static GalleryAfter AfterPlacing(bool landed) => landed ? GalleryAfter.Settings : GalleryAfter.Back;

    /// <summary>[S-14913] How far a sheet <paramref name="sheetRows"/> tall scrolls in a view <paramref name="viewRows"/> tall, in rows of cells.</summary>
    public static int MaxScroll(int sheetRows, int viewRows) => Math.Max(0, sheetRows - Math.Max(0, viewRows));

    /// <summary>[S-14913] A scroll of <paramref name="by"/> rows from <paramref name="at"/>, held to the sheet's ends.</summary>
    public static int Scroll(int at, int by, int max) => Math.Clamp(at + by, 0, Math.Max(0, max));

    /// <summary>[S-14913] A page of a view <paramref name="viewRows"/> tall: the view less one step, in whole steps, at least one.</summary>
    public static int PageRows(int viewRows) => Math.Max(Step, ((viewRows / Step) - 1) * Step);

    /// <summary>[S-16407] The scroll that brings <paramref name="box"/> whole into a view <paramref name="viewRows"/> tall scrolled to <paramref name="at"/>, as little as it takes.</summary>
    public static int Reveal(int at, CellBox box, int viewRows, int max)
    {
        var to = box.Bottom > at + viewRows ? box.Bottom - viewRows : at;
        to = box.Y < to ? box.Y : to;
        return Math.Clamp(to, 0, Math.Max(0, max));
    }

    /// <summary>
    /// [S-14911] [S-16400] The gallery's window on a display: <see cref="G.Share"/> of the work area (<paramref name="work"/>,
    /// DIPs) each way, never wider than the work area less its margins nor taller than the room under the bar
    /// (<paramref name="barBottom"/>), flush under the bar as the add page hangs (the flyouts' gap), in the middle across.
    /// Inside its padding: the head (the tabs, the hint and the search box, [S-16559]: <see cref="G.HeadInset"/> under the
    /// window's top and <see cref="G.HeadGap"/> over the well) over the well. The cards' view is the well's room on whole
    /// cells of the display's grid (<paramref name="pitchX"/> by <paramref name="pitchY"/> DIPs a cell), inside the part new
    /// geometry may use (<paramref name="allowed"/>).
    /// </summary>
    public static GalleryFrame Frame(DipRect work, double barBottom, DipRect allowed, double pitchX, double pitchY)
    {
        var top = Math.Max(work.Y, barBottom) + ShellMetrics.Flyout.Gap;
        var room = new DipRect(work.X + G.Margin, top, Math.Max(0, work.Width - (2 * G.Margin)), Math.Max(0, work.Y + work.Height - G.Margin - top));
        var width = Math.Min(room.Width, Math.Max(G.MinWidth, work.Width * G.Share));
        var height = Math.Min(room.Height, Math.Max(G.MinHeight, work.Height * G.Share));
        var window = new DipRect(room.X + ((room.Width - width) / 2), room.Y, width, height);

        var inner = new DipRect(window.X + G.Pad, window.Y + G.HeadInset, Math.Max(0, window.Width - (2 * G.Pad)), Math.Max(0, window.Height - G.HeadInset - G.Pad));
        var head = new DipRect(inner.X, inner.Y, inner.Width, Math.Min(G.Head, inner.Height));
        var well = new DipRect(inner.X, head.Y + head.Height + G.HeadGap, inner.Width, Math.Max(0, inner.Height - head.Height - G.HeadGap));

        // The view: whole cells inside the well's own padding and the part new geometry may use.
        var x0 = Math.Max(well.X + G.WellPad, allowed.X);
        var y0 = Math.Max(well.Y + G.WellPad, allowed.Y);
        var x1 = Math.Min(well.X + well.Width - G.WellPad - G.ScrollBar, allowed.X + allowed.Width);
        var y1 = Math.Min(well.Y + well.Height - G.WellPad, allowed.Y + allowed.Height);
        var cx = pitchX > 0 ? (int)Math.Ceiling((x0 / pitchX) - 1e-9) : 0;
        var cy = pitchY > 0 ? (int)Math.Ceiling((y0 / pitchY) - 1e-9) : 0;
        var cols = pitchX > 0 ? Math.Max(1, (int)Math.Floor((x1 / pitchX) + 1e-9) - cx) : 1;
        var rows = pitchY > 0 ? Math.Max(1, (int)Math.Floor((y1 / pitchY) + 1e-9) - cy) : 1;
        return new GalleryFrame(window, head, well, new CellBox(cx, cy, cols, rows));
    }
}
