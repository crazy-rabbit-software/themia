using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2440] A kit list that states its rows: its size from them, and the same plan at every size in its range.</summary>
public sealed class KitRowsTests
{
    private static readonly KitSpec Checks = new(KitRowLook.Check, KitShow.Subtitle | KitShow.When, () => "Untitled", () => "Empty");

    private static readonly KitSpec Rich = new(KitRowLook.Rich, KitShow.All, () => "Untitled", () => "Empty", MaxColumns: 3);

    /// <summary>[S-2440] [S-14500] The rows and the add footer at their optimal (30 and 40 on the default grid): no inset over or under them; every cell from one row up.</summary>
    [Theory]
    [InlineData(6, 1, true, (6 * 30) + 30)]
    [InlineData(6, 2, true, (6 * 40) + 30)]
    [InlineData(1, 1, false, 30)]
    [InlineData(20, 2, false, 20 * 40)]
    public void S2440_the_height_is_the_rows_and_the_add_footer_in_whole_cells(int rows, int lines, bool footer, double height)
    {
        var sizing = KitSizing.Sizing(new KitRowsChoice(rows, lines), footer);

        Assert.Equal(height, sizing.Natural.Height);
        Assert.Equal(KitSizing.MinWidth, sizing.Min.Width);
        Assert.Equal(KitSizing.MaxWidth, sizing.Max.Width);
        Assert.Equal(height - ((rows - 1) * Themia.Core.Widgets.ItemGrid.Default().Whole(FamilyShape.RowHeight(lines))), sizing.Min.Height);
        Assert.Empty(sizing.Problems());
    }

    [Fact]
    public void S14500_a_check_list_that_cannot_add_has_its_rows_take_the_add_rows_room()
    {
        // A CalDAV list 200 tall left its add row's place blank at the foot (33.3 under rows stretched to 41.3); its rows
        // take that room too and end on the card's foot. One that can add keeps the add row under its rows.
        var area = new WidgetArea(300, 200 - FamilySpace.Inset);
        var readOnly = KitPlanner.Plan(area, Checks, new KitChoices(KitShow.None) { CanAdd = false, Rows = new KitRowsChoice(6, 1) });
        var adds = KitPlanner.Plan(area, Checks, new KitChoices(KitShow.None) { CanAdd = true, Rows = new KitRowsChoice(6, 1) });

        Assert.Equal((0d, 200d), (readOnly.FootHeight, readOnly.Reach.Room));
        Assert.Equal(200, adds.Reach.Room + adds.FootHeight, 6);
        Assert.True(adds.FootHeight > 0);
    }

    [Fact]
    public void S2440_a_check_list_shows_the_same_at_its_min_and_its_max()
    {
        var choices = new KitChoices(KitShow.Subtitle | KitShow.When) { Count = 30, CanAdd = true, Rows = new KitRowsChoice(6, 2) };
        var sizing = KitSizing.Sizing(choices.Rows, addFooter: true);

        KitPlan At(double w, double h) => KitPlanner.Plan(new WidgetArea(w, h - 36), Checks, choices);
        var small = At(sizing.Min.Width, sizing.Natural.Height);
        var large = At(sizing.Max.Width, sizing.Natural.Height);

        Assert.Equal(small with { ColumnWidth = 0 }, large with { ColumnWidth = 0 });
        Assert.Equal(1, large.Columns);
        Assert.Equal(2, large.Lines);
        Assert.True(large.AddRow && large.Subtitle && large.When && large.Headings);
        Assert.False(large.UrgentOnly || large.LongWhen);
    }

    [Fact]
    public void S2440_a_rich_list_with_rows_keeps_one_column_and_every_field_it_has_on_at_any_width()
    {
        var choices = new KitChoices(KitShow.All) { Rows = new KitRowsChoice(4, 2) };
        var narrow = KitPlanner.Plan(new WidgetArea(220, 300), Rich, choices);
        var wide = KitPlanner.Plan(new WidgetArea(800, 300), Rich, choices);

        Assert.Equal(narrow with { ColumnWidth = 0 }, wide with { ColumnWidth = 0 });
        Assert.Equal(1, wide.Columns);
        Assert.True(wide.Lead && wide.When && wide.Subtitle);
        Assert.False(wide.Preview);
    }

    [Fact]
    public void S2440_rows_and_lines_are_held_to_their_ranges()
    {
        Assert.Equal(1, KitSizing.ClampRows(0));
        Assert.Equal(20, KitSizing.ClampRows(99));
        Assert.Equal(2, KitSizing.ClampLines(3));
        Assert.Equal(1, KitSizing.ClampLines(-1));
    }
    [Fact]
    public void S11400_S14502_a_list_of_two_columns_stands_one_column_new_and_splits_a_width_that_holds_two()
    {
        var one = KitSizing.Sizing(new KitRowsChoice(5, 2), addFooter: false);
        var two = KitSizing.Sizing(new KitRowsChoice(5, 2, Columns: 2), addFooter: false);

        // [S-14410] [S-14502] The columns are the card's width, never stored: a new card is one column's, its least too, so a
        // narrower drag goes back to one; the largest twice one column's.
        Assert.Equal(one.Min.Width, two.Min.Width);
        Assert.Equal(one.Natural.Width, two.Natural.Width);
        Assert.Equal(2 * one.Max.Width, two.Max.Width);
        Assert.Equal(one.Natural.Height, two.Natural.Height);

        var plan = KitPlanner.Plan(new WidgetArea(600, 300), Rich, new KitChoices(KitShow.All) { Rows = new KitRowsChoice(5, 2, Columns: 2) });
        Assert.Equal(2, plan.Columns);
        Assert.Equal(300, plan.ColumnWidth);

        // A list that may not stand in two never does, however wide.
        Assert.Equal(1, KitPlanner.Plan(new WidgetArea(600, 300), Rich, new KitChoices(KitShow.All) { Rows = new KitRowsChoice(5, 2) }).Columns);
    }

    [Fact]
    public void S11400_S14410_the_columns_are_the_cards_width_two_where_it_holds_two_columns_least()
    {
        // Owner, 2026-11-20: "Anything that's just 'how many of the same item' (days, hours, weeks, processes) follows
        // the card's size": no Columns line; the width a resize lands on decides them.
        Assert.Equal(1, KitSizing.ColumnsFor(300));
        Assert.Equal(1, KitSizing.ColumnsFor(439));
        Assert.Equal(2, KitSizing.ColumnsFor(440));
        Assert.Equal(2, KitSizing.ColumnsFor(1200));
        Assert.Equal(2, KitSizing.ColumnsFor(600, new KitWidths(300, 400, 600)));
        Assert.Equal(1, KitSizing.ColumnsFor(599, new KitWidths(300, 400, 600)));
        Assert.Equal(1, KitSizing.ColumnsFor(double.NaN));
    }

    [Fact]
    public void S11400_a_row_may_have_three_lines_the_preview_its_third()
    {
        var plan = KitPlanner.Plan(new WidgetArea(300, 300), Rich, new KitChoices(KitShow.All) { Rows = new KitRowsChoice(4, 3) });
        Assert.Equal(3, plan.Lines);
        Assert.True(plan.Subtitle && plan.Preview);
        Assert.Equal(4 * 60, KitSizing.Sizing(new KitRowsChoice(4, 3), false).Natural.Height);
        Assert.Equal(3, KitSizing.ClampLines(9, KitSizing.MaxLines));
        Assert.Equal(2, KitSizing.ClampColumns(5));
    }

    [Fact]
    public void S11401_a_table_with_rows_has_its_titles_by_choice_and_its_own_widths()
    {
        var table = new KitSpec(KitRowLook.Table, KitShow.All, () => "Untitled", () => "Empty");
        var choices = new KitChoices(KitShow.All, [KitColumn.Title, KitColumn.When]) { Rows = new KitRowsChoice(5, 1, ColumnTitles: true) };
        Assert.True(KitPlanner.Plan(new WidgetArea(400, 40), table, choices).ColumnHeader);
        Assert.False(KitPlanner.Plan(new WidgetArea(400, 900), table, choices with { Rows = new KitRowsChoice(5, 1) }).ColumnHeader);

        var sizing = KitSizing.Sizing(new KitRowsChoice(5, 1, ColumnTitles: true), false, new KitWidths(320, 420, 720));
        Assert.Equal((320, 420, 720), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
        Assert.Equal(40 + (5 * 30), sizing.Natural.Height);
    }

    [Fact]
    public void S11541_a_schedule_with_rows_is_one_column_of_its_lines_at_every_size()
    {
        var schedule = new KitSpec(KitRowLook.Schedule, KitShow.None, () => "Untitled", () => "Empty", MaxColumns: 2);
        foreach (var lines in new[] { 1, 2 })
        {
            var choices = new KitChoices(KitShow.None) { Rows = new KitRowsChoice(6, lines) };
            foreach (var area in new[] { new WidgetArea(260, 100), new WidgetArea(900, 700) })
            {
                var plan = KitPlanner.Plan(area, schedule, choices);
                Assert.Equal((1, lines, lines >= 2), (plan.Columns, plan.Lines, plan.Subtitle));

                // [S-12807] [S-14500] Its rows stand as many as the box holds at their optimal and share it to its foot, a
                // heading with them.
                var reach = ItemStretch.Rows(area.Height, lines, area.Grid());
                Assert.Equal(reach.Pitch, plan.RowHeight, 6);
                Assert.True(plan.RowHeight >= area.Grid().Whole(FamilyShape.RowHeight(lines)) - 1e-6);
                // [S-12834] A heading is one of the rows that tile the list: as tall as they are, whole cells (WR-6: "all items
                // of a widget are the same size"), so the rows under it start right under it and touch it (WR-2).
                Assert.Equal(plan.RowHeight, plan.HeadingHeight, 6);
            }
        }

        // A schedule that names no rows of its own is the kit's default rows: one column too ([S-11580]).
        Assert.Equal(1, KitPlanner.Plan(new WidgetArea(900, 700), schedule, KitShow.None).Columns);
    }

    /// <summary>
    /// [S-14266] A list in columns whose last line it does not fill (seven items in two): that line's last item takes the rest
    /// of it, so every line reaches the end edge; a list that fills its lines, and every other item, take one column.
    /// </summary>
    [Theory]
    [InlineData(6, 7, 2, 2)]
    [InlineData(5, 7, 2, 1)]
    [InlineData(7, 8, 3, 2)]
    [InlineData(6, 7, 3, 3)]
    [InlineData(5, 6, 2, 1)]
    [InlineData(5, 6, 3, 1)]
    [InlineData(0, 1, 1, 1)]
    [InlineData(0, 1, 2, 2)]
    public void S14266_the_last_item_of_a_line_the_list_does_not_fill_takes_the_rest_of_it(int index, int count, int columns, int span)
        => Assert.Equal(span, KitPlanner.ColumnSpan(index, count, columns));
}
