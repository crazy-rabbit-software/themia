using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using G = Themia.Sdk.Kit.ShellMetrics.WidgetGallery;

namespace Themia.Core.Widgets;

/// <summary>[S-14912] A run of whole grid cells: a card's box or a tile's, counted from the display's top-left grid line.</summary>
public readonly record struct CellBox(int X, int Y, int W, int H)
{
    public int Right => X + W;

    public int Bottom => Y + H;

    /// <summary>Whether the two share any cell (touching edges do not).</summary>
    public bool Overlaps(CellBox other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    /// <summary>Whether <paramref name="inner"/> lies wholly inside this box.</summary>
    public bool Holds(CellBox inner) => inner.X >= X && inner.Y >= Y && inner.Right <= Right && inner.Bottom <= Bottom;
}

/// <summary>
/// [S-14912] One whole thing of the gallery: a widget that stands alone (<see cref="Kind"/>) or a ready-made group
/// (<see cref="Group"/>: several widgets laid as one panel). Never a small part of a family: those are the add page's.
/// </summary>
/// <param name="Key">The same in every language: the add page's tile key (<c>kind:clock</c>, <c>group:nowPlaying</c>).</param>
public sealed record GalleryPiece(string Key, WidgetId? Kind, ReadyMadeGroup? Group);

/// <summary>[S-14912] A category of the gallery: an add page subject (or More), its whole pieces in the add page's order.</summary>
public sealed record GalleryCategory(AddFolderId Id, LocalText Title, string Glyph, IReadOnlyList<GalleryPiece> Pieces);

/// <summary>
/// [S-14912] What a piece asks of the bento, in whole cells of the display's grid: its natural size, the least it may be,
/// and on which axes it may be made another size (a widget on the axes its sizing leaves free; a ready-made group across
/// only: taller, its rows would grow unevenly and its lines of text with them).
/// </summary>
public readonly record struct PieceSize(int W, int H, int MinW, int MinH, bool Wider, bool Taller);

/// <summary>
/// [S-14912] A piece on a category's sheet: the bento tile it owns (<see cref="Tile"/>) and the box it is drawn at
/// (<see cref="Box"/>: as wide and as tall as the tile on the axes the piece stretches on, its own length in the tile's
/// middle on the others), both in cells from the sheet's top-left corner.
/// </summary>
public sealed record GalleryTile(GalleryPiece Piece, CellBox Tile, CellBox Box);

/// <summary>[S-14912] A category laid as a bento: its tiles and how many rows of cells it is tall.</summary>
public sealed record GallerySheet(AddFolderId Category, LocalText Name, int Rows, IReadOnlyList<GalleryTile> Tiles);

/// <summary>
/// [S-14911] The gallery's window on a display, in DIPs, and the cells its cards are seen through: the window, the rail
/// of categories, the title line, the well the cards stand in, the panel of what is picked, and <see cref="View"/>, the
/// well's room in whole cells of the display's grid (whole bento tiles wide).
/// </summary>
public sealed record GalleryFrame(DipRect Window, DipRect Rail, DipRect Head, DipRect Well, DipRect Panel, CellBox View);

/// <summary>
/// [S-14910]..[S-14915] The widget gallery's arrangement, pure (owner, 2026-11-21: "for now lets just show complete widgets
/// / groups, not the small atoms. scrolling should not scroll the tabs but a tabs contents should be scrollable if they
/// dont all fit on the screen. a tabs widget should not actually be grouped into the subgroups but they should be shown
/// as a bento"; "right now everything is fullscreen but it should also just be inside a settings window"). A category is
/// its whole pieces (<see cref="Of"/>), laid as one bento (<see cref="Lay"/>): a lattice of tiles
/// (<see cref="Tile"/> cells wide and <see cref="TileTall"/> tall, <see cref="Gap"/> apart), every piece on whole tiles,
/// sized to them, packed densely and then grown into the room left beside it, so the block has straight edges. The
/// sheet is as tall as it needs; the window shows as much of it as it holds (<see cref="Frame"/>) and the rest is
/// scrolled to (<see cref="Scroll"/>). Deterministic; no two tiles share a cell.
/// </summary>
public static class WidgetGallery
{
    /// <summary>[S-14912] A bento tile's width, in cells.</summary>
    public const int Tile = 7;

    /// <summary>
    /// [S-14912] A bento tile's height, in cells: finer than its width, so a piece that keeps its height (a ready-made
    /// group) leaves little of its tile empty.
    /// </summary>
    public const int TileTall = 3;

    /// <summary>[S-14912] Between two tiles, in cells: never touching, so no two pieces join ([S-2240]).</summary>
    public const int Gap = 1;

    /// <summary>[S-14912] From one tile's start to the next's across, in cells.</summary>
    public const int Pitch = Tile + Gap;

    /// <summary>[S-14912] From one tile's top to the next's, in cells.</summary>
    public const int PitchTall = TileTall + Gap;

    /// <summary>[S-14913] How far one notch of the wheel and one arrow key scroll a sheet, in rows of cells: two tiles.</summary>
    public const int Step = 2 * PitchTall;

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

    /// <summary>[S-14912] The widest run of whole tiles in <paramref name="cells"/> cells (at least one tile).</summary>
    public static int Width(int cells) => Cells(Columns(cells), Pitch);

    /// <summary>[S-14912] How many tiles stand side by side in <paramref name="cells"/> cells (at least one).</summary>
    public static int Columns(int cells) => Math.Max(1, (cells + Gap) / Pitch);

    /// <summary>[S-14912] The cells <paramref name="tiles"/> tiles in a run take at <paramref name="pitch"/>, their gaps between them.</summary>
    public static int Cells(int tiles, int pitch) => Math.Max(0, (tiles * pitch) - Gap);

    /// <summary>
    /// [S-14912] How many tiles a length of <paramref name="cells"/> takes at <paramref name="pitch"/>: a piece that
    /// stretches on that axis the nearest count (it is made that long, never under <paramref name="min"/>), one that
    /// does not the fewest that hold it.
    /// </summary>
    public static int Span(int cells, int min, bool stretches, int pitch)
    {
        if (!stretches)
        {
            return Math.Max(1, (int)Math.Ceiling((Math.Max(1, cells) + Gap) / (double)pitch));
        }

        var span = Math.Max(1, (int)Math.Round((Math.Max(1, cells) + Gap) / (double)pitch, MidpointRounding.AwayFromZero));
        while (Cells(span, pitch) < min)
        {
            span++;
        }

        return span;
    }

    /// <summary>
    /// [S-14912] <paramref name="category"/> as a bento <paramref name="width"/> cells wide: each piece on whole tiles
    /// (<see cref="Span"/> of <paramref name="size"/>'s cells, held to the columns there are), placed in the add page's
    /// order at the first room it fits, top-most then left-most, so a smaller piece fills the room a larger one left; then
    /// every piece is grown on the axes it stretches on, to the right and then down, into tiles nothing took, by at most
    /// half its own span again, so the block's edges are straight. On an axis it stretches on a piece is drawn as long
    /// as its tile; on one it does not, at its own length in its tile's middle.
    /// </summary>
    public static GallerySheet Lay(GalleryCategory category, int width, Func<GalleryPiece, PieceSize> size)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(size);
        var cols = Columns(width);
        var taken = new List<bool[]>();
        var placed = new List<(GalleryPiece Piece, PieceSize Size, int Col, int Row, int C, int R, int C0, int R0)>();

        bool Free(int col, int row, int c, int r)
        {
            if (col < 0 || col + c > cols)
            {
                return false;
            }

            for (var y = row; y < row + r; y++)
            {
                for (var x = col; x < col + c && y < taken.Count; x++)
                {
                    if (taken[y][x])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        void Take(int col, int row, int c, int r)
        {
            while (taken.Count < row + r)
            {
                taken.Add(new bool[cols]);
            }

            for (var y = row; y < row + r; y++)
            {
                for (var x = col; x < col + c; x++)
                {
                    taken[y][x] = true;
                }
            }
        }

        foreach (var piece in category.Pieces)
        {
            var asked = size(piece);
            var c = Math.Min(cols, Span(asked.W, asked.MinW, asked.Wider, Pitch));
            var r = Span(asked.H, asked.MinH, asked.Taller, PitchTall);
            var at = (Col: 0, Row: taken.Count);
            for (var row = 0; row <= taken.Count; row++)
            {
                var found = false;
                for (var col = 0; col + c <= cols; col++)
                {
                    if (Free(col, row, c, r))
                    {
                        (at, found) = ((col, row), true);
                        break;
                    }
                }

                if (found)
                {
                    break;
                }
            }

            Take(at.Col, at.Row, c, r);
            placed.Add((piece, asked, at.Col, at.Row, c, r, c, r));
        }

        // Grown into the room left: every piece to its right first, then every piece down, never past the sheet's foot.
        var rows = taken.Count;
        for (var pass = 0; pass < 2; pass++)
        {
            var grew = true;
            while (grew)
            {
                grew = false;
                for (var i = 0; i < placed.Count; i++)
                {
                    var p = placed[i];
                    if (pass == 0 && p.Size.Wider && p.C < p.C0 + Math.Max(1, p.C0 / 2) && Free(p.Col + p.C, p.Row, 1, p.R))
                    {
                        Take(p.Col + p.C, p.Row, 1, p.R);
                        placed[i] = p with { C = p.C + 1 };
                        grew = true;
                    }
                    else if (pass == 1 && p.Size.Taller && p.Row + p.R < rows && p.R < p.R0 + Math.Max(1, p.R0 / 2) && Free(p.Col, p.Row + p.R, p.C, 1))
                    {
                        Take(p.Col, p.Row + p.R, p.C, 1);
                        placed[i] = p with { R = p.R + 1 };
                        grew = true;
                    }
                }
            }
        }

        var tiles = new List<GalleryTile>(placed.Count);
        foreach (var p in placed)
        {
            var tile = new CellBox(p.Col * Pitch, p.Row * PitchTall, Cells(p.C, Pitch), Cells(p.R, PitchTall));
            var w = p.Size.Wider ? tile.W : Math.Clamp(p.Size.W, 1, tile.W);
            var h = p.Size.Taller ? tile.H : Math.Clamp(p.Size.H, 1, tile.H);
            tiles.Add(new GalleryTile(p.Piece, tile, new CellBox(tile.X + ((tile.W - w) / 2), tile.Y + ((tile.H - h) / 2), w, h)));
        }

        return new GallerySheet(category.Id, category.Title, Cells(rows, PitchTall), tiles);
    }

    /// <summary>[S-14913] How far a sheet <paramref name="sheetRows"/> tall scrolls in a view <paramref name="viewRows"/> tall, in rows of cells.</summary>
    public static int MaxScroll(int sheetRows, int viewRows) => Math.Max(0, sheetRows - Math.Max(0, viewRows));

    /// <summary>[S-14913] A scroll of <paramref name="by"/> rows from <paramref name="at"/>, held to the sheet's ends.</summary>
    public static int Scroll(int at, int by, int max) => Math.Clamp(at + by, 0, Math.Max(0, max));

    /// <summary>[S-14913] A page of a view <paramref name="viewRows"/> tall: the view less one step, in whole steps, at least one.</summary>
    public static int PageRows(int viewRows) => Math.Max(Step, ((viewRows / Step) - 1) * Step);

    /// <summary>
    /// [S-14911] The gallery's window on a display: <see cref="G.Share"/> of the work area (<paramref name="work"/>, DIPs)
    /// each way, never wider than the work area less its margins nor taller than the room under the bar
    /// (<paramref name="barBottom"/>), in the middle of that room. Inside its padding: the rail on the left, the panel on
    /// the right, and between them the title line over the well. The cards' view is the well's room on whole cells of the
    /// display's grid (<paramref name="pitchX"/> by <paramref name="pitchY"/> DIPs a cell), inside the part new geometry
    /// may use (<paramref name="allowed"/>), whole tiles wide and in the well's middle across.
    /// </summary>
    public static GalleryFrame Frame(DipRect work, double barBottom, DipRect allowed, double pitchX, double pitchY)
    {
        var top = Math.Max(work.Y, barBottom) + ShellMetrics.Flyout.Gap;
        var room = new DipRect(work.X + G.Margin, top, Math.Max(0, work.Width - (2 * G.Margin)), Math.Max(0, work.Y + work.Height - G.Margin - top));
        var width = Math.Min(room.Width, Math.Max(G.MinWidth, work.Width * G.Share));
        var height = Math.Min(room.Height, Math.Max(G.MinHeight, work.Height * G.Share));
        var window = new DipRect(room.X + ((room.Width - width) / 2), room.Y + ((room.Height - height) / 2), width, height);

        var inner = new DipRect(window.X + G.Pad, window.Y + G.Pad, Math.Max(0, window.Width - (2 * G.Pad)), Math.Max(0, window.Height - (2 * G.Pad)));
        var rail = new DipRect(inner.X, inner.Y, Math.Min(G.Rail, inner.Width), inner.Height);
        var panel = new DipRect(inner.X + inner.Width - Math.Min(G.Panel, inner.Width), inner.Y, Math.Min(G.Panel, inner.Width), inner.Height);
        var middle = new DipRect(rail.X + rail.Width + G.Pad, inner.Y, Math.Max(0, panel.X - G.Pad - (rail.X + rail.Width + G.Pad)), inner.Height);
        var head = new DipRect(middle.X, middle.Y, middle.Width, Math.Min(G.Head, middle.Height));
        var well = new DipRect(middle.X, middle.Y + head.Height, middle.Width, Math.Max(0, middle.Height - head.Height));

        // The view: whole cells inside the well's own padding and the part new geometry may use.
        var x0 = Math.Max(well.X + G.WellPad, allowed.X);
        var y0 = Math.Max(well.Y + G.WellPad, allowed.Y);
        var x1 = Math.Min(well.X + well.Width - G.WellPad, allowed.X + allowed.Width);
        var y1 = Math.Min(well.Y + well.Height - G.WellPad, allowed.Y + allowed.Height);
        var cx = pitchX > 0 ? (int)Math.Ceiling((x0 / pitchX) - 1e-9) : 0;
        var cy = pitchY > 0 ? (int)Math.Ceiling((y0 / pitchY) - 1e-9) : 0;
        var cols = pitchX > 0 ? Math.Max(1, (int)Math.Floor((x1 / pitchX) + 1e-9) - cx) : 1;
        var rows = pitchY > 0 ? Math.Max(1, (int)Math.Floor((y1 / pitchY) + 1e-9) - cy) : 1;
        var wide = Math.Min(cols, Width(cols));
        return new GalleryFrame(window, rail, head, well, panel, new CellBox(cx + ((cols - wide) / 2), cy, wide, rows));
    }
}
