using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2402] The detail widgets' ladder on the family's blocks: the figure a Readout at the top, fact
/// rows (32) under it or beside it, the list's rows (by the tier's lines) after them, whole rows only.
/// Areas are the seven tiers less the header, as the card hands them to a view.
/// </summary>
public sealed class DetailFaceTests
{
    private static readonly ReadoutText Hero = ReadoutRules.Split("10.00 GHz") with { LineUnder = true };

    private static WidgetArea Area(string tier)
    {
        var t = WidgetTiers.All.Single(x => x.Name == tier);
        return new WidgetArea(t.Width, t.Height - FamilySpace.ContentTop);
    }

    [Fact]
    public void S2402_a_tiny_tile_is_the_figure_and_its_line_alone()
    {
        var plan = DetailFace.Plan(Area("XS"), new DetailShape(Hero, 8));
        Assert.False(plan.Side);
        Assert.True(plan.Under);
        Assert.Equal(0, plan.Facts);
        Assert.Equal(28, FamilyType.BaseOf(plan.Hero));
    }

    [Theory]
    [InlineData("Wide")]
    [InlineData("L")]
    [InlineData("XL")]
    public void S2402_an_expanded_width_puts_the_figure_in_a_column_and_the_rows_beside_it(string tier)
    {
        var area = Area(tier);
        var plan = DetailFace.Plan(area, new DetailShape(Hero, 8));
        Assert.True(plan.Side);
        Assert.Equal(0, plan.RowsTop);
        Assert.Equal(Math.Floor(area.Width * DetailFace.HeroShare), plan.HeroColumn);
        Assert.Equal(Math.Min(8, RowRules.Fit(area.Height - FamilySpace.Inset, 1)), plan.Facts);
    }

    [Theory]
    [InlineData("S", 0)]
    [InlineData("M", 3)]
    [InlineData("Tall", 8)]
    public void S2402_otherwise_the_facts_are_whole_rows_8_under_the_figure(string tier, int facts)
    {
        var area = Area(tier);
        var plan = DetailFace.Plan(area, new DetailShape(Hero, 12));
        Assert.False(plan.Side);
        Assert.Equal(plan.Hero.LineHeight + FamilyType.Under(plan.Hero).LineHeight + FamilySpace.S2, plan.RowsTop);
        Assert.Equal(facts, plan.Facts);
        Assert.True(plan.RowsTop + (plan.Facts * FamilyShape.Row1) <= area.Height - FamilySpace.Inset);
    }

    [Theory]
    [InlineData("XS")]
    [InlineData("S")]
    [InlineData("M")]
    [InlineData("Wide")]
    [InlineData("Tall")]
    [InlineData("L")]
    [InlineData("XL")]
    public void S666_the_figure_stops_at_68_and_a_larger_tile_shows_more_rows(string tier)
    {
        var plan = DetailFace.Plan(Area(tier), new DetailShape(Hero, 20));
        Assert.True(FamilyType.BaseOf(plan.Hero) <= 68);
        Assert.True(FamilyType.BaseOf(plan.Hero) <= ReadoutRules.Ceiling(Area(tier)));
    }

    [Fact]
    public void S2402_the_list_follows_the_facts_in_whole_rows_of_the_tiers_lines()
    {
        var area = Area("Tall");
        var plan = DetailFace.Plan(area, new DetailShape(Hero, 6, ListRows: 4, ListLines: 3));
        Assert.Equal(6, plan.Facts);
        Assert.Equal(2, plan.ListLines);
        var left = area.Height - FamilySpace.Inset - plan.RowsTop - (6 * FamilyShape.Row1);
        Assert.Equal(RowRules.Fit(left, 2), plan.ListRows);
        Assert.Equal(plan.ListRows * FamilyShape.Row2, plan.ListHeight);
    }

    [Theory]
    [InlineData("XS", 1)]
    [InlineData("M", 2)]
    [InlineData("L", 3)]
    public void S2404_a_list_without_a_figure_takes_the_view_from_its_top(string tier, int lines)
    {
        var area = Area(tier);
        var plan = DetailFace.Plan(area, new DetailShape(null, 0, ListRows: 40, ListLines: 3));
        Assert.Equal(0, plan.RowsTop);
        Assert.Equal(lines, plan.ListLines);
        Assert.Equal(RowRules.Fit(area.Height - FamilySpace.Inset, lines), plan.ListRows);
    }

    [Fact]
    public void S2404_a_rows_lines_are_its_name_a_second_figure_and_a_detail()
    {
        Assert.Equal(1, DetailFace.ListLines([new DetailRow("a", "a", "1%")]));
        Assert.Equal(2, DetailFace.ListLines([new DetailRow("a", "a", "1%", Second: "2 MB")]));
        Assert.Equal(3, DetailFace.ListLines([new DetailRow("a", "a", "1%"), new DetailRow("b", "b", "1", Second: "2", Detail: "3")]));
    }
}
