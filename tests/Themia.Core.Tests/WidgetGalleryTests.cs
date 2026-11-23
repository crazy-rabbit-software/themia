using Themia.App.Shell;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14911]..[S-14913] [S-16400]..[S-16408] [S-16550]..[S-16555] The widget gallery's arrangement: a category as a column
/// bento of whole cells, one cell between two pieces and no hole in a column, each piece's title counted; Quick settings in
/// sections under headings; its scrolling, its keys, its window under the bar.
/// </summary>
public sealed class WidgetGalleryTests
{
    private const int Wide = 60;

    private static GalleryPiece Piece(string name) => new("kind:" + name, new WidgetId(name), null);

    private static GalleryCategory Category(params string[] names)
        => new(AddFolderId.Weather, LocalText.Fixed("Weather"), Glyphs.Apps, [.. names.Select(Piece)]);

    /// <summary>
    /// Sizes by the piece's name: "w30x20" is 30 × 20 cells and stretches both ways (least 3 each way); "m30x20" too, up to
    /// twice that; "f30x20" keeps that size; "g30x20" may be wider (to every third width) but neither smaller nor taller (a ready-made group); a letter
    /// after the height tells two of a size apart.
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
            'g' => new PieceSize(w, h, w, h, true, false) { Widths = [.. Enumerable.Range(0, 40).Select(i => w + (3 * i))] },
            'm' => new PieceSize(w, h, 3, 3, true, true) { MaxW = 2 * w, MaxH = 2 * h },
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

    /// <summary>Every column's cards one cell apart from its top to its foot: rows of one piece as wide as the column, or of several side by side at one height filling it.</summary>
    private static void AssertColumns(GallerySheet sheet, int width)
    {
        Assert.Equal(0, WidgetGallery.Holes(sheet));
        foreach (var col in sheet.Columns)
        {
            var inside = sheet.Tiles.Where(t => col.Holds(t.Box)).ToList();
            foreach (var row in inside.GroupBy(t => t.Box.Y))
            {
                Assert.Single(row.Select(t => t.Box.H).Distinct());
                Assert.Equal(col.W, row.Sum(t => t.Box.W) + ((row.Count() - 1) * WidgetGallery.Gap));
            }
        }

        // Every piece stands in a column.
        Assert.All(sheet.Tiles, t => Assert.Contains(sheet.Columns, c => c.Holds(t.Box)));
        var top = sheet.Columns.Where(c => c.Y == sheet.Columns.Min(t => t.Y)).ToList();
        Assert.True(top.Sum(c => c.W) + ((top.Count - 1) * WidgetGallery.Gap) <= width);
    }

    [Fact]
    public void S16550_a_category_is_columns_first_every_piece_as_wide_as_its_column_and_no_hole()
    {
        var category = Category("w30x20", "w40x12", "w24x24", "w60x30", "w20x8", "w20x8b", "w10x10", "w30x10", "w34x26", "w16x16");

        var sheet = WidgetGallery.Lay(category, Wide, Size);

        Assert.Equal(category.Pieces, sheet.Tiles.Select(t => t.Piece));
        AssertTidy(sheet, Wide);
        AssertColumns(sheet, Wide);
        Assert.Equal(sheet.Columns.Max(c => c.Bottom), sheet.Rows);
        Assert.Equal(AddFolderId.Weather, sheet.Category);

        // The columns fill the width, and pieces that stretch bring their feet level.
        var top = sheet.Columns.Where(c => c.Y == 0).ToList();
        Assert.Equal(Wide, top.Sum(c => c.W) + ((top.Count - 1) * WidgetGallery.Gap));
        Assert.True(sheet.Columns.Count >= 2, $"{sheet.Columns.Count} columns");
        Assert.Equal(0, WidgetGallery.Ragged(sheet));
    }

    [Fact]
    public void S16550_the_columns_need_not_be_one_width()
    {
        // Two wide pieces and four narrow ones: a wide column and narrow ones, not one width for all.
        var sheet = WidgetGallery.Lay(Category("f40x20", "f40x20b", "w14x12", "w14x12b", "w14x12c", "w14x12d"), 70, Size);

        AssertColumns(sheet, 70);
        Assert.True(sheet.Columns.Select(c => c.W).Distinct().Count() > 1, string.Join(", ", sheet.Columns));
        Assert.Contains(sheet.Columns, c => c.W == 40);
    }

    [Fact]
    public void S16551_a_piece_is_as_tall_as_its_sizing_makes_it_at_its_columns_width()
    {
        // Alone on a sheet of 30, a piece of 20 by 10 is as wide as its column and its height scales with it: 30 by 15.
        var sheet = WidgetGallery.Lay(Category("w20x10"), 30, Size);
        Assert.Equal(new CellBox(0, 0, 30, 15), sheet.Tiles[0].Box);

        // Never past twice its natural: a sheet wider than that holds it at its most.
        var held = WidgetGallery.Lay(Category("m20x10"), 60, Size);
        Assert.Equal(new CellBox(0, 0, 40, 20), held.Tiles[0].Box);
    }

    [Fact]
    public void S16551_pieces_that_keep_their_width_stand_side_by_side_at_one_height_filling_their_column()
    {
        // Three of 10 by 10 that keep their size on a sheet of 32: one row of them, one cell apart, filling it.
        var sheet = WidgetGallery.Lay(Category("f10x10", "f10x10b", "f10x10c"), 32, Size);
        Assert.Equal([new CellBox(0, 0, 10, 10), new CellBox(11, 0, 10, 10), new CellBox(22, 0, 10, 10)], sheet.Tiles.Select(t => t.Box));
        AssertColumns(sheet, 32);

        // Small pieces that stretch share a column wider than they may be alone, at one height.
        var pair = WidgetGallery.Lay(Category("m12x12", "m12x12b"), 30, Size);
        AssertColumns(pair, 30);
        Assert.Equal(pair.Tiles[0].Box.Y, pair.Tiles[1].Box.Y);
        Assert.Equal(pair.Tiles[0].Box.H, pair.Tiles[1].Box.H);
    }

    [Fact]
    public void S16553_a_ready_made_group_takes_its_columns_width_where_its_step_lands_and_keeps_its_height()
    {
        // A group 20 by 7 that may be every third width more: in a column of 29 it is 29 wide, still 7 tall.
        var sheet = WidgetGallery.Lay(Category("g20x7", "f29x15"), 29, Size);
        Assert.Equal((0, 29, 7), (sheet.Tiles[0].Box.X, sheet.Tiles[0].Box.W, sheet.Tiles[0].Box.H));
        AssertColumns(sheet, 29);
    }

    [Fact]
    public void S16552_the_holes_are_the_cells_of_a_column_no_piece_and_no_gap_takes()
    {
        GalleryTile Tile(int x, int y, int w, int h) => new(Piece("w1x1"), new CellBox(x, y, w, h));

        // A column of 10 from row 0 to 21: a card 10 by 10, its gap, a card 8 by 10 leaving 2 cells beside it 10 deep.
        var sheet = new GallerySheet(AddFolderId.Weather, LocalText.Fixed("x"), 21, [Tile(0, 0, 10, 10), Tile(0, 11, 8, 10)]) { Columns = [new CellBox(0, 0, 10, 21)] };
        Assert.Equal(2 * 10, WidgetGallery.Holes(sheet));

        // Two columns whose feet differ by 5: ragged 5, no hole.
        var two = new GallerySheet(AddFolderId.Weather, LocalText.Fixed("x"), 20, [Tile(0, 0, 10, 20), Tile(11, 0, 10, 15)])
        {
            Columns = [new CellBox(0, 0, 10, 20), new CellBox(11, 0, 10, 15)],
        };
        Assert.Equal(0, WidgetGallery.Holes(two));
        Assert.Equal(5, WidgetGallery.Ragged(two));

        // A column that goes on as two narrower ones under its last card: its foot is theirs, not the row it ends at.
        var divided = new GallerySheet(AddFolderId.Weather, LocalText.Fixed("x"), 31, [Tile(0, 0, 30, 10), Tile(0, 11, 14, 20), Tile(15, 11, 15, 15), Tile(31, 0, 20, 31)])
        {
            Columns = [new CellBox(0, 0, 30, 10), new CellBox(0, 11, 14, 20), new CellBox(15, 11, 15, 15), new CellBox(31, 0, 20, 31)],
        };
        Assert.Equal(0, WidgetGallery.Holes(divided));
        Assert.Equal(5, WidgetGallery.Ragged(divided));
        Assert.Equal(0, WidgetGallery.Holes(WidgetGallery.Lay(Category(), 30, Size)));
    }

    [Fact]
    public void S16550_a_piece_wider_than_the_sheet_is_held_to_it_and_the_same_input_lays_the_same_sheet()
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

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void S16721_each_title_takes_rows_over_its_card_inside_its_tile_and_the_columns_count_it(int title)
    {
        var category = Category("w20x10", "w24x12", "w14x8", "f10x6", "f10x6b", "m16x9");

        var sheet = WidgetGallery.Lay(category, Wide, Size, title);

        Assert.Equal(title, sheet.Title);
        AssertTidy(sheet, Wide);
        Assert.Equal(0, WidgetGallery.Holes(sheet));
        foreach (var tile in sheet.Tiles)
        {
            var card = sheet.CardOf(tile);
            Assert.Equal(tile.Box.Y + title, card.Y);
            Assert.Equal(tile.Box.Bottom, card.Bottom);
            Assert.Equal(tile.Box.W, card.W);

            // A card that keeps its size keeps it under its title.
            if (tile.Piece.Kind!.Value.Value[0] == 'f')
            {
                Assert.Equal(6, card.H);
            }
        }
    }

    [Fact]
    public void S16725_sections_stand_under_their_headings_in_order_never_interleaved()
    {
        var toggles = new GallerySection(LocalText.Fixed("Tile"), [.. new[] { "f6x6", "f4x4", "f6x6b", "f4x4b", "f6x6c" }.Select(Piece)]);
        var power = new GallerySection(LocalText.Fixed("Power"), [.. new[] { "f6x6d", "f4x4d" }.Select(Piece)]);
        var category = Category() with { Sections = [toggles, power], Pieces = [.. toggles.Pieces, .. power.Pieces] };

        var sheet = WidgetGallery.Lay(category, 20, Size, title: 3, heading: 2);

        Assert.Equal(0, sheet.Title);
        Assert.Equal(["Tile", "Power"], sheet.Headings.Select(h => h.Text.Text()));
        Assert.Equal([.. toggles.Pieces, .. power.Pieces], sheet.Tiles.Select(t => t.Piece));
        AssertTidy(sheet, 20);
        var (first, second) = (sheet.Headings[0].Box, sheet.Headings[1].Box);
        Assert.Equal((0, 2, 20), (first.Y, first.H, first.W));
        Assert.All(sheet.Tiles.Take(5), t => Assert.True(t.Box.Y >= first.Bottom && t.Box.Bottom < second.Y, $"{t.Piece.Key} at {t.Box}"));
        Assert.All(sheet.Tiles.Skip(5), t => Assert.True(t.Box.Y >= second.Bottom, $"{t.Piece.Key} at {t.Box}"));

        // In their order, left to right, a new row where the width ends; each at the size it asks.
        Assert.Equal([0, 7, 12, 0, 5], sheet.Tiles.Take(5).Select(t => t.Box.X));
        Assert.Equal(sheet.Tiles[0].Box.Y, sheet.Tiles[2].Box.Y);
        Assert.Equal(sheet.Tiles[0].Box.Y + 7, sheet.Tiles[3].Box.Y);
        Assert.Equal(new CellBox(0, sheet.Tiles[0].Box.Y, 6, 6), sheet.Tiles[0].Box);
        Assert.Equal(sheet.Tiles.Max(t => t.Box.Bottom), sheet.Rows);
    }

    [Theory]
    [InlineData(1504, 900, GalleryHint.Long, 280)]
    [InlineData(1504, 1100, GalleryHint.Short, 280)]
    [InlineData(1504, 1140, GalleryHint.Short, 262)]
    [InlineData(1504, 1250, GalleryHint.None, 280)]
    [InlineData(944, 1100, GalleryHint.None, 236)]
    public void S16566_the_tabs_take_the_line_first_the_hint_only_what_is_left(double line, double tabs, GalleryHint shown, double search)
    {
        // The search 280 (a quarter of a narrow line at the most), 200 at the least; 16 between; the whole hint 230, the short one 70.
        var (hint, width) = WidgetGallery.Head(line, tabs, search: Math.Min(280, line / 4), searchLeast: 200, hintLong: 230, hintShort: 70, gap: 16);

        Assert.Equal((shown, search), (hint, width));
        var used = tabs + 16 + width + (hint switch { GalleryHint.Long => 230 + 16, GalleryHint.Short => 70 + 16, _ => 0 });
        Assert.True(hint == GalleryHint.None || used <= line, $"{used} of {line}");
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

        // [S-16559] Little room over and under the tabs: a tab's height, 8 under the window's top, 4 over the well.
        Assert.Equal(ShellMetrics.Page.Tabs, frame.Head.Height);
        Assert.Equal(g.Y + ShellMetrics.WidgetGallery.HeadInset, frame.Head.Y, 6);
        Assert.Equal(frame.Head.Y + frame.Head.Height + ShellMetrics.WidgetGallery.HeadGap, frame.Well.Y, 6);

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
