using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Rss;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-14500] The kinds whose content is repeated items, at every cell (owner, 2026-11-20: "the items should have an optimal
/// size and then they may stretch a little to fill up the remaining space. the widgets should not have the size increments
/// anymore but should be incrementable in steps of one"): at any height a resize lands on, on displays of several
/// pitches, the items the view lays (the one rule, <see cref="ItemStretch"/>) are as many as the card holds at their
/// optimal and end exactly on its foot.
/// </summary>
public sealed class OptimalStretchKindsTests
{
    /// <summary>A 1080p display, 1920 at 125 % (pitch 9.974 across, 10.047 down) and a content scale of 1.25 (pitch 8).</summary>
    private static readonly (ScreenMetrics Screen, double Scale)[] Displays = [(new(1920, 1080), 1), (new(1536, 864), 1), (new(1920, 1080), 1.25)];

    private static readonly ItemGrid Grid = ItemGrid.Default();

    /// <summary>The heights, in the card's content DIPs, a resize lands a kind's card on: its least and the next 30 cells.</summary>
    private static IEnumerable<double> Heights(WidgetConfig config, ScreenMetrics screen, double scale)
    {
        var limits = SizeFit.Limits(SizeFit.Of(config), screen, scale)!;
        var (_, rows) = GridGeometry.GridDims(screen);
        var least = (int)Math.Round(limits.Height.Min * rows);
        // A card is whole device pixels (at 100 %): its height in its content DIPs so.
        return Enumerable.Range(least, 31).Select(cells => Math.Round(cells / (double)rows * screen.Height) / scale);
    }

    private static KitPlan Rss(double cardHeight, int lines = 1)
    {
        var config = new RssConfig { Lines = lines, ShowSource = lines >= 2 };
        return KitPlanner.Plan(new WidgetArea(300, cardHeight - FamilySpace.Inset), RssSpec.Spec, ((IKitConfig)config).Choices);
    }

    [Fact]
    public void S14500_a_list_ten_cells_tall_shows_three_rows_of_ten_thirds_with_nothing_left_over()
    {
        // A feed's headlines of one line (28: three cells, their optimal) in a card of ten cells: three, each a third.
        var plan = Rss(100);
        var reach = plan.Reach;

        Assert.Equal(3, reach.Shown);
        Assert.Equal(100.0 / 3, plan.RowHeight, 9);
        Assert.Equal(100, Enumerable.Range(0, 3).Sum(plan.RowAt), 9);
        Assert.True(Enumerable.Range(0, 3).Select(plan.RowAt).Max() - Enumerable.Range(0, 3).Select(plan.RowAt).Min() <= 1);
    }

    [Fact]
    public void S14500_a_list_eleven_cells_tall_shows_three_rows_of_eleven_thirds()
    {
        var plan = Rss(110);

        Assert.Equal(3, plan.Reach.Shown);
        Assert.Equal(110.0 / 3, plan.RowHeight, 9);
        Assert.Equal(110, Enumerable.Range(0, 3).Sum(plan.RowAt), 9);
    }

    [Fact]
    public void S14500_the_forecast_shows_the_days_its_card_holds_each_stretched_less_than_a_day_more()
    {
        foreach (var (screen, scale) in Displays)
        {
            var grid = SizeFit.Grid(screen, scale);
            foreach (var lines in new[] { 1, 2 })
            {
                var config = new WeatherForecastConfig { Lines = lines };
                foreach (var card in Heights(config, screen, scale))
                {
                    // The rows style's RowStack (SpreadStack): a row a day from the card's top edge to its foot.
                    var days = WeatherForecastLayout.DaysIn(new WidgetArea(300, card - FamilySpace.Inset, grid.Pitch), config);
                    var slots = ItemStretch.Reach(card, [.. Enumerable.Repeat(StretchItem.Row(lines), days)], grid);
                    var optimal = grid.Whole(FamilyShape.RowHeight(lines));
                    var why = $"{days} days of {lines} lines in {card:0.##} on {screen.Width}x{screen.Height} at {scale}";

                    Assert.Equal(card, slots[^1].End, 6);
                    Assert.All(slots, s => Assert.True(s.Length >= optimal - 1, why));
                    Assert.True(days == WeatherForecastConfig.MostDays || (days + 1) * optimal > card + 1e-6, why);
                }
            }
        }
    }

    [Fact]
    public void S14500_tasks_rows_and_add_row_fill_the_card_at_every_cell()
    {
        foreach (var (screen, scale) in Displays)
        {
            var grid = SizeFit.Grid(screen, scale);
            foreach (var lines in new[] { 1, 2 })
            {
                var config = new TasksConfig { Lines = lines, ListId = Themia.Core.Tasks.TaskRules.TodayListId };
                foreach (var card in Heights(config, screen, scale))
                {
                    // KitChecks.Plan: the rows over the add row, from the card's top edge (the view less the card's inset).
                    var reach = ItemStretch.RowsAbove(card - FamilySpace.Inset, config.RowsShown.Lines, grid);
                    Assert.Equal(card, reach.Room + reach.Foot, 6);
                    Assert.True(reach.Pitch >= grid.Whole(FamilyShape.RowHeight(config.RowsShown.Lines)) - 1, $"{card:0.##} on {screen.Width}x{screen.Height} at {scale}");
                }
            }
        }
    }

    [Theory]
    [InlineData("cpuSpeed")]
    [InlineData("memoryInUse")]
    [InlineData("driveUsed")]
    [InlineData("sunrise")]
    [InlineData("airUv")]
    [InlineData("weatherPressure")]
    public void S14500_S14600_a_single_metrics_one_row_fills_its_card_at_every_cell(string kind)
    {
        // The facts lists are single metrics ([S-14604]): one fact row a card at its optimal new, stretched to any height.
        foreach (var (screen, scale) in Displays)
        {
            var grid = SizeFit.Grid(screen, scale);
            var config = WidgetConfig.CreateDefault(new WidgetId(kind));
            Assert.Equal(MetricRules.RowHeight, SizeFit.Of(config).Natural.Height);
            foreach (var card in Heights(config, screen, scale))
            {
                // FactList (SpreadStack, its one stated row): from the card's top edge to its foot.
                var slots = ItemStretch.Reach(card, [StretchItem.Row(1)], grid);
                Assert.Equal(card, slots[^1].End, 6);
            }
        }
    }

    [Fact]
    public void S14500_a_ready_made_group_lands_its_forecast_at_its_days_and_still_touching()
    {
        var screen = new ScreenMetrics(1920, 1080);
        var recipe = ReadyMadeGroups.WeatherStation;
        var laid = ReadyMadeGroups.Laid(recipe, screen, new PlacementSizes(1));
        var at = recipe.Widgets.ToList().FindIndex(w => w.Kind == "weatherForecast");

        Assert.True(laid[at].H * 1080 >= WeatherForecastConfig.DefaultDays * 30 - 1e-6);

        // The card under it starts where it ends: no cell between them.
        var under = laid.Where((r, i) => i != at && Math.Abs(r.Y - laid[at].Bottom) < 1e-9).ToList();
        Assert.NotEmpty(under);
    }
}
