using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-16740]..[S-16746] [S-17160]..[S-17163] [S-17220]..[S-17224] The gallery's curated tabs (owner, 2026-11-23 and
/// 2026-11-24): columns of fixed widths in blocks two blocks apart on one line (a sheet wider than the view scrolls across),
/// sections in them, every widget at its place in its column (moved as on the desktop, never onto another), touching cards a
/// group; one file a tab, round-tripped, its places repaired where read.
/// </summary>
public sealed class CuratedBentoTests
{
    private static GalleryPiece Piece(CuratedWidget w) => new("kind:" + w.Pick, null, null);

    private static CuratedWidget W(string pick, int w, int h) => new() { Pick = pick, W = w, H = h };

    private static CuratedWidget At(string pick, int x, int y, int w, int h) => new() { Pick = pick, X = x, Y = y, W = w, H = h };

    private static IReadOnlyList<CellBox> Pack(IReadOnlyList<(int W, int H)> sizes, int width, int title = 0)
        => CuratedBento.Places([.. sizes.Select((z, i) => W("w" + i, z.W, z.H))], width, title, CuratedFit.AsStored, out _);

    private static CuratedLayout Layout(params CuratedColumn[] columns) => new() { Titles = false, Columns = columns };

    private static CuratedColumn Col(int width, params CuratedSection[] sections) => new() { Width = width, Sections = sections };

    private static CuratedSection Sec(string? title, params CuratedWidget[] widgets) => new() { Title = title, Widgets = widgets };

    private static GallerySheet Lay(CuratedLayout layout, int title = 0, bool editing = false)
        => CuratedBento.Lay(AddFolderId.Time, LocalText.Fixed("Time"), layout, CuratedFit.AsStored, title, 2, editing, Piece);

    /// <summary>
    /// [S-16743] [S-17222] A widget with no place goes where the packer put every widget until 2026-11-24 ("packed together as
    /// tightly as possible in direction of reading"): the highest free spot of the column that holds it, the left-most of those,
    /// one cell from the others; a short one fills the room beside a tall one.
    /// </summary>
    [Fact]
    public void S16743_a_widget_without_a_place_goes_to_the_highest_free_spot_left_first()
    {
        var boxes = Pack([(10, 20), (10, 8), (10, 8), (21, 5), (30, 4)], 21);

        Assert.Equal(new CellBox(0, 0, 10, 20), boxes[0]);
        Assert.Equal(new CellBox(11, 0, 10, 8), boxes[1]);
        Assert.Equal(new CellBox(11, 9, 10, 8), boxes[2]);   // under the short one, beside the tall one
        Assert.Equal(new CellBox(0, 21, 21, 5), boxes[3]);   // as wide as the column: under everything
        Assert.Equal(new CellBox(0, 27, 21, 4), boxes[4]);   // never wider than its column
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var apart = new CellBox(boxes[i].X - 1, boxes[i].Y - 1, boxes[i].W + 2, boxes[i].H + 2);
                Assert.False(apart.Overlaps(boxes[j]), $"{boxes[i]} and {boxes[j]} touch");
            }
        }

        // Tight: no widget can move a cell up and stay clear of the others.
        for (var i = 0; i < boxes.Count; i++)
        {
            var up = boxes[i] with { Y = boxes[i].Y - 1 };
            Assert.True(up.Y < 0 || boxes.Where((_, j) => j != i).Any(b => new CellBox(b.X - 1, b.Y - 1, b.W + 2, b.H + 2).Overlaps(up)), $"{boxes[i]} could stand higher");
        }

        // A later widget takes a hole an earlier one left, when it is the highest spot that holds it.
        var hole = Pack([(10, 4), (21, 4), (10, 2)], 21);
        Assert.Equal(new CellBox(11, 0, 10, 2), hole[2]);

        // With titles over the cards, a card's place counts from its title's foot: under another, its title a cell clear.
        Assert.Equal([new CellBox(0, 0, 10, 5), new CellBox(0, 8, 10, 5)], Pack([(21, 5), (21, 5)], 21, title: 2).Select(b => b with { W = 10 }));
    }

    /// <summary>
    /// [S-17220] [S-17221] The owner's "rearrange the widgets like i am on the desktop, just in the column that it is placed
    /// though": every card exactly at its place (from its column's left and its section's room, under its title), held inside its
    /// column (moved left as far as it takes), a section as tall as its widgets reach; nothing packs them.
    /// </summary>
    [Fact]
    public void S17220_every_widget_stands_at_its_place_in_its_column()
    {
        var layout = new CuratedLayout
        {
            Titles = true,
            Columns = [Col(30, Sec(null, At("a", 4, 7, 10, 6), At("b", 25, 0, 10, 6)), Sec("Second", At("c", 0, 20, 8, 4))), Col(20, Sec(null, At("d", 3, 2, 5, 5)))],
        };
        var sheet = Lay(layout, title: 2);

        Assert.Equal(new CellBox(4, 9, 10, 6), sheet.CardOf(sheet.Tiles[0]));      // its title's 2 rows over it, from the top
        Assert.Equal(new CellBox(4, 7, 10, 8), sheet.Tiles[0].Box);
        Assert.Equal(new CellBox(20, 2, 10, 6), sheet.CardOf(sheet.Tiles[1]));     // past the column's right edge: held to it
        Assert.Equal(new CellBox(0, 0, 30, 15), sheet.Areas[0].Box);               // as tall as its widgets reach
        Assert.Equal(2, sheet.Areas[0].Room);
        Assert.Equal(new CellBox(0, 16, 30, 2), sheet.Headings.Single().Box);       // the next section a cell under it
        Assert.Equal(new CellBox(0, 40, 8, 4), sheet.CardOf(sheet.Tiles[2]));      // its room under its heading and a title
        Assert.Equal(new CellBox(35, 4, 5, 5), sheet.CardOf(sheet.Tiles[3]));
        Assert.Equal(44, sheet.Rows);
        Assert.Empty(sheet.Groups);

        // Where a card's top lands in a column: the section holding that row, its place from that section's room.
        Assert.Equal((new CuratedSlot(0, 0), 5), CuratedBento.Landing(sheet, 0, 7));
        Assert.Equal((new CuratedSlot(0, 1), 0), CuratedBento.Landing(sheet, 0, 17));
        Assert.Equal((new CuratedSlot(0, 1), 30), CuratedBento.Landing(sheet, 0, 50));
        Assert.Null(CuratedBento.Landing(sheet, 5, 0));
    }

    /// <summary>
    /// [S-17221] [S-17222] A widget moved in the editor lands where it is let go, inside its column (into another section of it
    /// too); never onto another widget (its card, or its title over another's card), never into another column: refused, the
    /// layout unchanged. Set touching, it is allowed (that makes a group). A resize onto another is refused too.
    /// </summary>
    [Fact]
    public void S17221_a_widget_moves_inside_its_column_never_onto_another()
    {
        var layout = new CuratedLayout
        {
            Titles = true,
            Columns = [Col(30, Sec("A", At("a", 0, 0, 10, 6), At("b", 15, 0, 10, 6)), Sec("B", At("c", 0, 0, 10, 4))), Col(20, Sec(null, At("d", 0, 0, 5, 5)))],
        };
        var fit = CuratedFit.AsStored;

        var moved = CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 0, 0), new CuratedSlot(0, 0), 3, 20, fit, 2);
        Assert.Equal((3, 20), (moved.Columns[0].Sections[0].Widgets[0].X, moved.Columns[0].Sections[0].Widgets[0].Y));
        var held = CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 0, 0), new CuratedSlot(0, 0), 28, 10, fit, 2);
        Assert.Equal((20, 10), (held.Columns[0].Sections[0].Widgets[0].X, held.Columns[0].Sections[0].Widgets[0].Y));   // held inside
        var up = CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 1, 0), new CuratedSlot(0, 1), 5, -4, fit, 2);
        Assert.Equal((5, 0), (up.Columns[0].Sections[1].Widgets[0].X, up.Columns[0].Sections[1].Widgets[0].Y));       // never above its room

        var across = CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 0, 1), new CuratedSlot(0, 1), 12, 0, fit, 2);
        Assert.Equal(["a"], across.Columns[0].Sections[0].Widgets.Select(w => w.Pick));
        Assert.Equal(["c", "b"], across.Columns[0].Sections[1].Widgets.Select(w => w.Pick));
        Assert.Equal((12, 0), (across.Columns[0].Sections[1].Widgets[1].X, across.Columns[0].Sections[1].Widgets[1].Y));

        Assert.Same(layout, CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 0, 0), new CuratedSlot(1, 0), 0, 10, fit, 2));   // another column
        Assert.Same(layout, CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 0, 0), new CuratedSlot(0, 0), 12, 2, fit, 2));   // on b's card
        Assert.Same(layout, CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 0, 1), new CuratedSlot(0, 0), 0, 7, fit, 2));    // its title on a's card
        var touching = CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 0, 1), new CuratedSlot(0, 0), 0, 6, fit, 2);
        Assert.Equal((0, 6), (touching.Columns[0].Sections[0].Widgets[1].X, touching.Columns[0].Sections[0].Widgets[1].Y));   // under a, touching: allowed

        Assert.Same(layout, CuratedEdits.ResizeWidget(layout, new CuratedSlot(0, 0, 0), 16, 6, fit, 2));
        var grown = CuratedEdits.ResizeWidget(layout, new CuratedSlot(0, 0, 0), 15, 9, fit, 2);
        Assert.Equal((15, 9), (grown.Columns[0].Sections[0].Widgets[0].W, grown.Columns[0].Sections[0].Widgets[0].H));
        Assert.Equal(15, CuratedEdits.ResizeWidget(layout, new CuratedSlot(0, 0, 1), 40, 6, fit, 2).Columns[0].Sections[0].Widgets[1].W);   // no further right than its column

        // Added, a widget takes the first free spot of its section: a cell (and its title) clear of the others.
        var added = CuratedEdits.AddWidget(layout, new CuratedSlot(0, 1), W("e", 10, 4), fit, 2);
        Assert.Equal((11, 0), (added.Columns[0].Sections[1].Widgets[1].X, added.Columns[0].Sections[1].Widgets[1].Y));
    }

    /// <summary>
    /// [S-17223] The owner's "create "groups" by connecting the widgets in the editor together without space": cards whose cells
    /// meet side to side are one group with all they touch; a cell between keeps them apart, a corner alone does not join. A
    /// laid group knows its tiles (the top-most, then left-most first), the box round its cards and its one title over it.
    /// </summary>
    [Fact]
    public void S17223_cards_that_touch_are_one_group()
    {
        var cards = new[]
        {
            new CellBox(0, 0, 10, 5),   // 0
            new CellBox(10, 2, 4, 4),   // 1 beside 0
            new CellBox(2, 5, 3, 3),    // 2 under 0
            new CellBox(14, 6, 2, 2),   // 3 a corner of 1 alone
            new CellBox(0, 9, 5, 5),    // 4 a cell under 2
            new CellBox(5, 9, 5, 5),    // 5 beside 4
        };
        Assert.Equal([[0, 1, 2], [3], [4, 5]], CuratedBento.Groups(cards));
        Assert.True(new CellBox(0, 0, 2, 2).Meets(new CellBox(2, 1, 2, 2)));
        Assert.False(new CellBox(0, 0, 2, 2).Meets(new CellBox(2, 2, 2, 2)));
        Assert.False(new CellBox(0, 0, 2, 2).Meets(new CellBox(1, 1, 2, 2)));   // overlapping is no meeting

        // A clash: two cards on one cell, or a title over another's card; touching never is.
        Assert.False(CuratedBento.Clashes(cards, 0));
        Assert.True(CuratedBento.Clashes([new CellBox(0, 0, 4, 4), new CellBox(3, 3, 4, 4)], 0));
        Assert.True(CuratedBento.Clashes([new CellBox(0, 0, 4, 4), new CellBox(0, 5, 4, 4)], 2));
        Assert.False(CuratedBento.Clashes([new CellBox(0, 0, 4, 4), new CellBox(0, 4, 4, 4)], 2));

        var layout = new CuratedLayout { Titles = true, Columns = [Col(30, Sec(null, At("b", 10, 3, 4, 4), At("a", 0, 0, 10, 5), At("c", 0, 12, 5, 5)))] };
        var sheet = Lay(layout, title: 2);
        var group = Assert.Single(sheet.Groups);
        Assert.Equal([1, 0], group.Tiles);                                  // a first: top-most
        Assert.Equal(new CellBox(0, 2, 14, 7), group.Box);
        Assert.Equal(new CellBox(0, 0, 14, 2), group.Title);
        Assert.Equal([0, 0, null], sheet.Tiles.Select(t => t.Group));
    }

    /// <summary>
    /// [S-17160] The owner's "the columns should not stretch, they should have a fixed block width. between columns there should
    /// be a two block space": each column exactly its blocks from the sheet's left, two blocks between two, a widget wider than
    /// its column held to it; the sheet as wide as its columns.
    /// </summary>
    [Fact]
    public void S17160_columns_stand_at_their_blocks_two_apart_and_never_stretch()
    {
        var sheet = Lay(Layout(Col(30, Sec(null, W("a", 40, 6))), Col(20, Sec(null, W("b", 12, 6), W("c", 7, 6))), Col(25, Sec(null, W("d", 25, 6)))));

        Assert.Equal([new CellBox(0, 0, 30, 6), new CellBox(32, 0, 20, 6), new CellBox(54, 0, 25, 6)], sheet.Columns);
        Assert.Equal(new CellBox(0, 0, 30, 6), sheet.Tiles[0].Box);   // a widget wider than its column held to it
        Assert.Equal(new CellBox(45, 0, 7, 6), sheet.Tiles[2].Box);
        Assert.Equal(79, sheet.Cols);
        Assert.Equal(6, sheet.Rows);
    }

    /// <summary>
    /// [S-17161] The owner's "when the columns cant fit onto one screen anymore scrolling in both directions should be possible.
    /// dont scale or stretch anything for it": the columns stay on one line at their widths, whatever the view; the sheet is
    /// wider than a narrow view and scrolls across, as little as it takes to bring a widget whole into sight.
    /// </summary>
    [Fact]
    public void S17161_columns_never_stack_or_narrow_a_wider_sheet_scrolls_across()
    {
        var layout = Layout(
            Col(30, Sec("A", W("a1", 20, 30))),
            Col(20, Sec("B", W("b1", 20, 10)), Sec("B2", W("b2", 20, 4))),
            Col(25, Sec("C", W("c1", 20, 8))));

        var sheet = Lay(layout);
        Assert.Equal([new CellBox(0, 0, 30, 32), new CellBox(32, 0, 20, 19), new CellBox(54, 0, 25, 10)], sheet.Columns);
        Assert.Equal(new CellBox(54, 2, 20, 8), sheet.Tiles.Single(t => t.Piece.Key == "kind:c1").Box);
        Assert.Contains(sheet.Headings, h => h.Text.Text() == "C" && h.Box == new CellBox(54, 0, 25, 2));
        Assert.Equal((79, 32), (sheet.Cols, sheet.Rows));

        // In a view 40 cells wide it scrolls 39 across; the third column's widget comes into sight by as little as it takes.
        Assert.Equal(39, WidgetGallery.MaxScroll(sheet.Cols, 40));
        Assert.Equal(0, WidgetGallery.MaxScroll(sheet.Cols, 120));
        var c1 = sheet.Tiles.Single(t => t.Piece.Key == "kind:c1").Box;
        Assert.Equal(34, WidgetGallery.RevealAcross(0, c1, 40, 39));
        Assert.Equal(20, WidgetGallery.RevealAcross(20, sheet.Tiles[1].Box, 40, 39));   // already whole in sight: kept
        Assert.Equal(0, WidgetGallery.RevealAcross(30, sheet.Tiles[0].Box, 40, 39));
    }

    /// <summary>[S-16743] [S-16721] A tab's sections down a column one cell apart, a titled one under its heading, every card under its title rows.</summary>
    [Fact]
    public void S16743_sections_stand_down_a_column_titled_under_a_heading_cards_under_their_titles()
    {
        var layout = new CuratedLayout { Titles = true, Columns = [Col(30, Sec(null, W("a", 10, 6)), Sec("Second", W("b", 10, 6), W("c", 10, 6)))] };
        var sheet = Lay(layout, title: 2);

        Assert.Equal(2, sheet.Title);
        Assert.Equal(new CellBox(0, 0, 10, 8), sheet.Tiles[0].Box);                       // no heading: the widget from the top
        Assert.Equal(new GalleryHeading(LocalText.Fixed("Second"), new CellBox(0, 9, 30, 2)) { Slot = new CuratedSlot(0, 1) }, sheet.Headings.Single());
        Assert.Equal(new CellBox(0, 11, 10, 8), sheet.Tiles[1].Box);
        Assert.Equal(new CellBox(11, 11, 10, 8), sheet.Tiles[2].Box);
        Assert.Equal(new CellBox(11, 13, 10, 6), sheet.CardOf(sheet.Tiles[2]));
        Assert.Equal(19, sheet.Rows);
        Assert.Equal([new CellBox(0, 0, 30, 8), new CellBox(0, 9, 30, 10)], sheet.Areas.Select(a => a.Box));

        // Editing: the column's tools' band first, every section's heading line, an empty section room to drop into.
        var edit = Lay(CuratedEdits.AddSection(layout, 0), title: 2, editing: true);
        Assert.Equal(3, edit.Headings.Count);
        Assert.Equal(3, edit.Headings[0].Box.Y);
        Assert.Equal(CuratedBento.EmptyRows + 2, edit.Areas[2].Box.H);
    }

    /// <summary>[S-16746] [S-17162] A layout file read back is the layout written: titles, widths in blocks, sections, picks, styles, sizes and a config fragment.</summary>
    [Fact]
    public void S16746_a_layout_file_round_trips()
    {
        var layout = new CuratedLayout
        {
            Titles = false,
            Columns =
            [
                Col(26, Sec("Clocks", At("clock", 0, 0, 20, 20) with { Style = "analog" }, At("quickToggle/wifi", 21, 3, 5, 9))),
                Col(40, Sec(null, W("rss", 30, 40) with { Config = new JsonObject { ["rows"] = 6, ["feeds"] = new JsonArray("a", "b") } })),
            ],
        };

        var text = CuratedLayouts.Write(layout);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.Contains("\"width\": 26,", text, StringComparison.Ordinal);
        Assert.DoesNotContain("share", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"title\": null", text, StringComparison.Ordinal);
        Assert.Contains("\"x\": 21,\n              \"y\": 3,\n              \"w\": 5,", text, StringComparison.Ordinal);

        var back = CuratedLayouts.Read(text);
        Assert.Equal(text, CuratedLayouts.Write(back));
        Assert.False(back.Titles);
        Assert.Equal([26, 40], back.Columns.Select(c => c.Width));
        Assert.Equal("analog", back.Columns[0].Sections[0].Widgets[0].Style);
        Assert.Equal(6, (int)back.Columns[1].Sections[0].Widgets[0].Config!["rows"]!);
        Assert.Equal((21, 3), (back.Columns[0].Sections[0].Widgets[1].X, back.Columns[0].Sections[0].Widgets[1].Y));
        Assert.Null(back.Columns[1].Sections[0].Widgets[0].X);
        Assert.Throws<JsonException>(() => CuratedLayouts.Read("[1, 2]"));
        Assert.Equal("calendarMail.json", CuratedLayouts.FileName(AddFolderId.CalendarMail));
    }

    /// <summary>
    /// [S-16746] [S-17162] [S-17222] Where read, a widget the build cannot show is dropped (loudly), a size held to its range, a
    /// missing or bad width made the column's widest widget's, a place missing or on another widget the first free spot, one past
    /// its column moved into it.
    /// </summary>
    [Fact]
    public void S16746_a_layout_is_checked_where_it_is_read()
    {
        var layout = Layout(Col(-2, Sec("  ", W("clock", 0, 999), W("gone", 5, 5))));
        var checkedLayout = CuratedLayouts.Checked(layout, "time.json", 0, w => w.Pick == "gone" ? "no such widget" : null);

        var widgets = checkedLayout.Columns[0].Sections[0].Widgets;
        Assert.Equal(1, checkedLayout.Columns[0].Width);   // its widest widget, the dropped one not counted
        Assert.Null(checkedLayout.Columns[0].Sections[0].Title);
        Assert.Equal(["clock"], widgets.Select(w => w.Pick));
        Assert.Equal((1, CuratedLayouts.MostCells), (widgets[0].W, widgets[0].H));

        // A width missing (a file without one), too large or fine; an empty column's missing width the new column's.
        var widths = CuratedLayouts.Checked(
            Layout(Col(0, Sec(null, W("a", 12, 5), W("b", 30, 5))), Col(CuratedLayouts.MostCells + 1, Sec(null, W("c", 9, 5))), Col(26, Sec(null, W("d", 40, 5))), Col(0)),
            "time.json",
            0,
            _ => null);
        Assert.Equal([30, 9, 26, CuratedBento.NewColumn], widths.Columns.Select(c => c.Width));
        var read = CuratedLayouts.Read("""{ "columns": [ { "sections": [ { "widgets": [ { "pick": "a", "w": 14, "h": 3 } ] } ] } ] }""");
        Assert.Equal(0, read.Columns[0].Width);
        Assert.Equal(14, CuratedLayouts.Checked(read, "time.json", 0, _ => null).Columns[0].Width);

        // Places: kept where clear; one missing, one on another and one under another's title (titles 2 rows) the first free
        // spot; one past its column moved left into it.
        var placed = CuratedLayouts.Checked(
            new CuratedLayout { Titles = true, Columns = [Col(20, Sec(null, At("a", 0, 0, 8, 4), W("b", 8, 4), At("c", 4, 2, 8, 4), At("d", 0, 5, 8, 4), At("e", 18, 20, 8, 4)))] },
            "time.json",
            2,
            _ => null).Columns[0].Sections[0].Widgets;
        Assert.Equal([(0, 0), (9, 0), (0, 7), (9, 7), (12, 20)], placed.Select(w => (w.X!.Value, w.Y!.Value)));
        var clear = new CuratedLayout { Titles = true, Columns = [Col(20, Sec(null, At("a", 0, 0, 8, 4), At("b", 8, 0, 8, 4)))] };
        Assert.Equal(CuratedLayouts.Write(clear), CuratedLayouts.Write(CuratedLayouts.Checked(clear, "time.json", 2, _ => null)));

        // Over this test's registry (no widget) every pick is one the build lacks.
        Assert.NotNull(CuratedLayouts.Problem(W("clock", 5, 5)));
        Assert.Empty(CuratedLayouts.Checked(layout, "time.json", 0).Columns[0].Sections[0].Widgets);
    }

    /// <summary>[S-16745] [S-17163] The editor's steps: a widget resized and removed; a column set to a width in blocks, added and removed; sections reordered.</summary>
    [Fact]
    public void S16745_the_editors_steps_change_the_layout()
    {
        var layout = Layout(Col(30, Sec("A", W("a", 5, 5), W("b", 5, 5), W("c", 5, 5))), Col(30, Sec("B", W("d", 5, 5))));

        var resized = CuratedEdits.ResizeWidget(layout, new CuratedSlot(0, 0, 1), 12, 0, CuratedFit.AsStored, 0);
        Assert.Equal((12, 1), (resized.Columns[0].Sections[0].Widgets[1].W, resized.Columns[0].Sections[0].Widgets[1].H));
        Assert.Equal(["a", "c"], CuratedEdits.RemoveWidget(layout, new CuratedSlot(0, 0, 1)).Columns[0].Sections[0].Widgets.Select(w => w.Pick));

        // A column set to a width exactly, the others kept: the one right of it moves, never narrows.
        var wider = CuratedEdits.Width(layout, 0, 26);
        Assert.Equal([26, 30], wider.Columns.Select(c => c.Width));
        Assert.Equal((1, CuratedLayouts.MostCells), (CuratedEdits.Width(layout, 1, 0).Columns[1].Width, CuratedEdits.Width(layout, 1, 9999).Columns[1].Width));
        Assert.Same(layout, CuratedEdits.Width(layout, 2, 26));
        Assert.Equal([(0, 26), (28, 30)], Lay(wider).Columns.Select(c => (c.X, c.W)));
        var three = CuratedEdits.AddColumn(layout);
        Assert.Equal(3, three.Columns.Count);
        Assert.Equal(CuratedBento.NewColumn, three.Columns[2].Width);
        Assert.Single(three.Columns[2].Sections);
        Assert.Single(CuratedEdits.RemoveColumn(CuratedEdits.RemoveColumn(layout, 0), 0).Columns);

        var sections = CuratedEdits.AddSection(layout, 0, " New ");
        Assert.Equal("New", sections.Columns[0].Sections[1].Title);
        var moved = CuratedEdits.MoveSection(sections, new CuratedSlot(0, 1), -1);
        Assert.Equal(["New", "A"], moved.Columns[0].Sections.Select(s => s.Title));
        Assert.Null(CuratedEdits.Rename(moved, new CuratedSlot(0, 0), "  ").Columns[0].Sections[0].Title);
        Assert.Equal(["A"], CuratedEdits.RemoveSection(moved, new CuratedSlot(0, 0)).Columns[0].Sections.Select(s => s.Title));

        var placed = CuratedLayouts.Placed([layout, Layout(Col(20, Sec(null, W("clock", 1, 1) with { Style = "analog" })))]);
        Assert.Contains("clock@analog", placed);
        Assert.Contains("d", placed);
    }

    /// <summary>[S-16745] A tab with no file yet begins as its automatic bento: the layout made of it lays the same boxes.</summary>
    [Fact]
    public void S16745_a_tab_begins_as_its_automatic_bento()
    {
        var kinds = new[] { ("clock", 20, 10), ("date", 20, 6), ("alarm", 16, 14), ("timer", 30, 12), ("notes", 24, 20), ("tasks", 22, 16) };
        var category = new GalleryCategory(AddFolderId.Time, LocalText.Fixed("Time"), string.Empty, [.. kinds.Select(k => new GalleryPiece("kind:" + k.Item1, new WidgetId(k.Item1), null))]);
        var asked = kinds.ToDictionary(k => "kind:" + k.Item1, k => new PieceSize(k.Item2, k.Item3, k.Item2 / 2, k.Item3 / 2, true, true) { MaxW = k.Item2 * 2, MaxH = k.Item3 * 2 });
        var auto = WidgetGallery.Lay(category, 80, p => asked[p.Key], 2);

        var layout = CuratedLayouts.From(auto);
        Assert.True(layout.Titles);
        var top = auto.Columns.Where(c => c.Y == 0).OrderBy(c => c.X).ToList();
        Assert.Equal(top.Select(c => c.W), layout.Columns.Select(c => c.Width));

        // [S-17160] Each column its blocks, the columns two apart (the bento's one): each column's widgets where the bento
        // laid them in it, the column a cell further right for each gap before it.
        var curated = CuratedBento.Lay(AddFolderId.Time, LocalText.Fixed("Time"), layout, CuratedFit.AsStored, 2, 3, piece: w => new GalleryPiece("kind:" + w.Pick, new WidgetId(w.Pick), null));
        var shifted = auto.Tiles.Select(t => (t.Piece.Key, t.Box with { X = t.Box.X + top.FindIndex(c => t.Box.X >= c.X && t.Box.X < c.Right) }));
        Assert.Equal(shifted.OrderBy(t => t.Key), curated.Tiles.Select(t => (t.Piece.Key, t.Box)).OrderBy(t => t.Key));
    }
}
