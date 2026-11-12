using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2440] A kit list that states its rows: its size from them, and the same plan at every size in its range.</summary>
public sealed class KitRowsTests
{
    private static readonly KitSpec Checks = new(KitRowLook.Check, KitShow.Subtitle | KitShow.When, () => "Untitled", () => "Empty");

    private static readonly KitSpec Rich = new(KitRowLook.Rich, KitShow.All, () => "Untitled", () => "Empty", MaxColumns: 3);

    [Theory]
    [InlineData(6, 1, true, 36 + (6 * 28) + 28 + 12)]
    [InlineData(6, 2, true, 36 + (6 * 40) + 28 + 12)]
    [InlineData(1, 1, false, 36 + 28 + 12)]
    [InlineData(20, 2, false, 36 + (20 * 40) + 12)]
    public void S2440_the_height_is_the_content_top_the_rows_the_add_footer_and_the_inset(int rows, int lines, bool footer, double height)
    {
        var sizing = KitSizing.Sizing(new KitRowsChoice(rows, lines), footer);

        Assert.Equal(height, sizing.Natural.Height);
        Assert.Equal(KitSizing.MinWidth, sizing.Min.Width);
        Assert.Equal(KitSizing.MaxWidth, sizing.Max.Width);
        Assert.Equal(FamilyShape.RowHeight(lines), sizing.HeightSteps!.Step);
        Assert.Equal(rows, sizing.HeightSteps.Count);
        Assert.Empty(sizing.Problems());
    }

    [Fact]
    public void S2440_a_check_list_shows_the_same_at_its_min_and_its_max()
    {
        var choices = new KitChoices(KitShow.Subtitle | KitShow.When) { Count = 30, CanAdd = true, Rows = new KitRowsChoice(6, 2) };
        var sizing = KitSizing.Sizing(choices.Rows!.Value, addFooter: true);

        KitPlan At(double w, double h) => KitLadder.Plan(new WidgetArea(w, h - 36), Checks, choices);
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
        var narrow = KitLadder.Plan(new WidgetArea(220, 300), Rich, choices);
        var wide = KitLadder.Plan(new WidgetArea(800, 300), Rich, choices);

        Assert.Equal(narrow with { ColumnWidth = 0 }, wide with { ColumnWidth = 0 });
        Assert.Equal(1, wide.Columns);
        Assert.True(wide.Lead && wide.When && wide.Subtitle);
        Assert.False(wide.Preview);
    }

    [Fact]
    public void S2443_a_kit_list_without_rows_keeps_the_ladder()
    {
        var wide = KitLadder.Plan(new WidgetArea(800, 500), Rich, new KitChoices(KitShow.All));
        Assert.Equal(2, wide.Columns);
        Assert.Equal(3, wide.Lines);
    }

    [Fact]
    public void S2440_rows_and_lines_are_held_to_their_ranges()
    {
        Assert.Equal(1, KitSizing.ClampRows(0));
        Assert.Equal(20, KitSizing.ClampRows(99));
        Assert.Equal(2, KitSizing.ClampLines(3));
        Assert.Equal(1, KitSizing.ClampLines(-1));
    }
}
