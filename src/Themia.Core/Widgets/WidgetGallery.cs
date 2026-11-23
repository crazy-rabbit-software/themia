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

/// <summary>[S-16404] What a card of the gallery is: a piece of a category, or one of what stands round a piece picked.</summary>
public enum GalleryRole
{
    /// <summary>A whole widget or ready-made group of a category (or of a search).</summary>
    Piece,

    /// <summary>[S-16404] The piece picked, in the middle of its focus.</summary>
    Picked,

    /// <summary>[S-16404] The picked piece's words: its name, what it is, what it needs. A card of its own, no widget.</summary>
    About,

    /// <summary>[S-16404] The picked widget in another of its styles.</summary>
    Style,

    /// <summary>[S-16404] The picked widget at another size (its least, or large).</summary>
    Size,

    /// <summary>[S-16404] Another piece of the picked one's category.</summary>
    Similar,
}

/// <summary>[S-16404] Which other size a <see cref="GalleryRole.Size"/> card shows.</summary>
public enum GallerySize
{
    Least,
    Large,
}

/// <summary>
/// [S-14912] One whole thing of the gallery: a widget that stands alone (<see cref="Kind"/>) or a ready-made group
/// (<see cref="Group"/>: several widgets laid as one panel). Never a small part of a family: those are the add page's.
/// [S-16404] Round a piece picked, the same thing in another <see cref="Role"/>: its words, a style, a size, a neighbour.
/// </summary>
/// <param name="Key">The same in every language: the add page's tile key (<c>kind:clock</c>, <c>group:nowPlaying</c>).</param>
public sealed record GalleryPiece(string Key, WidgetId? Kind, ReadyMadeGroup? Group)
{
    public GalleryRole Role { get; init; }

    /// <summary>[S-16404] A <see cref="GalleryRole.Style"/> card's style.</summary>
    public int? Style { get; init; }

    /// <summary>[S-16404] A <see cref="GalleryRole.Size"/> card's size.</summary>
    public GallerySize? Size { get; init; }
}

/// <summary>[S-14912] A category of the gallery: an add page subject (or More), its whole pieces in the add page's order.</summary>
public sealed record GalleryCategory(AddFolderId Id, LocalText Title, string Glyph, IReadOnlyList<GalleryPiece> Pieces);

/// <summary>
/// [S-14912] [S-16403] What a piece asks of the bento, in whole cells of the display's grid: its natural size, the least
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

/// <summary>[S-14912] A category laid as a bento: its tiles and how many rows of cells it is tall.</summary>
public sealed record GallerySheet(AddFolderId Category, LocalText Name, int Rows, IReadOnlyList<GalleryTile> Tiles);

/// <summary>
/// [S-14911] [S-16400] [S-16401] The gallery's window on a display, in DIPs: the window, its head (the categories' tabs and
/// the search box) and the well the cards stand in; and <see cref="View"/>, the well's room in whole cells of the display's grid.
/// </summary>
public sealed record GalleryFrame(DipRect Window, DipRect Head, DipRect Well, CellBox View);

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
/// [S-14910]..[S-14917] [S-16400]..[S-16410] The widget gallery's arrangement, pure (owner, 2026-11-21: "for now lets just
/// show complete widgets / groups, not the small atoms ... they should be shown as a bento"; 2026-11-23: "make sure that in
/// the widget gallery that all the widgets are properly placed on a bento grid and there are no major spaces between them.
/// (except for the one block space between separate groups of widgets)"). A category is its whole pieces (<see cref="Of"/>),
/// a search the pieces that match (<see cref="Find"/>), laid as one bento (<see cref="Lay"/>): every piece on whole cells, one
/// cell (<see cref="Gap"/>) between two, packed at the first room it fits and grown into the room left beside it, so there is
/// no hole bigger than what nothing can fill (<see cref="Holes"/>). A piece picked is shown in focus (<see cref="Focus"/>,
/// <see cref="LayFocus"/>): itself in the middle, its words, its styles or sizes and its neighbours round it. Deterministic;
/// no two pieces share or touch a cell.
/// </summary>
public static class WidgetGallery
{
    /// <summary>[S-16403] Between two pieces, in cells: never touching, so no two pieces join ([S-2240]).</summary>
    public const int Gap = 1;

    /// <summary>[S-14913] How far one notch of the wheel scrolls a sheet, in rows of cells.</summary>
    public const int Step = 8;

    /// <summary>[S-16404] How many neighbours stand round a piece picked, at the least and at the most.</summary>
    public const int FewestSimilar = 2;

    public const int MostAround = 8;

    /// <summary>
    /// [S-14912] The categories of <paramref name="page"/>: every subject on its rail (and More), each its ready-made
    /// groups and then its own widgets, as the add page has them ([S-13611]). A family's small parts (a fact, a button,
    /// a line of text) are left out: they are shown whole, in their groups. Start and desktop items stay the add page's.
    /// A subject with no whole piece is left out.
    /// </summary>
    public static IReadOnlyList<GalleryCategory> Of(AddPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var categories = new List<GalleryCategory>();
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
                    case AddSectionKind.ReadyMade:
                        pieces.AddRange(section.Tiles.OfType<AddGroupTile>().Select(t => new GalleryPiece(t.Key, null, t.Group)));
                        break;
                    case AddSectionKind.Widgets:
                        pieces.AddRange(section.Tiles.OfType<AddWidgetTile>().Select(t => new GalleryPiece(t.Key, t.Kind, null)));
                        break;
                }
            }

            if (pieces.Count > 0)
            {
                categories.Add(new GalleryCategory(item.Id, item.Title, item.Glyph, pieces));
            }
        }

        return categories;
    }

    /// <summary>
    /// [S-16402] What a search shows in place of a category: the add page's search over every widget and group
    /// (<see cref="AddPage.Find"/>, best first, the groups before the widgets), kept to the gallery's whole pieces (a family's
    /// small part it finds is left out), each once; null while <paramref name="query"/> searches nothing. Laid as a category is.
    /// </summary>
    public static GalleryCategory? Find(AddPage page, IReadOnlyList<GalleryCategory> categories, string? query)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(categories);
        if (!AddPage.Searching(query))
        {
            return null;
        }

        var whole = new Dictionary<string, GalleryPiece>(StringComparer.Ordinal);
        foreach (var piece in categories.SelectMany(c => c.Pieces))
        {
            whole.TryAdd(piece.Key, piece);
        }

        var found = new List<GalleryPiece>();
        foreach (var tile in page.Find(query).SelectMany(s => s.Tiles))
        {
            if (whole.TryGetValue(tile.Key, out var piece) && !found.Contains(piece))
            {
                found.Add(piece);
            }
        }

        return new GalleryCategory(AddFolderId.Browse, Str.WidgetGallery_Results.Local(), Themia.App.Shell.Glyphs.Search, found);
    }

    /// <summary>
    /// [S-14912] [S-16403] <paramref name="category"/> as a bento <paramref name="width"/> cells wide: each piece at its
    /// natural size (held to the width), placed in order at the first room it fits with one cell round it, top-most then
    /// left-most, so a smaller piece fills the room a larger one left; then every piece is grown a cell at a time, by turns,
    /// on the axes it may be another size on, to the right, down (never past the sheet's foot), to the left and up, into
    /// room nothing took, so the block is one bento with one cell between two pieces and no hole a piece could fill.
    /// </summary>
    public static GallerySheet Lay(GalleryCategory category, int width, Func<GalleryPiece, PieceSize> size)
    {
        ArgumentNullException.ThrowIfNull(category);
        return Best(category, width, size, anchored: false);
    }

    /// <summary>
    /// [S-16404] What stands round <paramref name="picked"/>, a piece of <paramref name="category"/>, in focus: the piece
    /// itself (<see cref="GalleryRole.Picked"/>), its words (<see cref="GalleryRole.About"/>), the picked widget in each of
    /// its <paramref name="otherStyles"/>, where it has fewer than two the widget at its least and large (when it
    /// <paramref name="resizes"/>), then the category's other pieces after it in its order (and before it, wrapping) as
    /// neighbours, at least <see cref="FewestSimilar"/>, all of it at most <see cref="MostAround"/> round it.
    /// </summary>
    public static GalleryCategory Focus(GalleryCategory category, GalleryPiece picked, IReadOnlyList<int> otherStyles, bool resizes)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(picked);
        ArgumentNullException.ThrowIfNull(otherStyles);
        var key = picked.Key;
        List<GalleryPiece> around = [];
        if (picked.Kind is { } kind && picked.Group is null)
        {
            around.AddRange(otherStyles.Take(MostAround - FewestSimilar).Select(style => new GalleryPiece($"style:{kind.Value}:{style}", kind, null) { Role = GalleryRole.Style, Style = style }));
            if (otherStyles.Count < 2 && resizes)
            {
                around.Add(new GalleryPiece($"size:{kind.Value}:least", kind, null) { Role = GalleryRole.Size, Size = GallerySize.Least });
                around.Add(new GalleryPiece($"size:{kind.Value}:large", kind, null) { Role = GalleryRole.Size, Size = GallerySize.Large });
            }
        }

        var at = category.Pieces.ToList().FindIndex(p => string.Equals(p.Key, key, StringComparison.Ordinal));
        var others = category.Pieces.Count == 0 ? [] : Enumerable.Range(1, category.Pieces.Count - 1)
            .Select(i => category.Pieces[(Math.Max(0, at) + i) % category.Pieces.Count])
            .Where(p => !string.Equals(p.Key, key, StringComparison.Ordinal))
            .ToList();
        var similar = Math.Max(FewestSimilar, MostAround - around.Count);
        around.AddRange(others.Take(similar).Select(p => p with { Key = "similar:" + p.Key, Role = GalleryRole.Similar }));

        List<GalleryPiece> pieces =
        [
            picked with { Role = GalleryRole.Picked, Style = null, Size = null },
            new GalleryPiece("about:" + key, picked.Kind, picked.Group) { Role = GalleryRole.About },
            .. around,
        ];
        return new GalleryCategory(category.Id, category.Title, category.Glyph, pieces);
    }

    /// <summary>
    /// [S-16405] A focus (<see cref="Focus"/>) as a bento <paramref name="width"/> cells wide: the picked piece first, at the
    /// sheet's top in the middle across, the rest packed round it as a category is (<see cref="Lay"/>: its left and right,
    /// then under it), one cell between two and grown into the room left.
    /// </summary>
    public static GallerySheet LayFocus(GalleryCategory focus, int width, Func<GalleryPiece, PieceSize> size)
    {
        ArgumentNullException.ThrowIfNull(focus);
        return Best(focus, width, size, anchored: true);
    }

    /// <summary>
    /// [S-16403] The ways the pieces go in: which first (the largest, the tallest, the widest, or in their order) and where
    /// (where it keeps the sheet shortest and touches most, or the top-most, left-most room it fits).
    /// </summary>
    private static readonly (PackOrder Order, bool Touching)[] Ways =
    [
        (PackOrder.Largest, true), (PackOrder.Tallest, true), (PackOrder.Widest, true), (PackOrder.Given, true),
        (PackOrder.Largest, false), (PackOrder.Tallest, false), (PackOrder.Widest, false), (PackOrder.Given, false),
    ];

    private enum PackOrder
    {
        Largest,
        Tallest,
        Widest,
        Given,
    }

    /// <summary>
    /// [S-16403] The pieces packed every way (<see cref="Ways"/>), and the sheet that leaves the fewest cells empty between
    /// pieces kept (then the shortest, then the first way): what is left is what no way could fill.
    /// </summary>
    private static GallerySheet Best(GalleryCategory category, int width, Func<GalleryPiece, PieceSize> size, bool anchored)
    {
        ArgumentNullException.ThrowIfNull(size);
        var asks = category.Pieces.Select(size).ToList();
        PieceSize Asked(GalleryPiece piece) => asks[IndexOf(category.Pieces, piece)];
        GallerySheet? best = null;
        var (holes, rows) = (int.MaxValue, int.MaxValue);
        foreach (var (order, touching) in Ways)
        {
            var sheet = Pack(category, width, Asked, anchored, order, touching);
            var h = Holes(sheet, width);
            if (h < holes || (h == holes && sheet.Rows < rows))
            {
                (best, holes, rows) = (sheet, h, sheet.Rows);
            }
        }

        return best!;

        static int IndexOf(IReadOnlyList<GalleryPiece> pieces, GalleryPiece piece)
        {
            for (var i = 0; i < pieces.Count; i++)
            {
                if (ReferenceEquals(pieces[i], piece))
                {
                    return i;
                }
            }

            throw new ArgumentException("not a piece of the category", nameof(piece));
        }
    }

    private static GallerySheet Pack(GalleryCategory category, int width, Func<GalleryPiece, PieceSize> size, bool anchored, PackOrder order, bool touching)
    {
        ArgumentNullException.ThrowIfNull(size);
        var cols = Math.Max(1, width);
        var span = cols + Gap;
        var taken = new List<bool[]>();
        var placed = new List<(GalleryPiece Piece, PieceSize Size, int X, int Y, int W, int H)>();

        bool Free(int x, int y, int w, int h)
        {
            if (x < 0 || y < 0 || x + w > span)
            {
                return false;
            }

            for (var row = y; row < y + h && row < taken.Count; row++)
            {
                for (var col = x; col < x + w; col++)
                {
                    if (taken[row][col])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        void Take(int x, int y, int w, int h)
        {
            while (taken.Count < y + h)
            {
                taken.Add(new bool[span]);
            }

            for (var row = y; row < y + h; row++)
            {
                for (var col = x; col < x + w; col++)
                {
                    taken[row][col] = true;
                }
            }
        }

        // Each piece's footprint: its box and one cell to its right and under it, the gap no other piece takes. The pieces go
        // largest first (a focus: the piece in focus in the middle of the top, then the ones that keep their size, so the rest
        // can grow round them) each where it keeps the sheet
        // shortest and touches the most of what stands (other footprints, the sheet's sides and top), so the small ones fill
        // the room the large ones leave; then the top-most, the left-most.
        var asks = category.Pieces.Select(size).ToList();
        var boxes = new (int X, int Y, int W, int H)[asks.Count];
        long Area(int i) => (long)Math.Max(1, asks[i].W) * Math.Max(1, asks[i].H);
        long Key(int i) => order switch
        {
            PackOrder.Largest => Area(i),
            PackOrder.Tallest => asks[i].H,
            PackOrder.Widest => asks[i].W,
            _ => 0,
        };
        IEnumerable<int> sequence = anchored
            ? Enumerable.Range(0, Math.Min(1, asks.Count)).Concat(Enumerable.Range(1, Math.Max(0, asks.Count - 1))
                .OrderBy(i => asks[i].Wider || asks[i].Taller ? 1 : 0).ThenByDescending(Key).ThenBy(i => i))
            : Enumerable.Range(0, asks.Count).OrderByDescending(Key).ThenBy(i => i);
        var corners = new List<(int X, int Y)> { (0, 0) };
        var first = true;
        foreach (var i in sequence)
        {
            var asked = asks[i];
            var w = Width(asked, Math.Min(cols, Math.Max(1, asked.W)), cols);
            var h = Math.Max(1, asked.H);
            (int X, int Y) at = (0, taken.Count);
            if (anchored && first)
            {
                at = ((cols - w) / 2, 0);
            }
            else
            {
                var best = (Bottom: int.MaxValue, Contact: -1, Y: int.MaxValue, X: int.MaxValue);
                if (!touching)
                {
                    // The top-most, left-most room it fits.
                    for (var y = 0; y <= taken.Count && best.Bottom == int.MaxValue; y++)
                    {
                        for (var x = 0; x + w <= cols; x++)
                        {
                            if (Free(x, y, w + Gap, h + Gap))
                            {
                                best = (0, 0, y, x);
                                break;
                            }
                        }
                    }
                }

                foreach (var y in touching ? corners.Select(c => c.Y).Append(taken.Count).Distinct() : [])
                {
                    foreach (var x in corners.Select(c => c.X).Distinct())
                    {
                        if (x + w > cols || !Free(x, y, w + Gap, h + Gap))
                        {
                            continue;
                        }

                        var score = (Bottom: Math.Max(taken.Count, y + h + Gap), Contact: Contact(x, y, w + Gap, h + Gap), Y: y, X: x);
                        if (score.Bottom < best.Bottom || (score.Bottom == best.Bottom && (score.Contact > best.Contact
                            || (score.Contact == best.Contact && (score.Y < best.Y || (score.Y == best.Y && score.X < best.X))))))
                        {
                            best = score;
                        }
                    }
                }

                at = best.Bottom == int.MaxValue ? (0, taken.Count) : (best.X, best.Y);
            }

            first = false;
            Take(at.X, at.Y, w + Gap, h + Gap);
            corners.Add((at.X + w + Gap, at.Y));
            corners.Add((at.X, at.Y + h + Gap));
            boxes[i] = (at.X, at.Y, w, h);
        }

        for (var i = 0; i < asks.Count; i++)
        {
            placed.Add((category.Pieces[i], asks[i], boxes[i].X, boxes[i].Y, boxes[i].W, boxes[i].H));
        }

        // How much of a footprint's rim touches what stands: other footprints, the sheet's sides and its top.
        int Contact(int x, int y, int fw, int fh)
        {
            bool At(int col, int row) => col < 0 || col >= span || row < 0 || (row < taken.Count && taken[row][col]);
            var touch = 0;
            for (var row = y; row < y + fh; row++)
            {
                touch += (At(x - 1, row) ? 1 : 0) + (At(x + fw, row) ? 1 : 0);
            }

            for (var col = x; col < x + fw; col++)
            {
                touch += (At(col, y - 1) ? 1 : 0) + (At(col, y + fh) ? 1 : 0);
            }

            return touch;
        }

        // Grown into the room left, a cell at a time by turns: right, down (never past the foot), left, up; then a piece at
        // the sheet's side that cannot grow slides to the piece beyond the room it left, so what is left is at the block's
        // edge, never between two pieces; and grown again.
        var rows = placed.Count == 0 ? 0 : placed.Max(p => p.Y + p.H);
        for (var round = 0; round < 8; round++)
        {
            Grow();
            if (!Slide())
            {
                break;
            }
        }

        void Untake(int x, int y, int w, int h)
        {
            for (var row = y; row < Math.Min(taken.Count, y + h); row++)
            {
                for (var col = x; col < x + w; col++)
                {
                    taken[row][col] = false;
                }
            }
        }

        bool Slide()
        {
            var slid = false;
            for (var i = anchored ? 1 : 0; i < placed.Count; i++)
            {
                var p = placed[i];
                var k = 0;
                while (p.X + p.W + k + 1 <= cols && Free(p.X + p.W + Gap + k, p.Y, 1, p.H + Gap))
                {
                    k++;
                }

                // Only from the sheet's side toward a piece: the room it leaves is the block's edge then, never between two.
                if (p.X == 0 && k > 0 && p.X + p.W + k < cols)
                {
                    Untake(p.X, p.Y, p.W + Gap, p.H + Gap);
                    Take(p.X + k, p.Y, p.W + Gap, p.H + Gap);
                    placed[i] = p with { X = p.X + k };
                    slid = true;
                }
            }

            return slid;
        }

        void Grow()
        {
            var grew = true;
            while (grew)
            {
                grew = false;
                for (var i = 0; i < placed.Count; i++)
                {
                    var p = placed[i];
                    if (p.Size.Wider && Width(p.Size, p.W + 1, cols, up: true) is var wider && wider > p.W)
                    {
                        var more = wider - p.W;
                        if (p.X + wider <= cols && Free(p.X + p.W + Gap, p.Y, more, p.H + Gap))
                        {
                            Take(p.X + p.W + Gap, p.Y, more, p.H + Gap);
                            placed[i] = p = p with { W = wider };
                            grew = true;
                        }
                        else if (p.X - more >= 0 && Free(p.X - more, p.Y, more, p.H + Gap))
                        {
                            Take(p.X - more, p.Y, more, p.H + Gap);
                            placed[i] = p = p with { X = p.X - more, W = wider };
                            grew = true;
                        }
                    }

                    if (p.Size.Taller && p.H < Math.Min(p.Size.MaxH, int.MaxValue))
                    {
                        if (p.Y + p.H + 1 <= rows && Free(p.X, p.Y + p.H + Gap, p.W + Gap, 1))
                        {
                            Take(p.X, p.Y + p.H + Gap, p.W + Gap, 1);
                            placed[i] = p with { H = p.H + 1 };
                            grew = true;
                        }
                        else if (p.Y - 1 >= 0 && Free(p.X, p.Y - 1, p.W + Gap, 1))
                        {
                            Take(p.X, p.Y - 1, p.W + Gap, 1);
                            placed[i] = p with { Y = p.Y - 1, H = p.H + 1 };
                            grew = true;
                        }
                    }
                }
            }
        }

        var tiles = placed.Select(p => new GalleryTile(p.Piece, new CellBox(p.X, p.Y, p.W, p.H))).ToList();
        return new GallerySheet(category.Id, category.Title, rows, tiles);
    }

    /// <summary>
    /// The width a piece is made at for <paramref name="want"/> cells, held to <paramref name="cols"/>: any from its least to
    /// its most, or one of its <see cref="PieceSize.Widths"/>: the largest not over it (<paramref name="up"/>: the least not under it).
    /// </summary>
    private static int Width(PieceSize size, int want, int cols, bool up = false)
    {
        var most = Math.Min(cols, size.MaxW);
        if (size.Widths is not { Count: > 0 } widths)
        {
            return Math.Max(1, Math.Min(want, Math.Max(most, 1)));
        }

        var fits = widths.Where(w => w <= cols).ToList();
        if (fits.Count == 0)
        {
            return Math.Min(cols, widths[0]);
        }

        return up
            ? fits.Where(w => w >= want).DefaultIfEmpty(fits[^1]).Min()
            : fits.Where(w => w <= want).DefaultIfEmpty(fits[0]).Max();
    }

    /// <summary>
    /// [S-16403] The first piece of <paramref name="sheet"/> (laid <paramref name="width"/> cells wide) that could still be made
    /// a cell longer, as <paramref name="size"/> lets it, to its right, left, foot or top into room no other piece or gap takes
    /// (inside the sheet), or null: every piece is grown as far as it goes, so whatever room is left no piece could take.
    /// </summary>
    public static GalleryTile? Grows(GallerySheet sheet, int width, Func<GalleryPiece, PieceSize> size)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(size);
        var (w, h) = (Math.Max(1, width) + Gap, sheet.Rows + Gap);
        var owner = new int[w, h];
        for (var i = 0; i < sheet.Tiles.Count; i++)
        {
            var box = sheet.Tiles[i].Box;
            for (var x = box.X; x < Math.Min(w, box.Right + Gap); x++)
            {
                for (var y = box.Y; y < Math.Min(h, box.Bottom + Gap); y++)
                {
                    owner[x, y] = i + 1;
                }
            }
        }

        bool Free(int x0, int y0, int ww, int hh, int me)
        {
            if (x0 < 0 || y0 < 0 || x0 + ww > w || y0 + hh > h)
            {
                return false;
            }

            for (var x = x0; x < x0 + ww; x++)
            {
                for (var y = y0; y < y0 + hh; y++)
                {
                    if (owner[x, y] != 0 && owner[x, y] != me)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        for (var i = 0; i < sheet.Tiles.Count; i++)
        {
            var (piece, box) = (sheet.Tiles[i].Piece, sheet.Tiles[i].Box);
            var asked = size(piece);
            if (asked.Wider && Width(asked, box.W + 1, Math.Max(1, width), up: true) is var wider && wider > box.W)
            {
                var more = wider - box.W;
                if ((box.X + wider <= width && Free(box.Right + Gap, box.Y, more, box.H + Gap, i + 1)) || Free(box.X - more, box.Y, more, box.H + Gap, i + 1))
                {
                    return sheet.Tiles[i];
                }
            }

            if (asked.Taller && box.H < asked.MaxH
                && ((box.Bottom + 1 <= sheet.Rows && Free(box.X, box.Bottom + Gap, box.W + Gap, 1, i + 1)) || Free(box.X, box.Y - 1, box.W + Gap, 1, i + 1)))
            {
                return sheet.Tiles[i];
            }
        }

        return null;
    }

    /// <summary>
    /// [S-16403] The holes of <paramref name="sheet"/> laid <paramref name="width"/> cells wide: the cells no piece and no
    /// piece's one-cell gap (to its right and under it) takes that lie between pieces (one to their left and one to their right
    /// in their row, or one over and one under them in their column). Room at the block's edge is its edge, no hole. Zero is a
    /// bento whose pieces stand one cell apart everywhere.
    /// </summary>
    public static int Holes(GallerySheet sheet, int width)
    {
        var mask = HoleMask(sheet, width);
        var holes = 0;
        foreach (var hole in mask)
        {
            holes += hole ? 1 : 0;
        }

        return holes;
    }

    /// <summary>
    /// [S-16403] How thick the thickest hole of <paramref name="sheet"/> is, in cells: of each hole's cells, the shorter of
    /// its run of holes across and its run down, the most of them. Under three (the least a piece is), no piece could stand
    /// in it: what is left is the step of a piece that cannot be every length, not room left unused.
    /// </summary>
    public static int Thickest(GallerySheet sheet, int width)
    {
        var mask = HoleMask(sheet, width);
        var (w, h) = (mask.GetLength(0), mask.GetLength(1));
        var most = 0;
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                if (!mask[x, y])
                {
                    continue;
                }

                var (left, right, up, down) = (x, x, y, y);
                while (left > 0 && mask[left - 1, y])
                {
                    left--;
                }

                while (right < w - 1 && mask[right + 1, y])
                {
                    right++;
                }

                while (up > 0 && mask[x, up - 1])
                {
                    up--;
                }

                while (down < h - 1 && mask[x, down + 1])
                {
                    down++;
                }

                most = Math.Max(most, Math.Min(right - left + 1, down - up + 1));
            }
        }

        return most;
    }

    private static bool[,] HoleMask(GallerySheet sheet, int width)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var (w, h) = (Math.Max(1, width) + Gap, sheet.Rows + Gap);
        var covered = new bool[w, h];
        var mask = new bool[w, h];
        if (sheet.Tiles.Count == 0)
        {
            return mask;
        }

        foreach (var box in sheet.Tiles.Select(t => t.Box))
        {
            for (var x = box.X; x < Math.Min(w, box.Right + Gap); x++)
            {
                for (var y = box.Y; y < Math.Min(h, box.Bottom + Gap); y++)
                {
                    covered[x, y] = true;
                }
            }
        }

        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                if (covered[x, y])
                {
                    continue;
                }

                var across = Enumerable.Range(0, x).Any(i => covered[i, y]) && Enumerable.Range(x + 1, w - x - 1).Any(i => covered[i, y]);
                var down = Enumerable.Range(0, y).Any(i => covered[x, i]) && Enumerable.Range(y + 1, h - y - 1).Any(i => covered[x, i]);
                mask[x, y] = across || down;
            }
        }

        return mask;
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
    /// Inside its padding: the head (the tabs and the search box) over the well. The cards' view is the well's room on whole
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

        var inner = new DipRect(window.X + G.Pad, window.Y + G.Pad, Math.Max(0, window.Width - (2 * G.Pad)), Math.Max(0, window.Height - (2 * G.Pad)));
        var head = new DipRect(inner.X, inner.Y, inner.Width, Math.Min(G.Head, inner.Height));
        var well = new DipRect(inner.X, head.Y + head.Height + G.Pad / 2, inner.Width, Math.Max(0, inner.Height - head.Height - (G.Pad / 2)));

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
