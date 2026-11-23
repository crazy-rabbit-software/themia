using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-16740]..[S-16746] The gallery's curated tabs (owner, 2026-11-23): columns of shares, sections in them, widgets packed
/// as tightly as they go in reading order; columns shrink, then stack; one file a tab, round-tripped.
/// </summary>
public sealed class CuratedBentoTests
{
    private static GalleryPiece Piece(CuratedWidget w) => new("kind:" + w.Pick, null, null);

    private static CuratedWidget W(string pick, int w, int h) => new() { Pick = pick, W = w, H = h };

    private static CuratedLayout Layout(params CuratedColumn[] columns) => new() { Titles = false, Columns = columns };

    private static CuratedColumn Col(double share, params CuratedSection[] sections) => new() { Share = share, Sections = sections };

    private static CuratedSection Sec(string? title, params CuratedWidget[] widgets) => new() { Title = title, Widgets = widgets };

    private static GallerySheet Lay(CuratedLayout layout, int width, int title = 0, bool editing = false)
        => CuratedBento.Lay(AddFolderId.Time, LocalText.Fixed("Time"), layout, width, CuratedFit.AsStored, title, 2, editing, Piece);

    /// <summary>
    /// [S-16743] "packed together as tightly as possible in direction of reading": each widget at the highest free spot of the
    /// column that holds it, the left-most of those, one cell from the others; a short one fills the room beside a tall one.
    /// </summary>
    [Fact]
    public void S16743_a_section_packs_each_widget_into_the_highest_free_spot_left_first()
    {
        var boxes = CuratedBento.Pack([(10, 20), (10, 8), (10, 8), (21, 5), (30, 4)], 21);

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
        var hole = CuratedBento.Pack([(10, 4), (21, 4), (10, 2)], 21);
        Assert.Equal(new CellBox(11, 0, 10, 2), hole[2]);
    }

    /// <summary>[S-16744] Shares become whole cells filling the width, one cell between columns, the remainder to the largest parts.</summary>
    [Fact]
    public void S16744_shares_become_whole_cells_that_fill_the_width()
    {
        Assert.Equal([50, 49, 49], CuratedBento.Widths([1, 1, 1], 150));
        Assert.Equal([30, 89], CuratedBento.Widths([0.25, 0.75], 120));
        Assert.Equal([60, 59], CuratedBento.Widths([0, double.NaN], 120));
        var widths = CuratedBento.Widths([0.3, 0.2, 0.5], 137);
        Assert.Equal(137, widths.Sum() + 2);
    }

    /// <summary>
    /// [S-16744] The owner's "Columns shrink, then stack": a narrower view narrows every column down to its least, then the
    /// rightmost columns leave the line and each stands, whole with its sections, under the column whose foot is highest.
    /// </summary>
    [Fact]
    public void S16744_columns_shrink_then_the_rightmost_stack_under_the_others()
    {
        var layout = Layout(
            Col(1, Sec("A", W("a1", 20, 30))),
            Col(1, Sec("B", W("b1", 20, 10)), Sec("B2", W("b2", 20, 4))),
            Col(1, Sec("C", W("c1", 20, 8))));

        // Wide: three across.
        var wide = Lay(layout, 92);
        Assert.Equal(3, wide.Columns.Count(c => c.Y == 0));
        Assert.Equal([30, 30, 30], wide.Columns.Select(c => c.W));

        // Narrow enough that three would be under the least (20): two across, the third under the shorter one (the second,
        // its foot 2+10+1+2+4 = 19 against the first's 32), its sections intact.
        Assert.Equal(2, CuratedBento.Across([1, 1, 1], [20, 20, 20], 60));
        var narrow = Lay(layout, 60);
        Assert.Equal([(0, 30), (31, 29)], narrow.Columns.Take(2).Select(c => (c.X, c.W)));
        Assert.Equal(new CellBox(31, 20, 29, 10), narrow.Columns[2]);
        var c1 = narrow.Tiles.Single(t => t.Piece.Key == "kind:c1");
        Assert.Equal(new CellBox(31, 22, 20, 8), c1.Box);
        Assert.Equal(new CuratedSlot(2, 0, 0), c1.Slot);
        Assert.Contains(narrow.Headings, h => h.Text.Text() == "C" && h.Box == new CellBox(31, 20, 29, 2));

        // Narrower than one column's least: one column, the rest under it in turn; every widget held to its width.
        var one = Lay(layout, 15);
        Assert.Single(one.Columns, c => c.Y == 0);
        Assert.All(one.Tiles, t => Assert.True(t.Box.Right <= 15));
        Assert.Equal(3, one.Columns.Select(c => c.Y).Distinct().Count());

        // A column whose widget asks more than the least keeps it: its widgets' least decides when it stacks.
        Assert.Equal(1, CuratedBento.Across([1, 1], [20, 40], 60));
    }

    /// <summary>[S-16743] [S-16721] A tab's sections down a column one cell apart, a titled one under its heading, every card under its title rows.</summary>
    [Fact]
    public void S16743_sections_stand_down_a_column_titled_under_a_heading_cards_under_their_titles()
    {
        var layout = new CuratedLayout { Titles = true, Columns = [Col(1, Sec(null, W("a", 10, 6)), Sec("Second", W("b", 10, 6), W("c", 10, 6)))] };
        var sheet = Lay(layout, 30, title: 2);

        Assert.Equal(2, sheet.Title);
        Assert.Equal(new CellBox(0, 0, 10, 8), sheet.Tiles[0].Box);                       // no heading: the widget from the top
        Assert.Equal(new GalleryHeading(LocalText.Fixed("Second"), new CellBox(0, 9, 30, 2)) { Slot = new CuratedSlot(0, 1) }, sheet.Headings.Single());
        Assert.Equal(new CellBox(0, 11, 10, 8), sheet.Tiles[1].Box);
        Assert.Equal(new CellBox(11, 11, 10, 8), sheet.Tiles[2].Box);
        Assert.Equal(new CellBox(11, 13, 10, 6), sheet.CardOf(sheet.Tiles[2]));
        Assert.Equal(19, sheet.Rows);
        Assert.Equal([new CellBox(0, 0, 30, 8), new CellBox(0, 9, 30, 10)], sheet.Areas.Select(a => a.Box));

        // Editing: the column's tools' band first, every section's heading line, an empty section room to drop into.
        var edit = Lay(CuratedEdits.AddSection(layout, 0), 30, title: 2, editing: true);
        Assert.Equal(3, edit.Headings.Count);
        Assert.Equal(3, edit.Headings[0].Box.Y);
        Assert.Equal(CuratedBento.EmptyRows + 2, edit.Areas[2].Box.H);
    }

    /// <summary>[S-16746] A layout file read back is the layout written: titles, shares, sections, picks, styles, sizes and a config fragment.</summary>
    [Fact]
    public void S16746_a_layout_file_round_trips()
    {
        var layout = new CuratedLayout
        {
            Titles = false,
            Columns =
            [
                Col(0.33333333, Sec("Clocks", W("clock", 20, 20) with { Style = "analog" }, W("quickToggle/wifi", 9, 9))),
                Col(0.6666, Sec(null, W("rss", 30, 40) with { Config = new JsonObject { ["rows"] = 6, ["feeds"] = new JsonArray("a", "b") } })),
            ],
        };

        var text = CuratedLayouts.Write(layout);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.Contains("\"share\": 0.3333", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"title\": null", text, StringComparison.Ordinal);

        var back = CuratedLayouts.Read(text);
        Assert.Equal(text, CuratedLayouts.Write(back));
        Assert.False(back.Titles);
        Assert.Equal("analog", back.Columns[0].Sections[0].Widgets[0].Style);
        Assert.Equal(6, (int)back.Columns[1].Sections[0].Widgets[0].Config!["rows"]!);
        Assert.Throws<JsonException>(() => CuratedLayouts.Read("[1, 2]"));
        Assert.Equal("calendarMail.json", CuratedLayouts.FileName(AddFolderId.CalendarMail));
    }

    /// <summary>[S-16746] Where read, a widget the build cannot show is dropped (loudly), a size held to its range, a bad share made 1.</summary>
    [Fact]
    public void S16746_a_layout_is_checked_where_it_is_read()
    {
        var layout = Layout(Col(-2, Sec("  ", W("clock", 0, 999), W("gone", 5, 5))));
        var checkedLayout = CuratedLayouts.Checked(layout, "time.json", w => w.Pick == "gone" ? "no such widget" : null);

        var widgets = checkedLayout.Columns[0].Sections[0].Widgets;
        Assert.Equal(1, checkedLayout.Columns[0].Share);
        Assert.Null(checkedLayout.Columns[0].Sections[0].Title);
        Assert.Equal(["clock"], widgets.Select(w => w.Pick));
        Assert.Equal((1, CuratedLayouts.MostCells), (widgets[0].W, widgets[0].H));

        // Over this test's registry (no widget) every pick is one the build lacks.
        Assert.NotNull(CuratedLayouts.Problem(W("clock", 5, 5)));
        Assert.Empty(CuratedLayouts.Checked(layout, "time.json").Columns[0].Sections[0].Widgets);
    }

    /// <summary>[S-16745] The editor's steps: a widget moved between and inside sections, resized, removed; columns resized, added and removed; sections reordered.</summary>
    [Fact]
    public void S16745_the_editors_steps_change_the_layout()
    {
        var layout = Layout(Col(1, Sec("A", W("a", 5, 5), W("b", 5, 5), W("c", 5, 5))), Col(1, Sec("B", W("d", 5, 5))));

        var inside = CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 0, 0), new CuratedSlot(0, 0, 2));
        Assert.Equal(["b", "a", "c"], inside.Columns[0].Sections[0].Widgets.Select(w => w.Pick));
        var across = CuratedEdits.MoveWidget(layout, new CuratedSlot(0, 0, 2), new CuratedSlot(1, 0, 0));
        Assert.Equal(["c", "d"], across.Columns[1].Sections[0].Widgets.Select(w => w.Pick));
        Assert.Equal(2, across.Columns[0].Sections[0].Widgets.Count);

        var resized = CuratedEdits.ResizeWidget(layout, new CuratedSlot(0, 0, 1), 12, 0);
        Assert.Equal((12, 1), (resized.Columns[0].Sections[0].Widgets[1].W, resized.Columns[0].Sections[0].Widgets[1].H));
        Assert.Equal(["a", "c"], CuratedEdits.RemoveWidget(layout, new CuratedSlot(0, 0, 1)).Columns[0].Sections[0].Widgets.Select(w => w.Pick));

        Assert.Equal([0.25, 0.75], CuratedEdits.Resize(layout, [30, 90]).Columns.Select(c => c.Share));
        Assert.Same(layout, CuratedEdits.Resize(layout, [30]));
        var three = CuratedEdits.AddColumn(layout);
        Assert.Equal(3, three.Columns.Count);
        Assert.Single(three.Columns[2].Sections);
        Assert.Single(CuratedEdits.RemoveColumn(CuratedEdits.RemoveColumn(layout, 0), 0).Columns);

        var sections = CuratedEdits.AddSection(layout, 0, " New ");
        Assert.Equal("New", sections.Columns[0].Sections[1].Title);
        var moved = CuratedEdits.MoveSection(sections, new CuratedSlot(0, 1), -1);
        Assert.Equal(["New", "A"], moved.Columns[0].Sections.Select(s => s.Title));
        Assert.Null(CuratedEdits.Rename(moved, new CuratedSlot(0, 0), "  ").Columns[0].Sections[0].Title);
        Assert.Equal(["A"], CuratedEdits.RemoveSection(moved, new CuratedSlot(0, 0)).Columns[0].Sections.Select(s => s.Title));

        var placed = CuratedLayouts.Placed([layout, Layout(Col(1, Sec(null, W("clock", 1, 1) with { Style = "analog" })))]);
        Assert.Contains("clock@analog", placed);
        Assert.Contains("d", placed);
    }

    /// <summary>[S-16745] A widget dropped into a section lands before the first of its widgets that comes after the drop in reading order.</summary>
    [Fact]
    public void S16745_a_drop_lands_in_reading_order()
    {
        var sheet = Lay(Layout(Col(1, Sec(null, W("a", 10, 5), W("b", 10, 5), W("c", 10, 5)))), 21);
        var section = new CuratedSlot(0, 0);
        Assert.Equal(0, CuratedBento.Insertion(sheet, section, (2, 2)));
        Assert.Equal(1, CuratedBento.Insertion(sheet, section, (8, 2)));
        Assert.Equal(2, CuratedBento.Insertion(sheet, section, (3, 7)));
        Assert.Equal(3, CuratedBento.Insertion(sheet, section, (15, 8)));
        Assert.Equal(3, CuratedBento.Insertion(sheet, section, (0, 40)));
    }
}
