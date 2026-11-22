using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-13950] An inline box stands in exactly the box of the words it replaces and moves nothing else (owner, 2026-11-19:
/// "layout shifts for these kind of inputs should never happen"). The judge's cases are the task list's add row at its
/// natural 300 × 220, as the inline check measured it with the platform's text box and with the inline box.
/// </summary>
public class InlineFitTests
{
    private static readonly (double, double) Card = (300, 220);

    // At rest: the list's viewport over the rows, the add row (28) with its + and its line "Add a task".
    private static readonly InlinePart Scroll = new("scroll", 0, 0, 300, 192);
    private static readonly InlinePart AddRow = new("row", 0, 192, 300, 28);
    private static readonly InlinePart Plus = new("glyph E710", 12, 198, 16, 16);
    private static readonly InlinePart Line = new("text Add a task", 36, 196, 65.1, 20);
    private static readonly InlinePart LastTask = new("text Renew the bike insurance", 36, 166, 156.9, 20);

    private static readonly InlinePart[] Rest = [Scroll, LastTask, AddRow, Plus, Line];

    [Fact]
    public void S13950_a_box_is_as_tall_as_the_lines_of_the_words_it_stands_in_for()
    {
        Assert.Equal(FamilyType.BodyLine, InlineFit.Of(FamilyType.BodyLine).Height);
        Assert.Equal(2 * FamilyType.CaptionLine, InlineFit.Of(FamilyType.CaptionLine, 2).Height);
        Assert.Equal(FamilyType.BodyLine, InlineFit.Of(FamilyType.BodyLine, 0).Height);
    }

    [Fact]
    public void S13950_a_box_puts_nothing_between_its_edge_and_its_text()
        => Assert.Equal(0, InlineFit.Of(FamilyType.BodyLine).TextInset);

    [Fact]
    public void S13950_a_box_of_one_line_never_wraps_taller()
    {
        Assert.False(InlineFit.Of(FamilyType.BodyLine).Wraps);
        Assert.True(InlineFit.Of(FamilyType.CaptionLine, 2).Wraps);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void S13950_a_box_in_a_rows_name_is_no_taller_than_the_row_it_stands_in(int lines)
        => Assert.True(InlineFit.Of(FamilyType.BodyLine).Height <= FamilyShape.RowHeight(lines));

    [Fact]
    public void S13950_the_inline_box_in_the_add_row_moves_nothing()
    {
        // The box on the line's own box, as wide as the row's room; the line collapsed under it.
        var box = new InlinePart("box", 36, 196, 252, 20);

        Assert.Empty(InlineFit.Judge(Card, Card, box, Rest, [Scroll, LastTask, AddRow, Plus]));
    }

    [Fact]
    public void S13950_the_platform_box_in_the_add_row_grew_it_and_cut_the_list_short()
    {
        // The bug (2026-11-19): WinUI's box (32 minimum, padding 10,5,6,6, a frame of 1) grew the add row to 32.7, which
        // pushed its + up 2 and took the room of the list's last row.
        var box = new InlinePart("box", 36, 187.3, 252, 32.7);
        var open = new[] { Scroll with { Height = 187.3 }, LastTask, AddRow with { Y = 187.3, Height = 32.7 }, Plus with { Y = 196 } };

        var findings = InlineFit.Judge(Card, Card, box, Rest, open);

        Assert.Contains(findings, f => f.StartsWith("the box", StringComparison.Ordinal) && f.Contains("Add a task", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.StartsWith("\"scroll\" moved", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.StartsWith("\"row\" moved", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.StartsWith("\"glyph E710\" moved", StringComparison.Ordinal));
    }

    [Fact]
    public void S13950_words_wrapped_onto_more_lines_are_a_finding()
    {
        // Typed words longer than the row: the old box wrapped them onto three lines, 50.7 tall.
        var box = new InlinePart("box", 36, 169.3, 252, 50.7);

        Assert.Contains(InlineFit.Judge(Card, Card, box, Rest, [Scroll, LastTask, AddRow, Plus]), f => f.StartsWith("the box", StringComparison.Ordinal));
    }

    [Fact]
    public void S13950_a_box_whose_text_stands_off_the_words_edge_is_a_finding()
    {
        // The fence's and the folder's old boxes: padding 4 and a frame of 1 put the text 5 to the right.
        var box = new InlinePart("box", 31, 196, 257, 20);

        Assert.Contains(InlineFit.Judge(Card, Card, box, Rest, [Scroll, LastTask, AddRow, Plus]), f => f.StartsWith("the box", StringComparison.Ordinal));
    }

    [Fact]
    public void S13950_a_part_that_is_no_longer_drawn_is_a_finding()
    {
        var box = new InlinePart("box", 36, 196, 252, 20);

        var findings = InlineFit.Judge(Card, Card, box, Rest, [Scroll, AddRow, Plus]);

        Assert.Equal(["\"text Renew the bike insurance\" (36,166 156.9x20) is gone"], findings);
    }

    [Fact]
    public void S13950_a_card_that_changes_size_is_a_finding()
    {
        var box = new InlinePart("box", 36, 196, 252, 20);

        Assert.Contains(InlineFit.Judge(Card, (300, 232.7), box, Rest, [Scroll, LastTask, AddRow, Plus]), f => f.StartsWith("the card", StringComparison.Ordinal));
    }

    [Fact]
    public void S13950_a_search_box_taking_the_keys_replaces_nothing_and_must_move_nothing()
    {
        // The contacts' box is there at rest: focused, it stands in for no words, and the rows keep their places.
        var search = new InlinePart("box", 12, 12, 276, 32.7);
        InlinePart[] rows = [new("text Alex Morgan", 52, 56, 120, 20), new("row", 0, 52, 300, 28)];

        Assert.Empty(InlineFit.Judge(Card, Card, search, rows, rows));
    }

    // ---- a search opened on demand ([S-13950]) ------------------------------------------------------

    // The folder at rest: its tool line's label and its two buttons, the list's viewport and three rows (listed).
    private static readonly InlinePart Label = new("text Documents", 12, 0, 70, 20);
    private static readonly InlinePart SearchButton = new("glyph E721", 236, 2, 16, 16);
    private static readonly InlinePart Items = new("scroll", 0, 24, 300, 84);
    private static readonly InlinePart[] Rows =
    [
        new("row", 0, 24, 300, 28, Listed: true), new("text Northwind", 36, 28, 66, 20, Listed: true),
        new("row #2", 0, 52, 300, 28, Listed: true), new("text Onboarding", 36, 56, 75, 20, Listed: true),
        new("row #3", 0, 80, 300, 28, Listed: true), new("text Receipts", 36, 84, 57, 20, Listed: true),
    ];

    private static readonly InlinePart[] Folder = [Label, SearchButton, Items, .. Rows];

    [Fact]
    public void S13950_a_search_opened_on_the_tool_line_stands_in_its_label_and_moves_nothing()
    {
        // The line: the label's box (its left edge, top and 20), as wide as the label's room before the buttons.
        var line = new InlinePart("box", 12, 0, 212, 20);

        Assert.Empty(InlineFit.Judge(Card, Card, line, Folder, [SearchButton, Items, .. Rows]));
    }

    [Fact]
    public void S13950_the_field_opened_over_the_list_pushed_it_down()
    {
        // The bug: the 32 field and its 8 opened under the line and every row stood 40 lower.
        var field = new InlinePart("box", 12, 24, 276, 32.7);
        InlinePart[] open = [Label, SearchButton, Items with { Y = 64.7, Height = 44 }, .. Rows.Select(r => r with { Y = r.Y + 40.7 })];

        var findings = InlineFit.Judge(Card, Card, field, Folder, open);

        Assert.Contains(findings, f => f.StartsWith("\"row\" moved", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.StartsWith("\"scroll\" moved", StringComparison.Ordinal));
    }

    [Fact]
    public void S13950_typing_in_a_search_changes_what_the_rows_say_never_where_they_stand()
    {
        // "re" leaves two items, drawn on the first two rows' places; round the list nothing moved.
        var line = new InlinePart("box", 12, 0, 212, 20);
        InlinePart[] open =
        [
            SearchButton, Items,
            new("row", 0, 24, 300, 28, Listed: true), new("text Receipts", 36, 28, 57, 20, Listed: true),
            new("row #2", 0, 52, 300, 28, Listed: true), new("text Release notes", 36, 56, 90, 20, Listed: true),
        ];

        Assert.Empty(InlineFit.Judge(Card, Card, line, Folder, open, filters: true));
    }

    [Fact]
    public void S13950_a_row_a_search_draws_off_the_rows_places_is_a_finding()
    {
        var line = new InlinePart("box", 12, 0, 212, 20);
        InlinePart[] open = [SearchButton, Items, new("row", 0, 30, 300, 28, Listed: true)];

        Assert.Equal(["a row (0,30 300x28) stands on no row's place"], InlineFit.Judge(Card, Card, line, Folder, open, filters: true));
    }

    [Fact]
    public void S13950_a_filtering_search_still_holds_what_stands_round_the_list()
    {
        var line = new InlinePart("box", 12, 0, 212, 20);
        InlinePart[] open = [SearchButton with { X = 220 }, Items, .. Rows];

        Assert.Contains(InlineFit.Judge(Card, Card, line, Folder, open, filters: true), f => f.StartsWith("\"glyph E721\" moved", StringComparison.Ordinal));
    }

    [Fact]
    public void S13950_a_search_closed_again_leaves_everything_where_it_was()
        => Assert.Empty(InlineFit.Judge(Card, Card, null, Folder, Folder));

    [Fact]
    public void S15793_the_boxs_text_moves_down_onto_its_words_baseline_never_up()
    {
        // A Body line (14 on 20): its baseline at 16, the platform's box's at 15.26: 0.74 down.
        Assert.Equal((0.8 * 20) - (InlineFit.BoxBaseline * 14), InlineFit.BaselineShift(20, 14), 6);
        Assert.True(InlineFit.BaselineShift(20, 14) is > 0.5 and < 1);

        // Words whose box would stand lower than their line: no shift up; nonsense: none.
        Assert.Equal(0, InlineFit.BaselineShift(10, 14));
        Assert.Equal(0, InlineFit.BaselineShift(double.NaN, 14));
    }
}
