using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-14100] The kinds whose content is repeated rows are exactly as tall as their whole rows (owner, 2026-11-20: "now
/// the weather forecast widget always has one block space at the bottom. this should obviously not be"; "same for the
/// task widget, there is also a one block space at the bottom now. why is that?"): at every height a resize may land on,
/// on displays of several pitches, the rows the view lays (the one rule, <see cref="ItemStretch"/>) end on the card's foot.
/// </summary>
public sealed class NoLeftoverTests
{
    /// <summary>A 1080p display, 1920 at 125 % (pitch 9.974 across, 10.047 down) and a content scale of 1.25 (pitch 8).</summary>
    private static readonly (ScreenMetrics Screen, double Scale)[] Displays = [(new(1920, 1080), 1), (new(1536, 864), 1), (new(1920, 1080), 1.25)];

    /// <summary>The card's height, in its content DIPs, a stepped axis gives <paramref name="config"/> on a display.</summary>
    private static double CardHeight(WidgetConfig config, ScreenMetrics screen, double scale)
    {
        var limits = SizeFit.Limits(SizeFit.Of(config), screen, scale)!;
        return limits.Height.Pinned!.Value * screen.Height / scale;
    }

    /// <summary>What the screen's rows and columns differ by over a card: under one cell, never a cell.</summary>
    private static void Exactly(double card, double laid, ItemGrid grid, string what)
        => Assert.True(Math.Abs(card - laid) < grid.Pitch - 1e-6, $"{what}: {laid:0.##} laid of a {card:0.##} card ({(card - laid) / grid.Pitch:0.##} cells left over)");

    [Fact]
    public void S14100_the_forecast_is_exactly_its_days_at_every_count_and_lines()
    {
        foreach (var (screen, scale) in Displays)
        {
            var grid = SizeFit.Grid(screen, scale);
            for (var lines = 1; lines <= 2; lines++)
            {
                for (var days = 1; days <= 14; days++)
                {
                    var config = new WeatherForecastConfig { Days = days, Lines = lines };
                    var card = CardHeight(config, screen, scale);

                    // The rows style's RowStack (SpreadStack): a row a day from the card's top edge, growing to the room.
                    var slots = ItemStretch.Reach(card, [.. Enumerable.Repeat(StretchItem.Row(lines), days)], grid);
                    Assert.Equal(grid.Whole(FamilyShape.RowHeight(lines)), slots[0].Length, 6);
                    Exactly(card, slots[^1].End, grid, $"{days} days of {lines} lines on {screen.Width}x{screen.Height} at {scale}");
                }
            }
        }
    }

    [Fact]
    public void S14100_a_resize_of_the_forecast_lands_on_whole_days_and_stores_them()
    {
        var screen = new ScreenMetrics(1920, 1080);
        var config = new WeatherForecastConfig { Days = 5 };
        var limits = SizeFit.Limits(SizeFit.Of(config), screen, 1)!;

        // Dragged to 8.4 rows' height: the eight days its whole cells hold.
        var cell = new FracRect(0.1, 0.1, 300 / 1920.0, (8.4 * 30) / 1080);
        var next = Assert.IsType<WeatherForecastConfig>(SizeFit.ConfigFor(config, cell, screen, 1, limits));
        Assert.Equal(8, next.Days);
        Assert.Equal(240 / 1080.0, SizeFit.Limits(SizeFit.Of(next), screen, 1)!.Height.Pinned!.Value, 9);
    }

    [Fact]
    public void S14100_tasks_are_their_rows_and_the_add_row_at_every_count()
    {
        foreach (var (screen, scale) in Displays)
        {
            var grid = SizeFit.Grid(screen, scale);
            foreach (var lines in new[] { 1, 2 })
            {
                for (var rows = KitSizing.MinRows; rows <= KitSizing.MaxRows; rows++)
                {
                    var config = new TasksConfig { Rows = rows, Lines = lines };
                    var card = CardHeight(config, screen, scale);

                    // KitChecks.Plan: the rows over the add row, from the card's top edge (the view less the card's inset).
                    var choice = config.RowsShown;
                    var reach = ItemStretch.RowsAbove(card - FamilySpace.Inset, choice.Rows, choice.Lines, grid);
                    Assert.Equal(choice.Rows, reach.Shown);
                    Exactly(card, reach.Length(choice.Rows) + grid.Whole(FamilyShape.Row1), grid, $"{rows} tasks of {lines} lines on {screen.Width}x{screen.Height} at {scale}");
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
    public void S14100_S14600_a_single_metric_is_exactly_its_one_row(string kind)
    {
        // The facts lists are single metrics now ([S-14604]): one fact row a card, its height that row's whole cells.
        foreach (var (screen, scale) in Displays)
        {
            var grid = SizeFit.Grid(screen, scale);
            var sizing = SizeFit.Of(WidgetConfig.CreateDefault(new WidgetId(kind)));
            Assert.IsType<WholeCells>(sizing.HeightSteps!.Cells);
            var limits = SizeFit.Limits(sizing, screen, scale)!;
            Assert.True(limits.Height.Fixed, $"{kind}: its height is its one row");
            var card = limits.Height.Min * screen.Height / scale;

            // FactList (SpreadStack): its one row from the card's top edge.
            var slots = ItemStretch.Reach(card, [StretchItem.Row(1)], grid);
            Exactly(card, slots[^1].End, grid, $"{kind} on {screen.Width}x{screen.Height} at {scale}");
        }
    }

    [Theory]
    [InlineData("rss")]
    [InlineData("email")]
    [InlineData("bluetooth")]
    [InlineData("mixer")]
    [InlineData("contacts")]
    [InlineData("screenTimeApps")]
    public void S14100_every_height_step_of_a_list_is_its_parts_and_whole_rows(string kind)
    {
        foreach (var (screen, scale) in Displays)
        {
            var grid = SizeFit.Grid(screen, scale);
            var sizing = SizeFit.Of(WidgetConfig.CreateDefault(new WidgetId(kind)));
            var steps = sizing.HeightSteps!;
            var cells = Assert.IsType<WholeCells>(steps.Cells);
            var limits = SizeFit.Limits(sizing, screen, scale)!;
            var (_, lines) = GridGeometry.GridDims(screen);

            // Its parts and its rows, each its own whole cells on this display's grid, and nothing else.
            var (over, row) = cells.Count(grid);
            Assert.Equal(over + (steps.Count!.Value * row), limits.Height.Pinned!.Value * lines, 6);
            Assert.Equal(grid.Cells(cells.Item), row);
        }
    }

    [Fact]
    public void S14100_a_ready_made_group_lands_its_forecast_exactly_its_days_and_still_touching()
    {
        var screen = new ScreenMetrics(1920, 1080);
        var recipe = ReadyMadeGroups.WeatherStation;
        var laid = ReadyMadeGroups.Laid(recipe, screen, new PlacementSizes(1));
        var at = recipe.Widgets.ToList().FindIndex(w => w.Kind == "weatherForecast");
        var days = new WeatherForecastConfig().Days;

        Assert.Equal(days * 30 / 1080.0, laid[at].H, 9);

        // The card under it starts where it ends: no cell between them.
        var under = laid.Where((r, i) => i != at && Math.Abs(r.Y - laid[at].Bottom) < 1e-9).ToList();
        Assert.NotEmpty(under);
    }

    [Fact]
    public void S14103_a_forecast_stored_a_cell_too_tall_is_fitted_to_its_days_on_refit()
    {
        var screen = new ScreenMetrics(1920, 1080);

        // Stored as the old sizing painted it: 164 rounded up to 170, two cells under its five rows of 30.
        var forecast = WidgetInstance.Create(new WidgetId("weatherForecast"), new FracRect(0.2, 0.2, 300 / 1920.0, 170 / 1080.0));
        Assert.True(SizeFit.Refit(forecast, screen, 1));
        Assert.Equal(150 / 1080.0, forecast.Rect.H, 9);
        Assert.Equal(0.2, forecast.Rect.Y, 9);
    }
}
