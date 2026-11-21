using Themia.Core.Localization;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>[S-14912] A run of whole grid cells: a card's box or a line's, counted from the display's top-left grid line.</summary>
public readonly record struct CellBox(int X, int Y, int W, int H)
{
    public int Right => X + W;

    public int Bottom => Y + H;

    /// <summary>Whether the two share any cell (touching edges do not).</summary>
    public bool Overlaps(CellBox other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    /// <summary>Whether <paramref name="inner"/> lies wholly inside this box.</summary>
    public bool Holds(CellBox inner) => inner.X >= X && inner.Y >= Y && inner.Right <= Right && inner.Bottom <= Bottom;
}

/// <summary>[S-14912] A block of a category: a family (its name over it) or the category's own widgets (no name: the screen's title says it).</summary>
/// <param name="Key">The same in every language: the add page's section key.</param>
public sealed record GalleryBlock(string Key, LocalText? Title, IReadOnlyList<WidgetId> Kinds);

/// <summary>[S-14912] A category of the gallery: an add page subject (or More), its blocks in the tree's order.</summary>
public sealed record GalleryCategory(AddFolderId Id, LocalText Title, string Glyph, IReadOnlyList<GalleryBlock> Blocks);

/// <summary>[S-14912] A card on a screen: its kind on its box of whole cells, in the block it belongs to.</summary>
public sealed record GalleryCard(WidgetId Kind, CellBox Box, string Block);

/// <summary>[S-14912] A family's name line over its block, on whole cells.</summary>
public sealed record GalleryHead(LocalText Text, CellBox Box);

/// <summary>
/// [S-14912] One screen of the gallery: the category's title line (<see cref="Title"/>, its <see cref="Page"/> of
/// <see cref="Pages"/>), its family heads and its cards, every one on whole cells of the display's grid.
/// </summary>
public sealed record GalleryScreen(AddFolderId Category, LocalText Name, int Page, int Pages, CellBox Title, IReadOnlyList<GalleryHead> Heads, IReadOnlyList<GalleryCard> Cards);

/// <summary>
/// [S-14910]..[S-14915] The widget gallery's arrangement, pure (owner, 2026-11-21: "the widgets should be arranged in
/// groups in this preview already. not just all scattered randomly. if one category does not fit onto one screen you
/// can just implement multiple screens."; and: fill each screen before the next). A category is a run of screens, never
/// mixed with another; on each, its title line, then its pieces packed densely on the display's grid: each of the
/// category's own widgets a card, each family one block of its cards under its name (<see cref="CardGap"/>
/// apart), every piece at the first spot it fits on the first screen it fits (smaller ones filling the room beside
/// taller ones), <see cref="BlockGap"/> from another block's. Every card stands at its kind's natural size in whole cells
/// (what placing it makes, <see cref="Model.PlacementPayload.NewSize"/>), held to the area where it is larger.
/// Deterministic; no two cards share a cell, every box is inside the area, a family's cards together.
/// </summary>
public static class WidgetGallery
{
    /// <summary>[S-14912] Between two cards of a block, in cells: never touching, so no two join ([S-2240]).</summary>
    public const int CardGap = 1;

    /// <summary>[S-14912] Between two blocks (a family and anything else), in cells.</summary>
    public const int BlockGap = 2;

    /// <summary>[S-14912] A family's name line over its cards, in cells (a BodyStrong line and its air on the ~10-DIP grid).</summary>
    public const int HeadRows = 3;

    /// <summary>[S-14912] The category's title line at a screen's top, in cells (a Subtitle line and its air).</summary>
    public const int TitleRows = 4;

    /// <summary>
    /// [S-14912] The categories of <paramref name="page"/>: every subject on its rail (and More), each its own widgets and
    /// then its families, as the add page has them ([S-13611]); Start, ready-made groups and desktop items are not widgets
    /// of a category and stay the add page's. A subject with no widget is left out.
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

            var blocks = new List<GalleryBlock>();
            foreach (var section in page.Sections(item.Id))
            {
                WidgetId[] kinds = [.. section.Tiles.OfType<AddWidgetTile>().Select(t => t.Kind)];
                if (kinds.Length == 0)
                {
                    continue;
                }

                switch (section.Kind)
                {
                    case AddSectionKind.Widgets:
                        blocks.Add(new GalleryBlock(section.Key, null, kinds));
                        break;
                    case AddSectionKind.Family:
                        blocks.Add(new GalleryBlock(section.Key, section.Heading, kinds));
                        break;
                }
            }

            if (blocks.Count > 0)
            {
                categories.Add(new GalleryCategory(item.Id, item.Title, item.Glyph, blocks));
            }
        }

        return categories;
    }

    /// <summary>[S-14913] Every category's screens, one after another in the rail's order: what the arrows and the wheel walk.</summary>
    public static IReadOnlyList<GalleryScreen> Lay(IReadOnlyList<GalleryCategory> categories, CellBox area, Func<WidgetId, (int W, int H)> size)
    {
        ArgumentNullException.ThrowIfNull(categories);
        return [.. categories.SelectMany(c => Lay(c, area, size))];
    }

    /// <summary>
    /// [S-14912] One category's screens on <paramref name="area"/> (whole cells), each kind at <paramref name="size"/>'s cells,
    /// packed densely: each piece (a card of the category's own widgets, a family's block under its name) goes to the first
    /// spot it fits on the first screen it fits, reading order (<see cref="Spot"/>); a new screen only when it fits on none.
    /// A family's block is its cards packed the same way under its name, as wide as the room; one taller than a screen is
    /// cut into chunks, each under the name again, each on the screen of the one before or a later one.
    /// </summary>
    public static IReadOnlyList<GalleryScreen> Lay(GalleryCategory category, CellBox area, Func<WidgetId, (int W, int H)> size)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(size);
        var body = new CellBox(area.X, area.Y + TitleRows, Math.Max(1, area.W), Math.Max(1, area.H - TitleRows));
        var screens = new List<Sheet>();
        foreach (var block in category.Blocks)
        {
            var from = 0;
            foreach (var piece in Pieces(block, body, size))
            {
                // A family's chunk never goes to a screen before the one its previous chunk is on.
                var at = -1;
                CellBox spot = default;
                for (var i = piece.Continues ? from : 0; i < screens.Count && at < 0; i++)
                {
                    if (Spot(screens[i], piece, body) is { } found)
                    {
                        (at, spot) = (i, found);
                    }
                }

                if (at < 0)
                {
                    screens.Add(new Sheet());
                    (at, spot) = (screens.Count - 1, new CellBox(body.X, body.Y, piece.W, piece.H));
                }

                screens[at].Place(piece, spot);
                from = at;
            }
        }

        var title = new CellBox(area.X, area.Y, Math.Max(1, area.W), Math.Min(TitleRows, Math.Max(1, area.H)));
        return [.. screens.Select((s, i) => new GalleryScreen(category.Id, category.Title, i, screens.Count, title, s.Heads, s.Cards))];
    }

    /// <summary>[S-14913] The screen after (or before) <paramref name="at"/>, round the ends: the arrows, the wheel and Page Up/Down.</summary>
    public static int Step(int at, int count, bool back)
        => count <= 0 ? 0 : ((Math.Clamp(at, 0, count - 1) + (back ? -1 : 1)) % count + count) % count;

    /// <summary>[S-14913] The first screen of <paramref name="category"/> in <paramref name="screens"/>; 0 where it has none.</summary>
    public static int FirstOf(IReadOnlyList<GalleryScreen> screens, AddFolderId category)
    {
        ArgumentNullException.ThrowIfNull(screens);
        for (var i = 0; i < screens.Count; i++)
        {
            if (screens[i].Category == category)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// [S-14912] Where <paramref name="piece"/> first fits on <paramref name="sheet"/>: <see cref="Fit"/> over the boxes placed,
    /// <see cref="CardGap"/> from a card of its own block, <see cref="BlockGap"/> from another block's.
    /// </summary>
    private static CellBox? Spot(Sheet sheet, Piece piece, CellBox body)
        => Fit([.. sheet.Taken.Select(t => (t.Box, t.Block == piece.Block.Key ? CardGap : BlockGap))], piece.W, piece.H, body);

    /// <summary>
    /// [S-14912] The first spot for a box of <paramref name="w"/> × <paramref name="h"/> in <paramref name="room"/>, top-most then
    /// left-most, among the room's corner and the corners past every taken box (its gap on): inside the room and keeping
    /// each taken box's gap; null where it fits nowhere. Smaller boxes so fill the room beside taller ones.
    /// </summary>
    private static CellBox? Fit(IReadOnlyList<(CellBox Box, int Gap)> taken, int w, int h, CellBox room)
    {
        var xs = new SortedSet<int> { room.X };
        var ys = new SortedSet<int> { room.Y };
        foreach (var (box, gap) in taken)
        {
            xs.Add(box.Right + gap);
            ys.Add(box.Bottom + gap);
        }

        foreach (var y in ys)
        {
            if (y + h > room.Bottom)
            {
                break;
            }

            foreach (var x in xs)
            {
                if (x + w > room.Right)
                {
                    break;
                }

                var box = new CellBox(x, y, w, h);
                var clear = true;
                foreach (var (other, gap) in taken)
                {
                    if (new CellBox(other.X - gap, other.Y - gap, other.W + (2 * gap), other.H + (2 * gap)).Overlaps(box))
                    {
                        clear = false;
                        break;
                    }
                }

                if (clear)
                {
                    return box;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// [S-14912] A block's pieces: the category's own widgets a card each (no name to keep them under); a family one block
    /// under its name, its cards packed as the screen packs (<see cref="Fit"/>, a cell apart, as wide as the room), cut
    /// into chunks where the next card no longer fits under the name in a screen's height.
    /// </summary>
    private static IEnumerable<Piece> Pieces(GalleryBlock block, CellBox body, Func<WidgetId, (int W, int H)> size)
    {
        if (block.Title is null)
        {
            for (var i = 0; i < block.Kinds.Count; i++)
            {
                var (w, h) = Held(size(block.Kinds[i]), body.W, body.H);
                yield return new Piece(block, 0, [(i, new CellBox(0, 0, w, h))], false);
            }

            yield break;
        }

        var room = new CellBox(0, 0, body.W, Math.Max(1, body.H - HeadRows));
        var chunk = new List<(int Index, CellBox Box)>();
        var continues = false;
        for (var i = 0; i < block.Kinds.Count; i++)
        {
            var (w, h) = Held(size(block.Kinds[i]), room.W, room.H);
            if (Fit([.. chunk.Select(c => (c.Box, CardGap))], w, h, room) is not { } at)
            {
                yield return new Piece(block, HeadRows, chunk, continues);
                (chunk, continues, at) = ([], true, new CellBox(0, 0, w, h));
            }

            chunk.Add((i, at));
        }

        if (chunk.Count > 0)
        {
            yield return new Piece(block, HeadRows, chunk, continues);
        }
    }

    /// <summary>A piece to place: a card alone, or a family's chunk under its name (its cards' boxes from its corner under the name).</summary>
    private sealed record Piece(GalleryBlock Block, int Head, List<(int Index, CellBox Box)> Cards, bool Continues)
    {
        public int W => Cards.Max(c => c.Box.Right);

        public int H => Head + Cards.Max(c => c.Box.Bottom);
    }

    /// <summary>One screen being packed: the boxes taken (with their block's key), its heads and cards.</summary>
    private sealed class Sheet
    {
        public List<(CellBox Box, string Block)> Taken { get; } = [];

        public List<GalleryHead> Heads { get; } = [];

        public List<GalleryCard> Cards { get; } = [];

        public void Place(Piece piece, CellBox box)
        {
            Taken.Add((box, piece.Block.Key));
            if (piece.Block.Title is { } name)
            {
                Heads.Add(new GalleryHead(name, new CellBox(box.X, box.Y, box.W, piece.Head)));
            }

            foreach (var (index, at) in piece.Cards)
            {
                Cards.Add(new GalleryCard(piece.Block.Kinds[index], at with { X = box.X + at.X, Y = box.Y + piece.Head + at.Y }, piece.Block.Key));
            }
        }
    }

    /// <summary>A card's cells held to the room: at least one cell, never wider or taller than it.</summary>
    private static (int W, int H) Held((int W, int H) cells, int width, int height)
        => (Math.Clamp(cells.W, 1, Math.Max(1, width)), Math.Clamp(cells.H, 1, Math.Max(1, height)));
}
