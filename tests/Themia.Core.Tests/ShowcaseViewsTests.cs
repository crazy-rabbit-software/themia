using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1810]..[S-1814] One view per widget kind, holding the widget at every size tier.</summary>
public sealed class ShowcaseViewsTests
{
    private const double Gap = 8;

    /// <summary>1920 × 1080 at 100 %, a 48-DIP taskbar at the bottom.</summary>
    private static readonly ScreenMetrics FullHd = Screen(1920, 1080, new PixelRect(0, 0, 1920, 1032));

    /// <summary>2560 × 1440 at 150 %: 1706.67 × 960 DIPs, taskbar at the top.</summary>
    private static readonly ScreenMetrics QhdAt150 = Screen(2560 / 1.5, 960, new PixelRect(0, 32, 2560 / 1.5, 928));

    /// <summary>1920 × 1080 at 125 %: 1536 × 864 DIPs, too small for every tier on one view.</summary>
    private static readonly ScreenMetrics FullHdAt125 = Screen(1536, 864, new PixelRect(0, 0, 1536, 816));

    private static ScreenMetrics Screen(double width, double height, PixelRect work)
        => new(width, height) { Allowed = GridGeometry.AllowedArea(new PixelRect(0, 0, width, height), work) };

    private static List<ShowcaseKind> AllKinds()
        => [.. Enum.GetValues<WidgetKind>().Select(k => new ShowcaseKind(k, k.ToString(), ""))];

    private static PixelRect Dips(WidgetInstance w, ScreenMetrics screen)
        => new(w.X * screen.Width, w.Y * screen.Height, w.W * screen.Width, w.H * screen.Height);

    private static bool Overlap(PixelRect a, PixelRect b)
        => a.X < b.X + b.Width - 1e-6 && b.X < a.X + a.Width - 1e-6 && a.Y < b.Y + b.Height - 1e-6 && b.Y < a.Y + a.Height - 1e-6;

    public static TheoryData<string> Screens => new() { "fullhd", "qhd150", "fullhd125" };

    private static ScreenMetrics Named(string name) => name switch
    {
        "fullhd" => FullHd,
        "qhd150" => QhdAt150,
        _ => FullHdAt125,
    };

    [Theory]
    [MemberData(nameof(Screens))]
    public void S1810_no_two_widgets_of_a_view_overlap_and_all_sit_inside_the_work_area(string name)
    {
        var screen = Named(name);
        var work = GridGeometry.AllowedPixels(screen);

        foreach (var view in ShowcaseViews.Build(AllKinds(), screen, Gap))
        {
            var cells = view.Widgets.Select(w => Dips(w, screen)).ToList();
            for (var i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                Assert.True(c.X >= work.X - 1e-6 && c.Y >= work.Y - 1e-6, $"{view.Id}: cell {i} starts outside the work area");
                Assert.True(c.X + c.Width <= work.X + work.Width + 1e-6 && c.Y + c.Height <= work.Y + work.Height + 1e-6, $"{view.Id}: cell {i} ends outside the work area");
                for (var j = i + 1; j < cells.Count; j++)
                {
                    Assert.False(Overlap(c, cells[j]), $"{view.Id}: cells {i} and {j} overlap");
                }
            }

            Assert.All(view.Widgets, w => Assert.True(w.X >= 0 && w.Y >= 0 && w.X + w.W <= 1 + 1e-9 && w.Y + w.H <= 1 + 1e-9));
        }
    }

    [Theory]
    [InlineData("fullhd")]
    [InlineData("qhd150")]
    public void S1810_one_view_per_kind_with_all_seven_tiers_at_their_size(string name)
    {
        var screen = Named(name);
        var kinds = AllKinds();
        var views = ShowcaseViews.Build(kinds, screen, Gap);

        Assert.Equal(kinds.Count, views.Count);
        for (var k = 0; k < kinds.Count; k++)
        {
            var view = views[k];
            Assert.Equal(ShowcaseViews.ViewId(kinds[k].Kind), view.Id);
            Assert.Equal(kinds[k].Name, view.Name);
            Assert.Equal(kinds[k].Glyph, view.Icon);
            Assert.Equal(7, view.Widgets.Count);
            Assert.All(view.Widgets, w => Assert.Equal(kinds[k].Kind, w.Kind));

            foreach (var tier in WidgetTiers.All)
            {
                var widget = Assert.Single(view.Widgets, w => w.Id == ShowcaseViews.WidgetId(kinds[k].Kind, tier));

                // The painted card is the cell less the gap ([S-04]): exactly the tier.
                var painted = GridGeometry.Inset(GridGeometry.ToPixels(widget.Rect, screen), Gap);
                Assert.Equal(tier.Width, painted.Width, 3);
                Assert.Equal(tier.Height, painted.Height, 3);
            }
        }
    }

    [Fact]
    public void S1810_a_content_scale_grows_the_cell_so_the_content_keeps_its_tier()
    {
        var view = ShowcaseViews.Build([new(WidgetKind.Clock, "Clock", "x")], FullHd, Gap, contentScale: 1.25).First();
        var xs = view.Widgets.Single(w => w.Id == ShowcaseViews.WidgetId(WidgetKind.Clock, WidgetTiers.All[0]));
        var cell = Dips(xs, FullHd);

        Assert.Equal((120 * 1.25) + Gap, cell.Width, 3);
    }

    [Fact]
    public void S1810_widgets_are_default_instances_and_each_view_holds_only_its_kind()
    {
        var views = ShowcaseViews.Build(AllKinds(), FullHd, Gap);

        foreach (var widget in views.SelectMany(v => v.Widgets))
        {
            Assert.Equal(WidgetConfig.CreateDefault(widget.Kind).GetType(), widget.Config.GetType());
            Assert.Equal(WidgetInstance.DefaultHeader(widget.Kind), widget.Header);
        }

        foreach (var kind in Enum.GetValues<WidgetKind>())
        {
            var view = views.Single(v => v.Id == ShowcaseViews.ViewId(kind));
            Assert.All(view.Widgets, w => Assert.Equal(kind, w.Kind));
        }
    }

    [Fact]
    public void S1811_ids_are_deterministic_and_unique()
    {
        var first = ShowcaseViews.Build(AllKinds(), FullHd, Gap);
        var second = ShowcaseViews.Build(AllKinds(), FullHd, Gap);

        Assert.Equal(first.Select(v => v.Id), second.Select(v => v.Id));
        Assert.Equal(first.SelectMany(v => v.Widgets).Select(w => w.Id), second.SelectMany(v => v.Widgets).Select(w => w.Id));

        var widgetIds = first.SelectMany(v => v.Widgets).Select(w => w.Id).ToList();
        Assert.Equal(widgetIds.Count, widgetIds.Distinct().Count());
        Assert.Equal(first.Count, first.Select(v => v.Id).Distinct().Count());
        Assert.All(first, v => Assert.True(ShowcaseViews.IsShowcase(v)));
    }

    [Fact]
    public void S1812_tiers_that_do_not_fit_one_work_area_continue_on_a_second_view()
    {
        var views = ShowcaseViews.Build([new(WidgetKind.Clock, "Clock", "x")], FullHdAt125, Gap);

        Assert.Equal(2, views.Count);
        Assert.Equal(["showcase-clock", "showcase-clock-2"], views.Select(v => v.Id));
        Assert.Equal(["Clock (1)", "Clock (2)"], views.Select(v => v.Name));
        Assert.Equal(7, views.Sum(v => v.Widgets.Count));
        Assert.Equal(7, views.SelectMany(v => v.Widgets).Select(w => w.Id).Distinct().Count());
    }

    [Fact]
    public void S1812_a_tier_larger_than_the_work_area_is_shrunk_to_it()
    {
        var tiny = Screen(640, 480, new PixelRect(0, 0, 640, 440));
        var views = ShowcaseViews.Build([new(WidgetKind.Clock, "Clock", "x")], tiny, Gap);
        var work = GridGeometry.AllowedPixels(tiny);

        Assert.Equal(7, views.Sum(v => v.Widgets.Count));
        Assert.All(views.SelectMany(v => v.Widgets), w =>
        {
            var c = Dips(w, tiny);
            Assert.True(c.X + c.Width <= work.X + work.Width + 1e-6 && c.Y + c.Height <= work.Y + work.Height + 1e-6);
        });
    }

    [Fact]
    public void S1813_a_rerun_replaces_the_showcase_instead_of_adding_another()
    {
        var state = AppState.CreateDefault();
        var own = state.Views.Select(v => v.Id).ToList();
        var kinds = AllKinds();

        Assert.Equal(0, ShowcaseViews.Replace(state, ShowcaseViews.Build(kinds, FullHd, Gap)));
        var shown = ShowcaseViews.ViewId(WidgetKind.Clock);
        state.MonitorViews["display-a"] = shown;

        Assert.Equal(kinds.Count, ShowcaseViews.Replace(state, ShowcaseViews.Build(kinds, FullHd, Gap)));

        Assert.Equal(own.Count + kinds.Count, state.Views.Count);
        Assert.Equal(own, state.Views.Take(own.Count).Select(v => v.Id));
        Assert.Equal(state.Views.Count, state.Views.Select(v => v.Id).Distinct().Count());

        // A display showing a showcase view still shows it: the id came back.
        Assert.Equal(shown, state.MonitorViews["display-a"]);
        Assert.Empty(AppStateStore.Validate(state));
    }

    [Fact]
    public void S1813_a_showcase_view_that_does_not_come_back_is_deleted_with_its_references()
    {
        var state = AppState.CreateDefault();
        ShowcaseViews.Replace(state, ShowcaseViews.Build([new(WidgetKind.Clock, "Clock", "x")], FullHdAt125, Gap));
        var second = ShowcaseViews.ViewId(WidgetKind.Clock, 1);
        state.MonitorViews["display-a"] = second;
        state.ActiveViewId = second;

        // On a larger screen the clock fits one view, so its second page goes.
        ShowcaseViews.Replace(state, ShowcaseViews.Build([new(WidgetKind.Clock, "Clock", "x")], FullHd, Gap));

        Assert.Null(state.FindView(second));
        Assert.False(state.MonitorViews.ContainsKey("display-a"));
        Assert.NotNull(state.FindView(state.ActiveViewId));
        Assert.Single(state.Views, ShowcaseViews.IsShowcase);
    }

    [Fact]
    public void S1813_a_saved_and_reloaded_showcase_needs_no_repair()
    {
        var state = AppState.CreateDefault();
        ShowcaseViews.Replace(state, ShowcaseViews.Build(AllKinds(), QhdAt150, Gap));

        var json = System.Text.Json.JsonSerializer.Serialize(state, StateJson.AppState);
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);
        Assert.Equal(state.Views.Count, parsed.State!.Views.Count);
    }
}
