using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Rss;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-14810] The kinds whose content is repeated items, at every cell (owner, 2026-11-20: "the widget items should actually
/// have a fixed size. then when you change the height of a widget the items should not grow or shrink to fit all the way
/// from the top to the bottom, they should just keep their size and the last item will be cutoff to some extent then."):
/// at any height a resize lands on, on displays of several pitches, the items the view lays (the one rule,
/// <see cref="ItemSize"/>) keep their one size from the card's top edge, as many as the card reaches.
/// </summary>
public sealed class FixedItemKindsTests
{
    /// <summary>A 1080p display, 1920 at 125 % (pitch 9.974 across, 10.047 down) and a content scale of 1.25 (pitch 8).</summary>
    private static readonly (ScreenMetrics Screen, double Scale)[] Displays = [(new(1920, 1080), 1), (new(1536, 864), 1), (new(1920, 1080), 1.25)];

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
    public void S14810_a_list_ten_cells_tall_shows_three_rows_of_three_cells_and_a_third_of_a_fourth()
    {
        // A feed's headlines of one line (28: three cells, their optimal) in a card of ten cells: four, each 30, the fourth
        // cut by the card's foot after its first cell.
        var plan = Rss(100);

        Assert.Equal(4, plan.Reach.Shown);
        Assert.Equal(30, plan.RowHeight, 9);
        Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(30, plan.RowAt(i), 9));
        Assert.Equal(90, plan.LineStart(3), 9);
    }

    [Fact]
    public void S14810_one_cell_taller_or_shorter_never_resizes_a_row()
    {
        foreach (var card in new[] { 90.0, 100, 110, 120, 170 })
        {
            Assert.Equal(30, Rss(card).RowHeight, 9);
            Assert.Equal(40, Rss(card, 2).RowHeight, 9);
        }
    }

    [Fact]
    public void S14810_the_forecast_shows_every_day_its_card_reaches_each_at_its_size()
    {
        foreach (var (screen, scale) in Displays)
        {
            var grid = SizeFit.Grid(screen, scale);
            foreach (var hidden in new[] { new[] { Themia.Sdk.Items.ItemPart.Subtitle }, [] })
            {
                var config = new WeatherForecastConfig { HiddenParts = [.. hidden] };
                foreach (var card in Heights(config, screen, scale))
                {
                    // [S-16974] The table: a row a day from the card's top edge, at its optimal, the last cut by the foot.
                    var shape = ForecastItems.Shape(config.Style, config.HiddenParts, config.ShowPrecipitation);
                    var lay = Themia.Sdk.Items.ItemLayout.Lay(Themia.Sdk.Items.ItemStyle.Table, WeatherForecastConfig.MostDays, 300, card, 0, shape, ForecastItems.Cells, grid);
                    var optimal = grid.Whole(FamilyShape.RowHeight(1));
                    var why = $"{lay.Boxes.Count} days in {card:0.##} on {screen.Width}x{screen.Height} at {scale}";

                    Assert.All(lay.Boxes, b => Assert.True(Math.Abs(b.Height - optimal) <= 1, why));
                    Assert.True(lay.Boxes.Count == WeatherForecastConfig.MostDays || lay.Boxes[^1].Bottom >= card - 1, why);
                    Assert.True(lay.Boxes[^1].Y < card, why);
                }
            }
        }
    }

    [Fact]
    public void S14811_tasks_rows_and_add_row_keep_their_size_at_every_cell()
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
                    var reach = ItemSize.RowsAbove(card - FamilySpace.Inset, config.RowsShown.Lines, grid);
                    var why = $"{card:0.##} on {screen.Width}x{screen.Height} at {scale}";
                    Assert.Equal(grid.Whole(FamilyShape.RowHeight(config.RowsShown.Lines)), reach.Pitch, 6);
                    Assert.Equal(grid.Whole(FamilyShape.Row1), reach.Foot, 6);
                    Assert.Equal(card, reach.Room + reach.Foot, 6);
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
    public void S14600_a_single_metric_is_one_row_new(string kind)
    {
        // A single metric is one item, its whole face ([S-14705]): its natural is one fact row at its optimal.
        var config = WidgetConfig.CreateDefault(new WidgetId(kind));
        Assert.Equal(MetricRules.RowHeight, SizeFit.Of(config).Natural.Height);
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
