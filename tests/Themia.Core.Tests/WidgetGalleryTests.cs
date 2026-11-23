using Themia.App.Shell;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14911]..[S-14913] [S-16400]..[S-16408] The widget gallery's arrangement: a category as a bento of whole cells, one cell
/// between two pieces and no hole a piece could fill; a piece in focus with what stands round it; its scrolling, its keys,
/// its window under the bar.
/// </summary>
public sealed class WidgetGalleryTests
{
    private const int Wide = 60;

    private static GalleryPiece Piece(string name) => new("kind:" + name, new WidgetId(name), null);

    private static GalleryCategory Category(params string[] names)
        => new(AddFolderId.Weather, LocalText.Fixed("Weather"), Glyphs.Apps, [.. names.Select(Piece)]);

    /// <summary>
    /// Sizes by the piece's name: "w30x20" is 30 × 20 cells and stretches both ways (least 3 each way); "f30x20" keeps
    /// that size; "g30x20" may be wider (to every third width) but neither smaller nor taller (a ready-made group); a letter
    /// after the height tells two of a size apart. Its words' card is 12 × 8, any size over it.
    /// </summary>
    private static PieceSize Size(GalleryPiece piece)
    {
        if (piece.Role == GalleryRole.About)
        {
            return new PieceSize(12, 8, 12, 8, true, true);
        }

        var name = piece.Kind!.Value.Value;
        var x = name.IndexOf('x', StringComparison.Ordinal);
        var tall = new string([.. name[(x + 1)..].TakeWhile(char.IsAsciiDigit)]);
        var (w, h) = (int.Parse(name[1..x], System.Globalization.CultureInfo.InvariantCulture), int.Parse(tall, System.Globalization.CultureInfo.InvariantCulture));
        if (piece.Role is GalleryRole.Picked or GalleryRole.Size)
        {
            return new PieceSize(w, h, w, h, false, false);
        }

        return name[0] switch
        {
            'f' => new PieceSize(w, h, w, h, false, false),
            'g' => new PieceSize(w, h, w, h, true, false) { Widths = [.. Enumerable.Range(0, 40).Select(i => w + (3 * i))] },
            _ => new PieceSize(w, h, 3, 3, true, true),
        };
    }

    private static void AssertTidy(GallerySheet sheet, int width)
    {
        var room = new CellBox(0, 0, width, sheet.Rows);
        Assert.All(sheet.Tiles, t => Assert.True(room.Holds(t.Box), $"{t.Piece.Key}'s box {t.Box} is outside {room}"));

        // No two share a cell, and none touches another: one cell between every two, so none joins ([S-2240]).
        for (var i = 0; i < sheet.Tiles.Count; i++)
        {
            for (var j = i + 1; j < sheet.Tiles.Count; j++)
            {
                var (a, b) = (sheet.Tiles[i].Box, sheet.Tiles[j].Box);
                var apart = new CellBox(a.X - WidgetGallery.Gap, a.Y - WidgetGallery.Gap, a.W + (2 * WidgetGallery.Gap), a.H + (2 * WidgetGallery.Gap));
                Assert.False(apart.Overlaps(b), $"{sheet.Tiles[i].Piece.Key} {a} and {sheet.Tiles[j].Piece.Key} {b} share cells or touch");
            }
        }
    }

    [Fact]
    public void S16403_a_category_of_pieces_that_stretch_is_one_bento_with_one_cell_between_two_and_no_hole()
    {
        var category = Category("w30x20", "w40x12", "w24x24", "w60x30", "w20x8", "w20x8b", "w10x10", "w30x10", "w34x26", "w16x16");

        var sheet = WidgetGallery.Lay(category, Wide, Size);

        Assert.Equal(category.Pieces, sheet.Tiles.Select(t => t.Piece));
        AssertTidy(sheet, Wide);
        Assert.Equal(sheet.Tiles.Max(t => t.Box.Bottom), sheet.Rows);
        Assert.Equal(AddFolderId.Weather, sheet.Category);
        Assert.Null(WidgetGallery.Grows(sheet, Wide, Size));
        Assert.True(WidgetGallery.Thickest(sheet, Wide) < 3, $"{WidgetGallery.Holes(sheet, Wide)} cells empty between pieces");
    }

    [Fact]
    public void S16403_the_holes_are_the_cells_no_piece_and_no_gap_takes()
    {
        // Between two pieces three cells apart: the first one's gap is its own, the two after it a hole, five rows deep (the
        // gap's row under them too). The room after the last, at the sheet's side, is its edge: no hole.
        GalleryTile Tile(int x, int y, int w, int h) => new(Piece("w1x1"), new CellBox(x, y, w, h));
        var apart = new GallerySheet(AddFolderId.Weather, LocalText.Fixed("x"), 4, [Tile(0, 0, 10, 4), Tile(13, 0, 10, 4)]);
        Assert.Equal(2 * 5, WidgetGallery.Holes(apart, 30));

        // Two pieces that keep their size, 10 wide on a sheet of 30: side by side one cell apart, the room after them the edge's.
        var sheet = WidgetGallery.Lay(Category("f10x4", "f10x4b"), 30, Size);
        Assert.Equal(new CellBox(0, 0, 10, 4), sheet.Tiles[0].Box);
        Assert.Equal(new CellBox(11, 0, 10, 4), sheet.Tiles[1].Box);
        Assert.Equal(0, WidgetGallery.Holes(sheet, 30));


        // Stretched, they fill the row: none.
        Assert.Equal(0, WidgetGallery.Holes(WidgetGallery.Lay(Category("w10x4", "w10x4b"), 30, Size), 30));
        Assert.Equal(0, WidgetGallery.Holes(WidgetGallery.Lay(Category(), 30, Size), 30));
    }

    [Fact]
    public void S16403_a_smaller_piece_fills_the_room_a_larger_one_left_one_cell_apart()
    {
        // A piece 40 by 20, then two of 19 by 9: beside it, one under the other, each one cell off what it stands by.
        var sheet = WidgetGallery.Lay(Category("f40x20", "f19x9", "f19x9b"), Wide, Size);

        Assert.Equal(new CellBox(0, 0, 40, 20), sheet.Tiles[0].Box);
        Assert.Equal(new CellBox(41, 0, 19, 9), sheet.Tiles[1].Box);
        Assert.Equal(new CellBox(41, 10, 19, 9), sheet.Tiles[2].Box);
        Assert.Equal(20, sheet.Rows);
    }

    [Fact]
    public void S16403_a_piece_grows_into_the_room_left_beside_it_and_under_it_never_past_the_foot()
    {
        // Two pieces 25 wide in a row of 60: the room left is taken, the row full to the sheet's end.
        var row = WidgetGallery.Lay(Category("w25x15", "w25x15b"), Wide, Size);
        Assert.Equal(Wide, row.Tiles[1].Box.Right);
        Assert.Equal(row.Tiles[0].Box.Right + WidgetGallery.Gap, row.Tiles[1].Box.X);

        // A piece that keeps its size does not grow; a group grows wider, to its own widths, never taller.
        var kept = WidgetGallery.Lay(Category("f39x15"), Wide, Size);
        Assert.Equal(39, kept.Tiles[0].Box.W);
        var group = WidgetGallery.Lay(Category("f30x20", "g20x7"), Wide, Size);
        Assert.Equal(7, group.Tiles[1].Box.H);
        Assert.Equal(29, group.Tiles[1].Box.W);
        Assert.Equal(0, (group.Tiles[1].Box.W - 20) % 3);

        // Down too, into room nothing took, never past the sheet's foot: under a piece that keeps its height of 15.
        var down = WidgetGallery.Lay(Category("f40x15", "w19x7"), Wide, Size);
        Assert.Equal(15, down.Rows);
        Assert.Equal(15, down.Tiles[1].Box.H);
    }

    [Fact]
    public void S16403_a_piece_wider_than_the_sheet_is_held_to_it_and_the_same_input_lays_the_same_sheet()
    {
        var wide = WidgetGallery.Lay(Category("w200x10"), Wide, Size);
        Assert.Equal(Wide, wide.Tiles[0].Box.W);
        AssertTidy(wide, Wide);

        var category = Category("w30x20", "w40x12", "w24x24", "f34x26", "w20x8");
        var a = WidgetGallery.Lay(category, Wide, Size);
        var b = WidgetGallery.Lay(category, Wide, Size);
        Assert.Equal(a.Tiles, b.Tiles);
        Assert.Equal(a.Rows, b.Rows);

        var none = WidgetGallery.Lay(Category(), Wide, Size);
        Assert.Empty(none.Tiles);
        Assert.Equal(0, none.Rows);
    }

    [Fact]
    public void S16404_a_piece_in_focus_has_its_words_its_other_styles_and_its_neighbours_round_it()
    {
        var category = Category("w20x10", "w30x12", "w14x8", "w18x9", "w16x8");
        var picked = category.Pieces[1];

        var focus = WidgetGallery.Focus(category, picked, otherStyles: [1, 2, 3], resizes: true);

        Assert.Equal(GalleryRole.Picked, focus.Pieces[0].Role);
        Assert.Equal(picked.Kind, focus.Pieces[0].Kind);
        Assert.Equal(GalleryRole.About, focus.Pieces[1].Role);
        Assert.Equal([1, 2, 3], focus.Pieces.Where(p => p.Role == GalleryRole.Style).Select(p => p.Style!.Value));
        Assert.All(focus.Pieces.Where(p => p.Role == GalleryRole.Style), p => Assert.Equal(picked.Kind, p.Kind));

        // Three styles or more: no sizes. Its neighbours after it in the category's order, wrapping, never itself.
        Assert.DoesNotContain(focus.Pieces, p => p.Role == GalleryRole.Size);
        var similar = focus.Pieces.Where(p => p.Role == GalleryRole.Similar).ToList();
        Assert.Equal(["similar:kind:w14x8", "similar:kind:w18x9", "similar:kind:w16x8", "similar:kind:w20x10"], similar.Select(p => p.Key));
        Assert.True(focus.Pieces.Count - 2 <= WidgetGallery.MostAround);
        Assert.Equal(focus.Pieces.Count, focus.Pieces.Select(p => p.Key).Distinct().Count());
    }

    [Fact]
    public void S16404_a_widget_of_few_styles_shows_its_sizes_a_group_its_neighbours_alone()
    {
        var category = Category("w20x10", "w30x12", "w14x8");
        var plain = WidgetGallery.Focus(category, category.Pieces[0], otherStyles: [], resizes: true);
        Assert.Equal([GallerySize.Least, GallerySize.Large], plain.Pieces.Where(p => p.Role == GalleryRole.Size).Select(p => p.Size!.Value));
        Assert.Equal(2, plain.Pieces.Count(p => p.Role == GalleryRole.Similar));

        // One that does not resize: no sizes.
        Assert.DoesNotContain(WidgetGallery.Focus(category, category.Pieces[0], [], resizes: false).Pieces, p => p.Role == GalleryRole.Size);

        var group = new GalleryPiece("group:g", null, null);
        var withGroup = category with { Pieces = [group, .. category.Pieces] };
        var focus = WidgetGallery.Focus(withGroup, group, [1, 2], resizes: true);
        Assert.Equal([GalleryRole.Picked, GalleryRole.About, GalleryRole.Similar, GalleryRole.Similar, GalleryRole.Similar], focus.Pieces.Select(p => p.Role));
    }

    [Fact]
    public void S16405_the_piece_in_focus_stands_at_the_top_in_the_middle_the_rest_packed_round_it()
    {
        var category = Category("w20x10", "w24x12", "w14x8", "w18x9", "w16x8", "w12x6");
        var focus = WidgetGallery.Focus(category, category.Pieces[1], otherStyles: [1, 2], resizes: false);

        var sheet = WidgetGallery.LayFocus(focus, Wide, Size);

        var middle = sheet.Tiles[0];
        Assert.Equal(GalleryRole.Picked, middle.Piece.Role);
        Assert.Equal(new CellBox((Wide - 24) / 2, 0, 24, 12), middle.Box);
        AssertTidy(sheet, Wide);

        // Round it: on its left and its right, then under it; one cell between two, every piece grown as far as it goes.
        Assert.Contains(sheet.Tiles, t => t.Box.Right < middle.Box.X && t.Box.Y < middle.Box.Bottom);
        Assert.Contains(sheet.Tiles, t => t.Box.X > middle.Box.Right && t.Box.Y < middle.Box.Bottom);
        Assert.Null(WidgetGallery.Grows(sheet, Wide, Size));
        Assert.True(WidgetGallery.Holes(sheet, Wide) * 50 <= (Wide + 1) * (sheet.Rows + 1));
    }

    [Theory]
    [InlineData(GalleryMove.Right, 1)]
    [InlineData(GalleryMove.Down, 2)]
    [InlineData(GalleryMove.Left, 0)]
    [InlineData(GalleryMove.Up, 0)]
    public void S16407_an_arrow_moves_the_pick_to_the_nearest_piece_that_way(GalleryMove move, int to)
    {
        GalleryTile Tile(int x, int y, int w, int h) => new(Piece("w1x1"), new CellBox(x, y, w, h));
        var sheet = new GallerySheet(AddFolderId.Weather, LocalText.Fixed("x"), 30, [Tile(0, 0, 10, 10), Tile(11, 0, 10, 10), Tile(0, 11, 21, 10)]);

        Assert.Equal(to, WidgetGallery.Next(sheet, 0, move));
        Assert.Equal(0, WidgetGallery.Next(sheet, null, move));

        // Nothing that way: the pick stays.
        Assert.Equal(2, WidgetGallery.Next(sheet, 2, GalleryMove.Down));
        Assert.Equal(1, WidgetGallery.Next(sheet, 1, GalleryMove.Right));
    }

    [Fact]
    public void S16407_a_pick_out_of_sight_is_scrolled_to_as_little_as_it_takes()
    {
        Assert.Equal(0, WidgetGallery.Reveal(0, new CellBox(0, 10, 5, 5), viewRows: 40, max: 100));
        Assert.Equal(25, WidgetGallery.Reveal(0, new CellBox(0, 60, 5, 5), viewRows: 40, max: 100));
        Assert.Equal(10, WidgetGallery.Reveal(30, new CellBox(0, 10, 5, 5), viewRows: 40, max: 100));
        Assert.Equal(20, WidgetGallery.Reveal(0, new CellBox(0, 50, 5, 30), viewRows: 40, max: 20));
    }

    [Fact]
    public void S16408_a_drop_opens_the_settings_a_drag_given_up_brings_the_gallery_back()
    {
        Assert.Equal(GalleryAfter.Settings, WidgetGallery.AfterPlacing(landed: true));
        Assert.Equal(GalleryAfter.Back, WidgetGallery.AfterPlacing(landed: false));
    }

    [Fact]
    public void S14913_a_sheet_taller_than_the_view_scrolls_by_whole_cells_between_its_ends()
    {
        Assert.Equal(0, WidgetGallery.MaxScroll(60, 71));
        Assert.Equal(49, WidgetGallery.MaxScroll(120, 71));

        Assert.Equal(WidgetGallery.Step, WidgetGallery.Scroll(0, WidgetGallery.Step, 49));
        Assert.Equal(49, WidgetGallery.Scroll(48, WidgetGallery.Step, 49));
        Assert.Equal(0, WidgetGallery.Scroll(3, -WidgetGallery.Step, 49));
        Assert.Equal(0, WidgetGallery.Scroll(5, 5, 0));

        // A page is the view less a step, in whole steps, never less than one.
        Assert.Equal(7 * WidgetGallery.Step, WidgetGallery.PageRows(71));
        Assert.Equal(WidgetGallery.Step, WidgetGallery.PageRows(10));
    }

    [Fact]
    public void S16400_the_gallery_hangs_flush_under_the_bar_its_tabs_over_the_well()
    {
        var work = new DipRect(0, 0, 1920, 1032);
        var frame = WidgetGallery.Frame(work, barBottom: 56, allowed: work, pitchX: 10, pitchY: 10);
        var g = frame.Window;

        // Four fifths of the work area each way, in the middle across, flush under the bar as the add page hangs.
        Assert.Equal(1920 * ShellMetrics.WidgetGallery.Share, g.Width, 6);
        Assert.Equal(1032 * ShellMetrics.WidgetGallery.Share, g.Height, 6);
        Assert.Equal((1920 - g.Width) / 2, g.X, 6);
        Assert.Equal(56 + ShellMetrics.Flyout.Gap, g.Y, 6);
        Assert.True(g.Y + g.Height <= 1032 - ShellMetrics.WidgetGallery.Margin + 1e-6);

        // [S-16401] The head (the tabs, the search) over the well, both the window's width inside its padding: no rail, no panel.
        static bool Inside(DipRect outer, DipRect inner)
            => inner.X >= outer.X - 1e-6 && inner.Y >= outer.Y - 1e-6 && inner.X + inner.Width <= outer.X + outer.Width + 1e-6 && inner.Y + inner.Height <= outer.Y + outer.Height + 1e-6;
        Assert.True(Inside(g, frame.Head) && Inside(g, frame.Well));
        Assert.Equal(ShellMetrics.WidgetGallery.Head, frame.Head.Height);
        Assert.Equal(g.Width - (2 * ShellMetrics.WidgetGallery.Pad), frame.Well.Width, 6);
        Assert.True(frame.Head.Y + frame.Head.Height <= frame.Well.Y);

        // The cards' view: whole cells inside the well.
        var view = frame.View;
        Assert.True(view.X * 10 >= frame.Well.X && view.Right * 10 <= frame.Well.X + frame.Well.Width);
        Assert.True(view.Y * 10 >= frame.Well.Y && view.Bottom * 10 <= frame.Well.Y + frame.Well.Height);
    }

    [Fact]
    public void S14911_a_small_work_area_holds_the_window_whole_and_the_view_stays_in_what_new_geometry_may_use()
    {
        // Smaller than the window's least (1200 by 640): the window is the room there is, never past the work area.
        var small = new DipRect(0, 0, 800, 500);
        var frame = WidgetGallery.Frame(small, barBottom: 56, allowed: small, pitchX: 10, pitchY: 10);
        Assert.True(frame.Window.X >= 0 && frame.Window.X + frame.Window.Width <= 800 + 1e-6);
        Assert.True(frame.Window.Y >= 56 && frame.Window.Y + frame.Window.Height <= 500 + 1e-6);
        Assert.True(frame.View.W >= 1 && frame.View.H >= 1);

        // A 1280 by 720 display (1920 by 1080 at 150 %): the window takes the least it may.
        var laptop = new DipRect(0, 0, 1280, 688);
        var held = WidgetGallery.Frame(laptop, barBottom: 56, allowed: laptop, pitchX: 10, pitchY: 10);
        Assert.Equal(ShellMetrics.WidgetGallery.MinWidth, held.Window.Width);

        // A part of the display new geometry may not use (a taskbar's side) cuts the view, not the window.
        var work = new DipRect(0, 0, 1920, 1032);
        var cut = WidgetGallery.Frame(work, barBottom: 56, allowed: new DipRect(0, 0, 1920, 600), pitchX: 10, pitchY: 10);
        Assert.True(cut.View.Bottom * 10 <= 600);
        Assert.Equal(WidgetGallery.Frame(work, 56, work, 10, 10).Window, cut.Window);
    }
}
