using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-13018] A lasso over empty desktop names an area: when it is offered, the area on the grid, what a fence there takes.</summary>
public sealed class LassoAreaTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private static readonly ScreenMetrics Screen = new(1000, 800);

    private static WidgetInstance Icon(string name, FracRect rect) => new() { Id = name, Config = new DeskIconFake { Item = Desk + name }, Rect = rect };

    [Fact]
    public void S13018_only_a_lasso_that_touched_nothing_and_spans_two_cells_each_way_offers_its_area()
    {
        var (cols, rows) = GridGeometry.GridDims(Screen);
        var cellW = 1.0 / cols;
        var cellH = 1.0 / rows;

        var band = new FracRect(0.105, 0.103, 3.4 * cellW, 2.5 * cellH);
        var area = LassoArea.Offered(band, caught: 0, Screen);
        Assert.NotNull(area);

        // Snapped out to whole cells: it holds the band, its corner on a grid line.
        var a = area!.Value;
        Assert.True(a.X <= band.X + 1e-9 && a.Y <= band.Y + 1e-9);
        Assert.True(a.X + a.W >= band.X + band.W - 1e-9 && a.Y + a.H >= band.Y + band.H - 1e-9);
        Assert.Equal(Math.Round(a.X * cols), a.X * cols, 6);
        Assert.Equal(Math.Round(a.W * cols), a.W * cols, 6);

        // A lasso that caught a widget is a selection; one under two cells is a press that slipped.
        Assert.Null(LassoArea.Offered(band, caught: 1, Screen));
        Assert.Null(LassoArea.Offered(band with { W = 1.5 * cellW }, caught: 0, Screen));
        Assert.Null(LassoArea.Offered(band with { H = 1.5 * cellH }, caught: 0, Screen));
        Assert.Null(LassoArea.Offered(band, caught: 0, new ScreenMetrics(0, 0)));
    }

    [Fact]
    public void S13018_a_fence_made_on_the_area_takes_the_free_icons_whose_middle_is_inside_it()
    {
        var view = ViewState.Create("Home");
        view.Widgets.Add(Icon("in.txt", new FracRect(0.21, 0.21, 0.04, 0.05)));
        view.Widgets.Add(Icon("edge.txt", new FracRect(0.37, 0.21, 0.04, 0.05)));
        view.Widgets.Add(Icon("out.txt", new FracRect(0.6, 0.6, 0.04, 0.05)));
        view.Widgets.Add(new WidgetInstance { Id = "card", Config = new DeskIconFake { Target = @"C:\Tools\app.exe" }, Rect = new FracRect(0.22, 0.3, 0.04, 0.05) });

        var inside = LassoArea.IconsInside(view, new FracRect(0.2, 0.2, 0.2, 0.2));

        Assert.Equal([Desk + "in.txt", Desk + "edge.txt"], inside);
    }
}
