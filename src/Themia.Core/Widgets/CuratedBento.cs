using Themia.Core.Localization;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>[S-16743] A section of a laid curated tab: where it stands (its heading and its widgets, the column's width), for the editor.</summary>
public sealed record GalleryArea(CuratedSlot Slot, CellBox Box);

/// <summary>
/// [S-16743] What a curated widget is on a display: its card's cells in a column <c>width</c> cells wide (held to its sizing
/// there, never wider than the column).
/// </summary>
public sealed record CuratedFit(Func<CuratedWidget, int, (int W, int H)> Size)
{
    /// <summary>The sizes as stored, no sizing known (the pure tests'): as wide as stored, never wider than the column.</summary>
    public static CuratedFit AsStored { get; } = new((w, width) => (Math.Clamp(w.W, 1, Math.Max(1, width)), Math.Max(1, w.H)));
}

/// <summary>
/// [S-16740]..[S-16746] [S-17160]..[S-17162] A curated tab laid on a display, pure (owner, 2026-11-23: "in a column the widgets
/// are automatically aligned and i can change their height and width and then they are packed together as tightly as possible
/// in direction of reading"; "the columns should not stretch, they should have a fixed block width. between columns there
/// should be a two block space"; "when the columns cant fit onto one screen anymore scrolling in both directions should be
/// possible. dont scale or stretch anything for it."). Each column is exactly its width in blocks, the columns on one line from
/// the sheet's left <see cref="ColumnGap"/> apart, whatever the view's width: the room left at the right empty, or, where they
/// are wider than the view, the sheet wider than it (<see cref="GallerySheet.Cols"/>: the gallery scrolls across). Down a
/// column its sections stand one cell apart, each its heading (when titled) over its widgets packed (<see cref="Pack"/>): each
/// in turn at the highest free spot of the column's width that holds it, the left-most of those, one cell from every other.
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
    /// [S-16743] [S-17160] <paramref name="layout"/> laid, its columns at their widths: every widget a tile (its title's
    /// <paramref name="title"/> rows over its card when the layout has titles, [S-16721]), every titled section a heading of
    /// <paramref name="heading"/> rows. <paramref name="editing"/> ([S-16745]): every column starts with a band of
    /// <paramref name="heading"/> rows for its tools, every section has its heading line (titled or not) and an empty one room
    /// to drop into. <paramref name="piece"/> makes a widget's piece (the build's registry by default).
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

        return new GallerySheet(tab, name, foot, tiles) { Cols = x - ColumnGap, Title = title, Headings = headings, Areas = areas, Columns = boxes };

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

                var sizes = section.Widgets.Select(w => fit.Size(w, cw)).Select(z => (Math.Clamp(z.W, 1, cw), Math.Max(1, z.H) + title)).ToList();
                var packed = Pack(sizes, cw);
                var rows = 0;
                for (var i = 0; i < packed.Count; i++)
                {
                    if (piece(section.Widgets[i]) is not { } made)
                    {
                        continue;
                    }

                    var box = packed[i] with { X = packed[i].X + cx, Y = packed[i].Y + y };
                    tiles.Add(new GalleryTile(made, box) { Slot = new CuratedSlot(c, s, i) });
                    rows = Math.Max(rows, packed[i].Bottom);
                }

                if (editing && rows == 0)
                {
                    rows = EmptyRows;
                }

                y += rows;
                areas.Add(new GalleryArea(new CuratedSlot(c, s), new CellBox(cx, start, cw, Math.Max(1, y - start))));
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
    /// [S-16743] The widgets of a section packed in a column <paramref name="width"/> cells wide, in their order: each at the
    /// highest free spot that holds it (never wider than the column), the left-most of those, one cell from every other (no two
    /// share or touch a cell). The boxes are from the section's top-left corner.
    /// </summary>
    public static IReadOnlyList<CellBox> Pack(IReadOnlyList<(int W, int H)> sizes, int width)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        width = Math.Max(1, width);
        var placed = new List<CellBox>(sizes.Count);
        foreach (var (asked, tall) in sizes)
        {
            var (w, h) = (Math.Clamp(asked, 1, width), Math.Max(1, tall));

            // A spot that cannot move up stands at the top or a cell under a box; one that cannot move left at the left or a
            // cell right of a box: those are the only places the highest, then left-most, free spot can be.
            var ys = placed.Select(b => b.Bottom + WidgetGallery.Gap).Prepend(0).Distinct().Order().ToList();
            var xs = placed.Select(b => b.Right + WidgetGallery.Gap).Prepend(0).Where(cx => cx + w <= width).Distinct().Order().ToList();
            CellBox? spot = null;
            foreach (var y in ys)
            {
                foreach (var x in xs)
                {
                    var box = new CellBox(x, y, w, h);
                    if (!placed.Exists(b => Apart(b).Overlaps(box)))
                    {
                        spot = box;
                        break;
                    }
                }

                if (spot is not null)
                {
                    break;
                }
            }

            // Under everything at the left: always free (a widget as wide as the column finds it there).
            placed.Add(spot ?? new CellBox(0, placed.Count == 0 ? 0 : placed.Max(b => b.Bottom) + WidgetGallery.Gap, w, h));
        }

        return placed;

        static CellBox Apart(CellBox b) => new(b.X - WidgetGallery.Gap, b.Y - WidgetGallery.Gap, b.W + (2 * WidgetGallery.Gap), b.H + (2 * WidgetGallery.Gap));
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

    /// <summary>[S-16745] The place in its section a widget dropped at <paramref name="at"/> (a cell of the sheet) takes: before the first of the section's widgets that comes after that point in reading order.</summary>
    public static int Insertion(GallerySheet sheet, CuratedSlot section, (int X, int Y) at)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var mine = sheet.Tiles.Where(t => t.Slot is { } s && s.Column == section.Column && s.Section == section.Section).OrderBy(t => t.Slot!.Value.Widget).ToList();
        foreach (var tile in mine)
        {
            var box = tile.Box;
            if (at.Y < box.Y || (at.Y < box.Bottom && at.X < box.X + (box.W / 2)))
            {
                return tile.Slot!.Value.Widget;
            }
        }

        return mine.Count == 0 ? 0 : mine.Max(t => t.Slot!.Value.Widget) + 1;
    }
}
