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
/// can just implement multiple screens."). A category is a run of screens; on each, its title line, then its blocks
/// (the category's own widgets, then each family under its name, in the add tree's order) laid as tidy blocks on the
/// display's grid: a block's cards in rows left to right (<see cref="CardGap"/> apart, the row as tall as its tallest),
/// blocks side by side on a shelf while they fit across (<see cref="BlockGap"/> apart), the next shelf under the
/// tallest; a block that does not fit under what stands goes on from a shelf's start with as many of its rows as fit
/// there, the rest continuing on the next screen under its name again. Every card stands at its kind's natural size in whole cells
/// (what placing it makes, <see cref="Model.PlacementPayload.NewSize"/>), held to the area where it is larger.
/// Deterministic; no two cards share a cell, every box is inside the area.
/// </summary>
public static class WidgetGallery
{
    /// <summary>[S-14912] Between two cards of a block, in cells: never touching, so no two join ([S-2240]).</summary>
    public const int CardGap = 2;

    /// <summary>[S-14912] Between two blocks, in cells.</summary>
    public const int BlockGap = 6;

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

    /// <summary>[S-14912] One category's screens on <paramref name="area"/> (whole cells), each kind at <paramref name="size"/>'s cells.</summary>
    public static IReadOnlyList<GalleryScreen> Lay(GalleryCategory category, CellBox area, Func<WidgetId, (int W, int H)> size)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(size);
        var body = new CellBox(area.X, area.Y + TitleRows, Math.Max(1, area.W), Math.Max(1, area.H - TitleRows));
        var screens = new List<(List<GalleryHead> Heads, List<GalleryCard> Cards)>();
        List<GalleryHead> heads = [];
        List<GalleryCard> cards = [];
        int x = body.X, y = body.Y, shelf = 0;

        void NewScreen()
        {
            if (cards.Count > 0)
            {
                screens.Add((heads, cards));
            }

            heads = [];
            cards = [];
            (x, y, shelf) = (body.X, body.Y, 0);
        }

        void NewShelf()
        {
            (x, y, shelf) = (body.X, y + shelf + BlockGap, 0);
        }

        foreach (var block in category.Blocks)
        {
            var head = block.Title is null ? 0 : HeadRows;

            // Every card at its natural cells, held to the screen's body (under the block's name).
            var sizes = block.Kinds.Select(k => Held(size(k), body.W, body.H - head)).ToList();
            var rows = Rows(sizes, body.W);
            var at = 0;
            while (at < rows.Count)
            {
                // The rows of this block still to place, whole: does the rest fit across what is left of the shelf?
                var rest = rows.Skip(at).ToList();
                var width = rest.Max(r => r.Width);
                var height = head + rest.Sum(r => r.Height) + (CardGap * (rest.Count - 1));
                if (x > body.X && x + width > body.Right)
                {
                    NewShelf();
                }

                if (y + height <= body.Bottom)
                {
                    Place(block, head, rest, x, y);
                    (x, shelf) = (x + width + BlockGap, Math.Max(shelf, height));
                    break;
                }

                // Not all of it fits under what stands: from a shelf's start, as many of its rows as fit here.
                if (x > body.X)
                {
                    NewShelf();
                    continue;
                }

                var fit = 0;
                var used = head;
                while (fit < rest.Count && y + used + rest[fit].Height + (fit > 0 ? CardGap : 0) <= body.Bottom)
                {
                    used += rest[fit].Height + (fit > 0 ? CardGap : 0);
                    fit++;
                }

                if (fit == 0)
                {
                    // Not one row: the next screen, unless this one is empty (a row is never taller than a screen, held above).
                    if (cards.Count > 0)
                    {
                        NewScreen();
                        continue;
                    }

                    fit = 1;
                }

                // The rest of the block on the next screen, under its name again.
                Place(block, head, rest.Take(fit).ToList(), x, y);
                at += fit;
                NewScreen();
            }
        }

        NewScreen();
        var count = screens.Count;
        var title = new CellBox(area.X, area.Y, Math.Max(1, area.W), Math.Min(TitleRows, Math.Max(1, area.H)));
        return [.. screens.Select((s, i) => new GalleryScreen(category.Id, category.Title, i, count, title, s.Heads, s.Cards))];

        void Place(GalleryBlock block, int head, List<Row> rows, int left, int top)
        {
            if (block.Title is { } name)
            {
                heads.Add(new GalleryHead(name, new CellBox(left, top, rows.Max(r => r.Width), head)));
            }

            var rowTop = top + head;
            foreach (var row in rows)
            {
                var cx = left;
                foreach (var (index, w, h) in row.Cards)
                {
                    cards.Add(new GalleryCard(block.Kinds[index], new CellBox(cx, rowTop, w, h), block.Key));
                    cx += w + CardGap;
                }

                rowTop += row.Height + CardGap;
            }
        }
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

    /// <summary>A card's cells held to the room: at least one cell, never wider or taller than it.</summary>
    private static (int W, int H) Held((int W, int H) cells, int width, int height)
        => (Math.Clamp(cells.W, 1, Math.Max(1, width)), Math.Clamp(cells.H, 1, Math.Max(1, height)));

    /// <summary>One row of a block: its cards (their index in the block and cells), as wide and as tall as they make it.</summary>
    private sealed record Row(List<(int Index, int W, int H)> Cards)
    {
        public int Width => Cards.Sum(c => c.W) + (CardGap * (Cards.Count - 1));

        public int Height => Cards.Max(c => c.H);
    }

    /// <summary>The block's cards in rows left to right, a new row where the next would pass <paramref name="width"/>.</summary>
    private static List<Row> Rows(IReadOnlyList<(int W, int H)> sizes, int width)
    {
        var rows = new List<Row>();
        var row = new List<(int, int, int)>();
        var x = 0;
        for (var i = 0; i < sizes.Count; i++)
        {
            var (w, h) = sizes[i];
            if (row.Count > 0 && x + CardGap + w > width)
            {
                rows.Add(new Row(row));
                row = [];
                x = 0;
            }

            x += (row.Count > 0 ? CardGap : 0) + w;
            row.Add((i, w, h));
        }

        if (row.Count > 0)
        {
            rows.Add(new Row(row));
        }

        return rows;
    }
}
