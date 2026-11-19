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
}
