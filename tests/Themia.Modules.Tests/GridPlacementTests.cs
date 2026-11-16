using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11807] [S-11808] With the snap grid on, every real kind and every ready-made group lands on whole
/// grid cells, and the desktop then paints it exactly where it landed (placing moves nothing).
/// </summary>
public sealed class GridPlacementTests
{
    /// <summary>A plain 1080p display, 2560 × 1440 at 150 % (171 columns of 9.98 DIPs), a small one.</summary>
    private static readonly ScreenMetrics[] Screens = [new(1920, 1080), new(2560 / 1.5, 1440 / 1.5), new(1280, 720)];

    private static bool Whole(double frac, int lines) => Math.Abs((frac * lines) - Math.Round(frac * lines)) < 1e-6;

    private static void OnLines(FracRect r, ScreenMetrics screen, string what)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        Assert.True(Whole(r.X, cols) && Whole(r.Right, cols), $"{what}: x {r.X * cols:0.###}..{r.Right * cols:0.###} cells on {screen.Width:0}");
        Assert.True(Whole(r.Y, rows) && Whole(r.Bottom, rows), $"{what}: y {r.Y * rows:0.###}..{r.Bottom * rows:0.###} cells on {screen.Height:0}");
    }

    public static TheoryData<string> Kinds()
    {
        var data = new TheoryData<string>();
        foreach (var kind in ModuleCatalog.Widgets)
        {
            data.Add(kind.Id.Value);
        }

        return data;
    }

    public static TheoryData<string> Recipes()
    {
        var data = new TheoryData<string>();
        foreach (var recipe in ReadyMadeGroups.Offered())
        {
            data.Add(recipe.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void S11807_a_new_widget_of_every_kind_lands_on_whole_cells_at_least_its_natural_size(string id)
    {
        var kind = ModuleCatalog.Of(new WidgetId(id));
        foreach (var screen in Screens)
        {
            foreach (var grid in new[] { new PlacementSizes(1), new PlacementSizes(1.25) })
            {
                var plan = PlacementPayload.ForNew(kind.Id, kind.Natural.Width, kind.Natural.Height).Plan(0.4137, 0.3719, screen, grid);
                var widget = Assert.Single(plan.Added);
                var limits = SizeFit.Limits(widget, screen, grid.Scale);
                var shown = SizeFit.Shown(widget.Rect, limits, screen);

                // [S-11875] Every widget, a desktop icon too, lands on whole cells.
                OnLines(widget.Rect, screen, id);
                Assert.Equal(widget.Rect, shown);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S11808_every_ready_made_group_lands_on_whole_cells_touching_as_it_was_and_shown_where_it_landed(string id)
    {
        var recipe = ReadyMadeGroups.Find(id)!;
        foreach (var screen in Screens)
        {
            var plan = PlacementPayload.ForReadyMade(recipe).Plan(0.5, 0.5, screen, new PlacementSizes(1));
            Assert.Equal(recipe.Widgets.Count, plan.Added.Count);

            var rects = plan.Added.Select(w => w.Rect).ToList();
            foreach (var widget in plan.Added)
            {
                OnLines(widget.Rect, screen, $"{id} {widget.Kind.Value}");
                Assert.Equal(widget.Rect, SizeFit.Shown(widget.Rect, SizeFit.Limits(widget, screen, 1), screen));
            }

            // No two overlap, and every pair of boxes the recipe has touching still touches.
            for (var i = 0; i < rects.Count; i++)
            {
                for (var j = i + 1; j < rects.Count; j++)
                {
                    var (a, b) = (rects[i], rects[j]);
                    var overlapW = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
                    var overlapH = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
                    Assert.False(overlapW > 1e-9 && overlapH > 1e-9, $"{id}: {plan.Added[i].Kind.Value} overlaps {plan.Added[j].Kind.Value} on {screen.Width:0}");

                    var (ra, rb) = (recipe.Widgets[i].Box, recipe.Widgets[j].Box);
                    var touchedSide = (Near(ra.X + ra.Width, rb.X) || Near(rb.X + rb.Width, ra.X)) && Math.Min(ra.Y + ra.Height, rb.Y + rb.Height) > Math.Max(ra.Y, rb.Y);
                    var touchedTop = (Near(ra.Y + ra.Height, rb.Y) || Near(rb.Y + rb.Height, ra.Y)) && Math.Min(ra.X + ra.Width, rb.X + rb.Width) > Math.Max(ra.X, rb.X);
                    if (touchedSide)
                    {
                        Assert.True(Near(a.Right, b.X) || Near(b.Right, a.X), $"{id}: {plan.Added[i].Kind.Value} and {plan.Added[j].Kind.Value} no longer touch on {screen.Width:0}");
                    }

                    if (touchedTop)
                    {
                        Assert.True(Near(a.Bottom, b.Y) || Near(b.Bottom, a.Y), $"{id}: {plan.Added[i].Kind.Value} and {plan.Added[j].Kind.Value} no longer touch on {screen.Height:0}");
                    }
                }
            }

            // The ghost is exactly what lands.
            var ghosts = PlacementPayload.ForReadyMade(recipe).Outlines(0.5, 0.5, screen, new PlacementSizes(1));
            Assert.Equal(rects, ghosts.Select(g => g.Rect).ToList());
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S11808_off_the_grid_a_ready_made_group_lands_at_its_recipes_boxes(string id)
    {
        var recipe = ReadyMadeGroups.Find(id)!;
        var screen = Screens[0];
        var plan = PlacementPayload.ForReadyMade(recipe).Plan(0.5, 0.5, screen);

        for (var i = 0; i < plan.Added.Count; i++)
        {
            Assert.Equal(recipe.Widgets[i].Box.Width, plan.Added[i].Rect.W * screen.Width, 6);
            Assert.Equal(recipe.Widgets[i].Box.Height, plan.Added[i].Rect.H * screen.Height, 6);
        }
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;
}
