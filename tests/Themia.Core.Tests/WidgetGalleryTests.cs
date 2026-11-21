using Themia.App.Shell;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-14911]..[S-14913] The widget gallery's arrangement: a category as a bento of whole tiles, its scrolling, its window.</summary>
public sealed class WidgetGalleryTests
{
    /// <summary>Twelve tiles across.</summary>
    private const int Wide = (12 * WidgetGallery.Pitch) - WidgetGallery.Gap;

    private static GalleryPiece Piece(string name) => new("kind:" + name, new WidgetId(name), null);

    private static GalleryCategory Category(params string[] names)
        => new(AddFolderId.Weather, LocalText.Fixed("Weather"), Glyphs.Apps, [.. names.Select(Piece)]);

    /// <summary>
    /// Sizes by the piece's name: "w30x20" is 30 × 20 cells and stretches both ways (least 3 each way); "f30x20" keeps
    /// that size; "g30x20" may be wider but neither smaller nor taller (a ready-made group); a letter after the height
    /// tells two of a size apart.
    /// </summary>
    private static PieceSize Size(GalleryPiece piece)
    {
        var name = piece.Kind!.Value.Value;
        var x = name.IndexOf('x', StringComparison.Ordinal);
        var tall = new string([.. name[(x + 1)..].TakeWhile(char.IsAsciiDigit)]);
        var (w, h) = (int.Parse(name[1..x], System.Globalization.CultureInfo.InvariantCulture), int.Parse(tall, System.Globalization.CultureInfo.InvariantCulture));
        return name[0] switch
        {
            'f' => new PieceSize(w, h, w, h, false, false),
            'g' => new PieceSize(w, h, w, h, true, false),
            _ => new PieceSize(w, h, 3, 3, true, true),
        };
    }

    private static void AssertTidy(GallerySheet sheet, int width)
    {
        var room = new CellBox(0, 0, width, sheet.Rows);
        Assert.All(sheet.Tiles, t => Assert.True(room.Holds(t.Tile), $"{t.Piece.Key}'s tile {t.Tile} is outside {room}"));
        Assert.All(sheet.Tiles, t => Assert.True(t.Tile.Holds(t.Box), $"{t.Piece.Key}'s box {t.Box} is outside its tile {t.Tile}"));

        // Every tile on the lattice: its corner on a tile's start, its sides whole tiles with their gaps.
        Assert.All(sheet.Tiles, t =>
        {
            Assert.Equal(0, t.Tile.X % WidgetGallery.Pitch);
            Assert.Equal(0, t.Tile.Y % WidgetGallery.PitchTall);
            Assert.Equal(WidgetGallery.Tile, t.Tile.W % WidgetGallery.Pitch);
            Assert.Equal(WidgetGallery.TileTall, t.Tile.H % WidgetGallery.PitchTall);
        });

        // No two share a cell, and none touches another: a gap between every two, so none joins ([S-2240]).
        for (var i = 0; i < sheet.Tiles.Count; i++)
        {
            for (var j = i + 1; j < sheet.Tiles.Count; j++)
            {
                var (a, b) = (sheet.Tiles[i].Tile, sheet.Tiles[j].Tile);
                var apart = new CellBox(a.X - WidgetGallery.Gap, a.Y - WidgetGallery.Gap, a.W + (2 * WidgetGallery.Gap), a.H + (2 * WidgetGallery.Gap));
                Assert.False(apart.Overlaps(b), $"{sheet.Tiles[i].Piece.Key} {a} and {sheet.Tiles[j].Piece.Key} {b} share cells or touch");
            }
        }
    }

    [Fact]
    public void S14912_the_lattice_is_whole_tiles_with_a_gap_between_finer_in_height_than_in_width()
    {
        Assert.Equal(WidgetGallery.Tile + WidgetGallery.Gap, WidgetGallery.Pitch);
        Assert.Equal(WidgetGallery.TileTall + WidgetGallery.Gap, WidgetGallery.PitchTall);
        Assert.True(WidgetGallery.TileTall < WidgetGallery.Tile);
        Assert.Equal(12, WidgetGallery.Columns(Wide));
        Assert.Equal(12, WidgetGallery.Columns(Wide + 5));
        Assert.Equal(1, WidgetGallery.Columns(3));
        Assert.Equal(Wide, WidgetGallery.Width(Wide + 5));
        Assert.Equal(WidgetGallery.Tile, WidgetGallery.Cells(1, WidgetGallery.Pitch));
        Assert.Equal((2 * WidgetGallery.Pitch) - WidgetGallery.Gap, WidgetGallery.Cells(2, WidgetGallery.Pitch));
        Assert.Equal(WidgetGallery.TileTall, WidgetGallery.Cells(1, WidgetGallery.PitchTall));
        Assert.Equal(0, WidgetGallery.Cells(0, WidgetGallery.Pitch));
    }

    [Theory]
    [InlineData(7, 1)]
    [InlineData(10, 1)]
    [InlineData(12, 2)]
    [InlineData(15, 2)]
    [InlineData(24, 3)]
    [InlineData(30, 4)]
    public void S14912_a_piece_that_stretches_takes_the_nearest_whole_tiles(int cells, int tiles)
        => Assert.Equal(tiles, WidgetGallery.Span(cells, 1, stretches: true, WidgetGallery.Pitch));

    [Fact]
    public void S14912_a_piece_is_never_made_smaller_than_its_least_and_one_that_keeps_its_length_never_cut()
    {
        // 10 cells would round to one tile (7), under its least of 9: two.
        Assert.Equal(2, WidgetGallery.Span(10, 9, stretches: true, WidgetGallery.Pitch));

        // One that keeps its length takes the fewest tiles that hold it.
        Assert.Equal(2, WidgetGallery.Span(8, 8, stretches: false, WidgetGallery.Pitch));
        Assert.Equal(1, WidgetGallery.Span(7, 7, stretches: false, WidgetGallery.Pitch));
        Assert.Equal(4, WidgetGallery.Span(24, 24, stretches: false, WidgetGallery.Pitch));

        // Down the sheet the tiles are finer: a group 17 cells tall leaves two of its tile's cells empty, not six.
        Assert.Equal(5, WidgetGallery.Span(17, 17, stretches: false, WidgetGallery.PitchTall));
        Assert.Equal(19, WidgetGallery.Cells(5, WidgetGallery.PitchTall));
        Assert.Equal(3, WidgetGallery.Span(12, 1, stretches: true, WidgetGallery.PitchTall));
    }

    [Fact]
    public void S14912_a_category_is_a_bento_of_whole_tiles_no_two_sharing_a_cell()
    {
        var category = Category("w30x20", "w40x12", "w24x24", "w60x30", "w20x8", "w20x8b", "w10x10", "w30x10", "f34x26", "w16x16");

        var sheet = WidgetGallery.Lay(category, Wide, Size);

        Assert.Equal(category.Pieces, sheet.Tiles.Select(t => t.Piece));
        AssertTidy(sheet, Wide);
        Assert.Equal(sheet.Tiles.Max(t => t.Tile.Bottom), sheet.Rows);
        Assert.Equal(AddFolderId.Weather, sheet.Category);
    }

    [Fact]
    public void S14912_a_piece_is_as_long_as_its_tile_on_the_axes_it_stretches_on_and_in_its_middle_on_the_others()
    {
        var sheet = WidgetGallery.Lay(Category("w30x20", "f34x26", "g30x17"), Wide, Size);

        var stretched = sheet.Tiles[0];
        Assert.Equal(stretched.Tile, stretched.Box);

        var kept = sheet.Tiles[1];
        Assert.Equal((34, 26), (kept.Box.W, kept.Box.H));
        Assert.Equal((5 * WidgetGallery.Pitch) - WidgetGallery.Gap, kept.Tile.W);
        Assert.Equal((7 * WidgetGallery.PitchTall) - WidgetGallery.Gap, kept.Tile.H);
        Assert.Equal(kept.Tile.X + ((kept.Tile.W - 34) / 2), kept.Box.X);
        Assert.Equal(kept.Tile.Y + ((kept.Tile.H - 26) / 2), kept.Box.Y);

        // A group: as wide as its tile, its own height in the tile's middle.
        var group = sheet.Tiles[2];
        Assert.Equal((group.Tile.X, group.Tile.W), (group.Box.X, group.Box.W));
        Assert.Equal(17, group.Box.H);
        Assert.Equal(19, group.Tile.H);
        Assert.Equal(group.Tile.Y + 1, group.Box.Y);
    }

    [Fact]
    public void S14912_a_smaller_piece_fills_the_room_a_larger_one_left()
    {
        // A piece eight tiles wide and eight tall, then two of four by two: beside it, one under the other.
        var sheet = WidgetGallery.Lay(Category("f63x31", "f31x7", "f31x7b"), Wide, Size);

        Assert.Equal(new CellBox(0, 0, 63, 31), sheet.Tiles[0].Tile);
        Assert.Equal((8 * WidgetGallery.Pitch, 0), (sheet.Tiles[1].Tile.X, sheet.Tiles[1].Tile.Y));
        Assert.Equal((8 * WidgetGallery.Pitch, 2 * WidgetGallery.PitchTall), (sheet.Tiles[2].Tile.X, sheet.Tiles[2].Tile.Y));
        Assert.Equal(31, sheet.Rows);
    }

    [Fact]
    public void S14912_a_piece_grows_into_the_room_left_beside_it_so_the_edges_are_straight()
    {
        // Two pieces of five tiles each in a row of twelve: the first keeps its place and size (the second stands beside
        // it), the second grows into the two tiles left, and the row is full to the sheet's end.
        var row = WidgetGallery.Lay(Category("w39x15", "w39x15b"), Wide, Size);
        Assert.Equal((5 * WidgetGallery.Pitch) - WidgetGallery.Gap, row.Tiles[0].Tile.W);
        Assert.Equal(5 * WidgetGallery.Pitch, row.Tiles[1].Tile.X);
        Assert.Equal(Wide, row.Tiles[1].Tile.Right);

        // Never by more than half its own span again: one piece of two tiles alone stays three wide at most.
        var alone = WidgetGallery.Lay(Category("w15x7"), Wide, Size);
        Assert.Equal((3 * WidgetGallery.Pitch) - WidgetGallery.Gap, alone.Tiles[0].Tile.W);

        // A piece that keeps its size does not grow; a group grows wider, never taller.
        var kept = WidgetGallery.Lay(Category("f39x15"), Wide, Size);
        Assert.Equal((5 * WidgetGallery.Pitch) - WidgetGallery.Gap, kept.Tiles[0].Tile.W);
        var group = WidgetGallery.Lay(Category("f47x31", "g31x7"), Wide, Size);
        Assert.Equal((6 * WidgetGallery.Pitch) - WidgetGallery.Gap, group.Tiles[1].Tile.W);
        Assert.Equal(7, group.Tiles[1].Tile.H);

        // Down too, into tiles nothing took, by half its own span again and never past the sheet's foot.
        var down = WidgetGallery.Lay(Category("f47x15", "w47x7"), Wide, Size);
        Assert.Equal(15, down.Rows);
        Assert.Equal((3 * WidgetGallery.PitchTall) - WidgetGallery.Gap, down.Tiles[1].Tile.H);
        var foot = WidgetGallery.Lay(Category("f47x7", "w47x7"), Wide, Size);
        Assert.Equal(7, foot.Rows);
        Assert.Equal(7, foot.Tiles[1].Tile.H);
    }

    [Fact]
    public void S14912_a_piece_wider_than_the_sheet_is_held_to_it()
    {
        var sheet = WidgetGallery.Lay(Category("w200x10"), Wide, Size);

        Assert.Equal(Wide, sheet.Tiles[0].Tile.W);
        AssertTidy(sheet, Wide);
    }

    [Fact]
    public void S14912_the_same_input_lays_the_same_sheet_and_a_category_of_nothing_is_empty()
    {
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
    public void S14913_a_sheet_taller_than_the_view_scrolls_by_whole_cells_between_its_ends()
    {
        Assert.Equal(0, WidgetGallery.MaxScroll(60, 71));
        Assert.Equal(49, WidgetGallery.MaxScroll(120, 71));

        Assert.Equal(WidgetGallery.Step, WidgetGallery.Scroll(0, WidgetGallery.Step, 49));
        Assert.Equal(49, WidgetGallery.Scroll(48, WidgetGallery.Step, 49));
        Assert.Equal(0, WidgetGallery.Scroll(3, -WidgetGallery.Step, 49));
        Assert.Equal(0, WidgetGallery.Scroll(5, 5, 0));

        // A step is two tiles; a page is the view less a step, in whole steps, never less than one.
        Assert.Equal(2 * WidgetGallery.PitchTall, WidgetGallery.Step);
        Assert.Equal(7 * WidgetGallery.Step, WidgetGallery.PageRows(71));
        Assert.Equal(WidgetGallery.Step, WidgetGallery.PageRows(10));
    }

    [Fact]
    public void S14911_the_gallery_is_a_window_in_the_room_under_the_bar_not_the_whole_work_area()
    {
        var work = new DipRect(0, 0, 1920, 1032);
        var frame = WidgetGallery.Frame(work, barBottom: 56, allowed: work, pitchX: 10, pitchY: 10);
        var g = frame.Window;

        // Four fifths of the work area each way, in the middle across, under the bar.
        Assert.Equal(1920 * ShellMetrics.WidgetGallery.Share, g.Width, 6);
        Assert.Equal(1032 * ShellMetrics.WidgetGallery.Share, g.Height, 6);
        Assert.Equal((1920 - g.Width) / 2, g.X, 6);
        Assert.True(g.Y >= 56 + ShellMetrics.Flyout.Gap);
        Assert.True(g.Y + g.Height <= 1032 - ShellMetrics.WidgetGallery.Margin + 1e-6);

        // The rail, the well under its title line and the panel: side by side inside the window, none over another.
        static bool Inside(DipRect outer, DipRect inner)
            => inner.X >= outer.X - 1e-6 && inner.Y >= outer.Y - 1e-6 && inner.X + inner.Width <= outer.X + outer.Width + 1e-6 && inner.Y + inner.Height <= outer.Y + outer.Height + 1e-6;
        Assert.All(new[] { frame.Rail, frame.Head, frame.Well, frame.Panel }, part => Assert.True(Inside(g, part)));
        Assert.Equal(ShellMetrics.WidgetGallery.Rail, frame.Rail.Width);
        Assert.Equal(ShellMetrics.WidgetGallery.Panel, frame.Panel.Width);
        Assert.True(frame.Rail.X + frame.Rail.Width < frame.Well.X);
        Assert.True(frame.Well.X + frame.Well.Width < frame.Panel.X);
        Assert.Equal(frame.Head.Y + frame.Head.Height, frame.Well.Y, 6);

        // The cards' view: whole cells inside the well, whole tiles wide.
        var view = frame.View;
        Assert.True(view.X * 10 >= frame.Well.X && view.Right * 10 <= frame.Well.X + frame.Well.Width);
        Assert.True(view.Y * 10 >= frame.Well.Y && view.Bottom * 10 <= frame.Well.Y + frame.Well.Height);
        Assert.Equal(WidgetGallery.Width(view.W), view.W);
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

        // A 1280 by 720 display (1920 by 1080 at 150 %): the window takes the least it may, and the well holds eight tiles.
        var laptop = new DipRect(0, 0, 1280, 688);
        var held = WidgetGallery.Frame(laptop, barBottom: 56, allowed: laptop, pitchX: 10, pitchY: 10);
        Assert.Equal(ShellMetrics.WidgetGallery.MinWidth, held.Window.Width);
        Assert.True(WidgetGallery.Columns(held.View.W) >= 8);

        // A part of the display new geometry may not use (a taskbar's side) cuts the view, not the window.
        var work = new DipRect(0, 0, 1920, 1032);
        var cut = WidgetGallery.Frame(work, barBottom: 56, allowed: new DipRect(0, 0, 1920, 600), pitchX: 10, pitchY: 10);
        Assert.True(cut.View.Bottom * 10 <= 600);
        Assert.Equal(WidgetGallery.Frame(work, 56, work, 10, 10).Window, cut.Window);
    }
}
