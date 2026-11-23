using Themia.Core.Localization;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-16743] [S-17221] A section of a laid curated tab: where it stands (its heading and its widgets, the column's width) and
/// <see cref="Room"/>, the sheet's row its widgets' places count from (a card at <c>y</c> 0 has its top there), for the editor.
/// </summary>
public sealed record GalleryArea(CuratedSlot Slot, CellBox Box)
{
    public int Room { get; init; }
}

/// <summary>
/// [S-17223] Widgets of a curated tab whose cards touch (owner, 2026-11-24: "allow me to create "groups" by connecting the
/// widgets in the editor together without space"): its tiles (indexes of <see cref="GallerySheet.Tiles"/>, the first the one
/// its title names: the top-most, then left-most card), the box round their cards and the one title over them.
/// </summary>
public sealed record GalleryGroup(IReadOnlyList<int> Tiles, CellBox Box, CellBox Title);

/// <summary>
/// [S-16743] What a curated widget is on a display: its card's cells in a column <c>width</c> cells wide (held to its sizing
/// there, never wider than the column).
/// </summary>
public sealed record CuratedFit(Func<CuratedWidget, int, (int W, int H)> Size)
{
    /// <summary>The sizes as stored, no sizing known (the pure tests', and the files' checks): as wide as stored, never wider than the column.</summary>
    public static CuratedFit AsStored { get; } = new((w, width) => (Math.Clamp(w.W, 1, Math.Max(1, width)), Math.Max(1, w.H)));
}

/// <summary>
/// [S-16740]..[S-16746] [S-17160]..[S-17162] [S-17220]..[S-17224] A curated tab laid on a display, pure (owner, 2026-11-23:
/// "the columns should not stretch, they should have a fixed block width. between columns there should be a two block space";
/// "when the columns cant fit onto one screen anymore scrolling in both directions should be possible. dont scale or stretch
/// anything for it."; 2026-11-24: "in the gallery edit mode i should be able to rearrange the widgets like i am on the desktop,
/// just in the column that it is placed though"). Each column is exactly its width in blocks, the columns on one line from
/// the sheet's left <see cref="ColumnGap"/> apart, whatever the view's width: the room left at the right empty, or, where they
/// are wider than the view, the sheet wider than it (<see cref="GallerySheet.Cols"/>: the gallery scrolls across). Down a
/// column its sections stand one cell apart, each its heading (when titled) over its widgets, every widget's card exactly at
/// its place (<see cref="CuratedWidget.X"/>, <see cref="CuratedWidget.Y"/>, held inside its column); a section is as tall as
/// its widgets reach. Cards that touch are a group (<see cref="Groups"/>): one title over them, picked and placed as one.
/// Deterministic, and the same on every display.
/// </summary>
public static class CuratedBento
{
    /// <summary>[S-17160] The blocks between two columns (owner: "a two block space").</summary>
    public const int ColumnGap = 2;

    /// <summary>[S-17163] The width a new column is given where nothing says more, in blocks (about 200 DIPs).</summary>
    public const int NewColumn = 20;

    /// <summary>[S-16745] How tall an empty section stands while editing, in rows: room to drop a widget into.</summary>
    public const int EmptyRows = 6;

    /// <summary>
    /// [S-16743] [S-17160] [S-17221] <paramref name="layout"/> laid, its columns at their widths: every widget's card at its
    /// place, under its title's <paramref name="title"/> rows when the layout has titles ([S-16721]; a group's one title over
    /// it, [S-17223]), every titled section a heading of <paramref name="heading"/> rows. <paramref name="editing"/>
    /// ([S-16745]): every column starts with a band of <paramref name="heading"/> rows for its tools, every section has its
    /// heading line (titled or not) and an empty one room to drop into. <paramref name="piece"/> makes a widget's piece (the
    /// build's registry by default).
    /// </summary>
    public static GallerySheet Lay(AddFolderId tab, LocalText name, CuratedLayout layout, CuratedFit fit, int title, int heading, bool editing = false, Func<CuratedWidget, GalleryPiece?>? piece = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fit);
        piece ??= Piece;
        title = layout.Titles ? Math.Max(0, title) : 0;
        heading = Math.Max(1, heading);
        var columns = layout.Columns;
        var tiles = new List<GalleryTile>();
        var headings = new List<GalleryHeading>();
        var areas = new List<GalleryArea>();
        var groups = new List<GalleryGroup>();
        var boxes = new CellBox[columns.Count];
        if (columns.Count == 0)
        {
            return new GallerySheet(tab, name, 0, []) { Title = title };
        }

        // [S-17160] [S-17161] Every column exactly its blocks, all on one line from the left, two blocks apart: never stacked,
        // narrowed or stretched for the view (a sheet wider than it scrolls across).
        var x = 0;
        var foot = 0;
        for (var c = 0; c < columns.Count; c++)
        {
            var w = Math.Max(1, columns[c].Width);
            foot = Math.Max(foot, Column(c, x, w, 0));
            x += w + ColumnGap;
        }

        return new GallerySheet(tab, name, foot, tiles) { Cols = x - ColumnGap, Title = title, Headings = headings, Areas = areas, Columns = boxes, Groups = groups };

        // One column laid from row y down at x, w wide: its tools' band, then its sections one cell apart. Its foot.
        int Column(int c, int cx, int cw, int y)
        {
            var top = y;
            if (editing)
            {
                y += heading + WidgetGallery.Gap;
            }

            var sections = columns[c].Sections;
            for (var s = 0; s < sections.Count; s++)
            {
                var section = sections[s];
                var start = y;
                if (editing || !string.IsNullOrWhiteSpace(section.Title))
                {
                    headings.Add(new GalleryHeading(string.IsNullOrWhiteSpace(section.Title) ? default : LocalText.Fixed(section.Title), new CellBox(cx, y, cw, heading)) { Slot = new CuratedSlot(c, s) });
                    y += heading;
                }

                // [S-17221] Each card at its place in the section (a place missing or outside the column: the first free spot,
                // as the file's check repairs it), its title's rows over it inside the section.
                var cards = Places(section.Widgets, cw, title, fit, out _, repairClashes: false);
                var room = y;
                var rows = 0;
                var mine = new List<(int Tile, CellBox Card)>();
                for (var i = 0; i < cards.Count; i++)
                {
                    if (piece(section.Widgets[i]) is not { } made)
                    {
                        continue;
                    }

                    var card = cards[i] with { X = cards[i].X + cx, Y = cards[i].Y + room + title };
                    mine.Add((tiles.Count, card));
                    tiles.Add(new GalleryTile(made, card with { Y = card.Y - title, H = card.H + title }) { Slot = new CuratedSlot(c, s, i) });
                    rows = Math.Max(rows, cards[i].Bottom + title);
                }

                // [S-17223] The cards that touch: a group each, its first the top-most, then left-most card.
                foreach (var set in Groups([.. mine.Select(m => m.Card)]).Where(g => g.Count > 1))
                {
                    var members = set.Select(i => mine[i]).OrderBy(m => m.Card.Y).ThenBy(m => m.Card.X).ToList();
                    var box = Bounds(members.Select(m => m.Card));
                    foreach (var (tile, _) in members)
                    {
                        tiles[tile] = tiles[tile] with { Group = groups.Count };
                    }

                    groups.Add(new GalleryGroup([.. members.Select(m => m.Tile)], box, box with { Y = box.Y - title, H = title }));
                }

                if (editing && rows == 0)
                {
                    rows = EmptyRows;
                }

                y += rows;
                areas.Add(new GalleryArea(new CuratedSlot(c, s), new CellBox(cx, start, cw, Math.Max(1, y - start))) { Room = room + title });
                if (s < sections.Count - 1)
                {
                    y += WidgetGallery.Gap;
                }
            }

            boxes[c] = new CellBox(cx, top, cw, y - top);
            return y;
        }
    }

    /// <summary>
    /// [S-17221] [S-17222] The cards of a section's <paramref name="widgets"/> in a column <paramref name="width"/> cells wide,
    /// from the section's room (<see cref="CuratedWidget.Y"/>), each its size by <paramref name="fit"/>: each at its place,
    /// held inside the column (its x moved left as far as it takes). A widget with no place, and with
    /// <paramref name="repairClashes"/> one whose card would clash with those before it (<see cref="Clashes"/>), goes to the first
    /// free spot (<see cref="FirstFree"/>), its index in <paramref name="repaired"/>; those with places stand first.
    /// </summary>
    public static IReadOnlyList<CellBox> Places(IReadOnlyList<CuratedWidget> widgets, int width, int title, CuratedFit fit, out IReadOnlyList<int> repaired, bool repairClashes = true)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        ArgumentNullException.ThrowIfNull(fit);
        width = Math.Max(1, width);
        title = Math.Max(0, title);
        var boxes = new CellBox?[widgets.Count];
        var placed = new List<CellBox>();
        var moved = new List<int>();
        for (var i = 0; i < widgets.Count; i++)
        {
            var (w, h) = Sized(widgets[i], width, fit);
            if (widgets[i] is not { X: { } wx, Y: { } wy } || wx < 0 || wy < 0)
            {
                continue;
            }

            var box = new CellBox(Math.Min(wx, width - w), wy, w, h);
            if (repairClashes && Clashes([.. placed, box], title))
            {
                continue;
            }

            boxes[i] = box;
            placed.Add(box);
        }

        for (var i = 0; i < widgets.Count; i++)
        {
            if (boxes[i] is null)
            {
                var (w, h) = Sized(widgets[i], width, fit);
                var box = FirstFree(placed, w, h, width, title);
                boxes[i] = box;
                placed.Add(box);
                moved.Add(i);
            }
        }

        repaired = moved;
        return [.. boxes.Select(b => b!.Value)];
    }

    /// <summary>A widget's card cells in a column: as <paramref name="fit"/> makes it, never wider than the column.</summary>
    private static (int W, int H) Sized(CuratedWidget widget, int width, CuratedFit fit)
    {
        var (w, h) = fit.Size(widget, width);
        return (Math.Clamp(w, 1, width), Math.Max(1, h));
    }

    /// <summary>
    /// [S-16743] [S-17222] Where a card of <paramref name="w"/> by <paramref name="h"/> cells goes among <paramref name="cards"/>
    /// in a column <paramref name="width"/> cells wide, as the packer laid every widget until 2026-11-24: its tile (its title's
    /// <paramref name="title"/> rows and its card) at the highest free spot that holds it, the left-most of those, one cell from
    /// every other's tile (no two share or touch a cell: it joins none). The box is its card's, from the section's room.
    /// </summary>
    public static CellBox FirstFree(IReadOnlyList<CellBox> cards, int w, int h, int width, int title)
    {
        ArgumentNullException.ThrowIfNull(cards);
        width = Math.Max(1, width);
        (w, h) = (Math.Clamp(w, 1, width), Math.Max(1, h));

        // A tile's top is its card's less its title: from the room's top, -title.
        var tiles = cards.Select(c => new CellBox(c.X, c.Y - title, c.W, c.H + title)).ToList();

        // A spot that cannot move up stands at the top or a cell under a tile; one that cannot move left at the left or a
        // cell right of a tile: those are the only places the highest, then left-most, free spot can be.
        var ys = tiles.Select(b => b.Bottom + WidgetGallery.Gap).Prepend(-title).Where(y => y >= -title).Distinct().Order().ToList();
        var xs = tiles.Select(b => b.Right + WidgetGallery.Gap).Prepend(0).Where(cx => cx + w <= width).Distinct().Order().ToList();
        foreach (var y in ys)
        {
            foreach (var x in xs)
            {
                var tile = new CellBox(x, y, w, h + title);
                if (!tiles.Exists(b => Apart(b).Overlaps(tile)))
                {
                    return new CellBox(x, y + title, w, h);
                }
            }
        }

        // Under everything at the left: always free (a widget as wide as the column finds it there).
        return new CellBox(0, tiles.Count == 0 ? 0 : tiles.Max(b => b.Bottom) + WidgetGallery.Gap + title, w, h);

        static CellBox Apart(CellBox b) => new(b.X - WidgetGallery.Gap, b.Y - WidgetGallery.Gap, b.W + (2 * WidgetGallery.Gap), b.H + (2 * WidgetGallery.Gap));
    }

    /// <summary>
    /// [S-17223] The cards that touch one another, as sets of indexes (owner, 2026-11-24: groups "by connecting the widgets in
    /// the editor together without space"): two whose cells meet side to side (<see cref="CellBox.Meets"/>) are in one set, and
    /// so is everything that touches either; a card touching nothing a set of its own (a cell between two keeps them apart). In
    /// first-index order.
    /// </summary>
    public static List<List<int>> Groups(IReadOnlyList<CellBox> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var root = Enumerable.Range(0, cards.Count).ToArray();
        int Find(int i) => root[i] == i ? i : root[i] = Find(root[i]);
        for (var i = 0; i < cards.Count; i++)
        {
            for (var j = i + 1; j < cards.Count; j++)
            {
                if (cards[i].Meets(cards[j]))
                {
                    root[Find(i)] = Find(j);
                }
            }
        }

        return [.. Enumerable.Range(0, cards.Count).GroupBy(Find).Select(g => g.ToList())];
    }

    /// <summary>
    /// [S-17222] Whether the cards of a section cannot stand so (a drop or a resize to them is refused, as nothing may land on
    /// another widget): two cards share a cell, or, with titles (<paramref name="title"/> rows over each group, or card alone,
    /// [S-16721]), a title runs over a card of another or over another's title, or above the section's room.
    /// </summary>
    public static bool Clashes(IReadOnlyList<CellBox> cards, int title)
    {
        ArgumentNullException.ThrowIfNull(cards);
        for (var i = 0; i < cards.Count; i++)
        {
            for (var j = i + 1; j < cards.Count; j++)
            {
                if (cards[i].Overlaps(cards[j]))
                {
                    return true;
                }
            }
        }

        if (title <= 0)
        {
            return false;
        }

        var sets = Groups(cards);
        var bands = sets.Select(set => Bounds(set.Select(i => cards[i])) is var box ? box with { Y = box.Y - title, H = title } : default).ToList();
        for (var g = 0; g < sets.Count; g++)
        {
            if (bands[g].Y < -title || cards.Where((_, i) => !sets[g].Contains(i)).Any(c => c.Overlaps(bands[g])) || bands.Where((_, o) => o != g).Any(b => b.Overlaps(bands[g])))
            {
                return true;
            }
        }

        return false;
    }

    private static CellBox Bounds(IEnumerable<CellBox> boxes)
    {
        var all = boxes.ToList();
        var (left, top) = (all.Min(b => b.X), all.Min(b => b.Y));
        return new CellBox(left, top, all.Max(b => b.Right) - left, all.Max(b => b.Bottom) - top);
    }

    /// <summary>
    /// [S-16742] The gallery's piece for a curated widget, read in the build's registry: its kind, preset and style; null for
    /// a widget the build does not have (dropped where the file is read, [S-16746]).
    /// </summary>
    public static GalleryPiece? Piece(CuratedWidget widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        if (CuratedLayouts.PickOf(widget.Pick) is not { } pick)
        {
            return null;
        }

        int? style = null;
        if (widget.Style is { } name)
        {
            var styles = WidgetStyles.For(pick.Kind);
            var at = Enumerable.Range(0, styles.Count).FirstOrDefault(i => string.Equals(styles[i].Name, name, StringComparison.OrdinalIgnoreCase), -1);
            style = at >= 0 ? styles[at].Value : null;
        }

        var key = style is { } s ? $"style:kind:{pick.Key}:{s}" : "kind:" + pick.Key;
        return new GalleryPiece(key, pick.Kind, null) { Preset = pick.Preset, Style = style, Config = widget.Config };
    }

    /// <summary>
    /// [S-17221] The section of its column a card whose top is on sheet row <paramref name="row"/> lands in: the one whose box
    /// holds that row, else the last that starts above it, else the column's first; and its place there, from that section's
    /// room (at least 0). Null where the column has no section.
    /// </summary>
    public static (CuratedSlot Section, int Y)? Landing(GallerySheet sheet, int column, int row)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var areas = sheet.Areas.Where(a => a.Slot.Column == column).OrderBy(a => a.Box.Y).ToList();
        if (areas.Count == 0)
        {
            return null;
        }

        var area = areas.FirstOrDefault(a => row >= a.Box.Y && row < a.Box.Bottom) ?? areas.LastOrDefault(a => a.Box.Y <= row) ?? areas[0];
        return (area.Slot, Math.Max(0, row - area.Room));
    }
}
