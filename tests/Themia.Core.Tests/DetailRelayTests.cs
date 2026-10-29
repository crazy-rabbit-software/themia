using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6333] A new read lays a detail tile out again only when its face changes: a list of processes
/// growing or shrinking by one is a fill of the rows it has, not a layout of the whole tile.
/// </summary>
public sealed class DetailRelayTests
{
    /// <summary>A Top processes sheet: no hero, no facts, one row per process.</summary>
    private static DetailSheet Processes(int count)
        => new(null, null, [], [.. Enumerable.Range(0, count).Select(i => new DetailRow($"p{i}", $"app{i}", "1%"))]);

    /// <summary>The plan a list-only part gets for a sheet in <paramref name="area"/>, as the view plans it.</summary>
    private static Func<DetailSheet, DetailFacePlan> ListPlan(WidgetArea area)
        => sheet => DetailFace.Plan(area, new DetailShape(null, 0, sheet.Rows.Count, DetailFace.ListLines(sheet.Rows)));

    [Theory]
    [InlineData(120, 121)]
    [InlineData(121, 120)]
    [InlineData(8, 60)]
    public void S6333_a_scrolling_list_with_another_count_of_rows_is_a_fill(int before, int after)
    {
        // Was a layout of the whole tile on every process that started or exited.
        var planFor = ListPlan(new WidgetArea(320, 196));
        var shown = Processes(before);

        Assert.False(DetailFace.Relays(shown, Processes(after), planFor(shown), planFor));
    }

    [Fact]
    public void S6333_a_list_that_empties_or_fills_is_laid_out()
    {
        var planFor = ListPlan(new WidgetArea(320, 196));
        var shown = Processes(5);

        Assert.True(DetailFace.Relays(shown, Processes(0), planFor(shown), planFor));
        Assert.True(DetailFace.Relays(Processes(0), shown, planFor(Processes(0)), planFor));
    }

    [Fact]
    public void S6333_rows_that_change_the_face_are_laid_out()
    {
        // A list whose rows all fit: one more row is a taller list.
        var area = new WidgetArea(560, 376);
        Func<DetailSheet, DetailFacePlan> planFor = sheet =>
            DetailFace.Plan(area, new DetailShape(new Themia.Sdk.Kit.ReadoutText("42", "%"), 0, sheet.Rows.Count));
        var shown = Processes(2) with { Hero = "42%" };
        var next = Processes(3) with { Hero = "42%" };

        Assert.NotEqual(planFor(shown), planFor(next));
        Assert.True(DetailFace.Relays(shown, next, planFor(shown), planFor));
    }

    [Fact]
    public void S6333_what_the_face_is_made_of_still_lays_it_out()
    {
        var planFor = ListPlan(new WidgetArea(320, 196));
        var shown = Processes(5);
        var plan = planFor(shown);

        Assert.False(DetailFace.Relays(shown, Processes(5), plan, planFor));
        Assert.True(DetailFace.Relays(shown, shown with { Hero = "1" }, plan, planFor));
        Assert.True(DetailFace.Relays(shown, shown with { Caption = "of 8" }, plan, planFor));
        Assert.True(DetailFace.Relays(shown, shown with { Absent = true }, plan, planFor));
        Assert.True(DetailFace.Relays(shown, shown with { Facts = [new DetailFact("Uptime", "3 h")] }, plan, planFor));

        // A longer value is a fill: the row's name gives way, the value is never cut ([S-6711]).
        var facts = shown with { Facts = [new DetailFact("Uptime", "3 h")] };
        Assert.False(DetailFace.Relays(facts, facts with { Facts = [new DetailFact("Uptime", "3 h 12 min")] }, plan, planFor));

    }
}
