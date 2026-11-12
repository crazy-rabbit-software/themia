using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1810]..[S-1815] One view per widget kind, holding the widget at its min, natural and max.</summary>
public sealed class ShowcaseViewsTests
{
    private const double Gap = 8;

    /// <summary>1920 × 1080 at 100 %, a 48-DIP taskbar at the bottom.</summary>
    private static readonly ScreenMetrics FullHd = Screen(1920, 1080, new PixelRect(0, 0, 1920, 1032));

    /// <summary>2560 × 1440 at 150 %: 1706.67 × 960 DIPs, taskbar at the top.</summary>
    private static readonly ScreenMetrics QhdAt150 = Screen(2560 / 1.5, 960, new PixelRect(0, 32, 2560 / 1.5, 928));

    /// <summary>640 × 480 with a 40-DIP taskbar: too small for a note's three sizes on one view.</summary>
    private static readonly ScreenMetrics Tiny = Screen(640, 480, new PixelRect(0, 0, 640, 440));

    /// <summary>A kind whose max (600 × 600) is larger than <see cref="Tiny"/>'s work area.</summary>
    private static readonly WidgetId Notes = new("notes");

    private static ScreenMetrics Screen(double width, double height, PixelRect work)
        => new(width, height) { Allowed = GridGeometry.AllowedArea(new PixelRect(0, 0, width, height), work) };

    private static List<ShowcaseKind> AllKinds()
        => [.. ModuleCatalog.Widgets.Select(w => w.Id).ToArray().Select(k => new ShowcaseKind(k, k.ToString(), ""))];

    private static PixelRect Dips(WidgetInstance w, ScreenMetrics screen)
        => new(w.X * screen.Width, w.Y * screen.Height, w.W * screen.Width, w.H * screen.Height);

    private static bool Overlap(PixelRect a, PixelRect b)
        => a.X < b.X + b.Width - 1e-6 && b.X < a.X + a.Width - 1e-6 && a.Y < b.Y + b.Height - 1e-6 && b.Y < a.Y + a.Height - 1e-6;

    public static TheoryData<string> Screens => new() { "fullhd", "qhd150", "tiny" };

    private static ScreenMetrics Named(string name) => name switch
    {
        "fullhd" => FullHd,
        "qhd150" => QhdAt150,
        _ => Tiny,
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
    public void S1810_one_view_per_kind_with_its_min_natural_and_max_at_their_size(string name)
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
            // [S-2448] Every kind holds its min, natural and max.
            var checkedAt = SizeFit.CheckedAt(SizeFit.Of(kinds[k].Kind));
            Assert.Equal(3, view.Widgets.Count);
            Assert.All(view.Widgets, w => Assert.Equal(kinds[k].Kind, w.Kind));

            foreach (var tier in checkedAt)
            {
                var widget = Assert.Single(view.Widgets, w => w.Id == ShowcaseViews.WidgetId(kinds[k].Kind, tier));

                // The painted card is the cell less the gap ([S-04]): exactly the size.
                var painted = GridGeometry.Inset(GridGeometry.ToPixels(widget.Rect, screen), Gap);
                Assert.Equal(tier.Width, painted.Width, 3);
                Assert.Equal(tier.Height, painted.Height, 3);
            }
        }
    }

    [Fact]
    public void S1810_a_content_scale_grows_the_cell_so_the_content_keeps_its_size()
    {
        var view = ShowcaseViews.Build([new(Notes, "Notes", "x")], FullHd, Gap, contentScale: 1.25).First();
        var min = SizeFit.CheckedAt(SizeFit.Of(Notes))[0];
        var cell = Dips(view.Widgets.Single(w => w.Id == ShowcaseViews.WidgetId(Notes, min)), FullHd);

        Assert.Equal(CheckedSize.Min, min.Name);
        Assert.Equal((min.Width * 1.25) + Gap, cell.Width, 3);
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

        foreach (var kind in ModuleCatalog.Widgets.Select(w => w.Id).ToArray())
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
    public void S2448_a_converted_kind_is_shown_at_its_min_natural_and_max()
    {
        var clock = new WidgetId("clock");
        var view = Assert.Single(ShowcaseViews.Build([new(clock, "Clock", "x")], FullHd, Gap));
        var range = SizeFit.CheckedAt(SizeFit.Of(clock));

        Assert.Equal(["min", "natural", "max"], range.Select(t => t.Name));
        Assert.Equal(3, view.Widgets.Count);
        foreach (var tier in range)
        {
            var widget = Assert.Single(view.Widgets, w => w.Id == ShowcaseViews.WidgetId(clock, tier));
            var painted = GridGeometry.Inset(GridGeometry.ToPixels(widget.Rect, FullHd), Gap);
            Assert.Equal(tier.Width, painted.Width, 3);
            Assert.Equal(tier.Height, painted.Height, 3);
        }
    }

    [Fact]
    public void S11601_a_count_stepped_kind_is_held_at_its_min_and_max_with_the_config_each_size_means()
    {
        var facts = new WidgetId("cpuFacts");
        var view = Assert.Single(ShowcaseViews.Build([new(facts, "CPU facts", "x")], FullHd, Gap));

        foreach (var tier in SizeFit.CheckedAt(SizeFit.Of(facts)))
        {
            var widget = Assert.Single(view.Widgets, w => w.Id == ShowcaseViews.WidgetId(facts, tier));
            var laidOut = SizeFit.Of(widget);
            Assert.Equal(tier.Height, laidOut.Fit(new SizeDip(tier.Width, tier.Height)).Height);
        }
    }

    [Fact]
    public void S1812_sizes_that_do_not_fit_one_work_area_continue_on_a_second_view()
    {
        var views = ShowcaseViews.Build([new(Notes, "Notes", "x")], Tiny, Gap);

        Assert.Equal(2, views.Count);
        Assert.Equal([ShowcaseViews.ViewId(Notes), ShowcaseViews.ViewId(Notes, 1)], views.Select(v => v.Id));
        Assert.Equal(["Notes (1)", "Notes (2)"], views.Select(v => v.Name));
        Assert.Equal(3, views.Sum(v => v.Widgets.Count));
        Assert.Equal(3, views.SelectMany(v => v.Widgets).Select(w => w.Id).Distinct().Count());
    }

    [Fact]
    public void S1812_a_size_larger_than_the_work_area_is_shrunk_to_it()
    {
        var views = ShowcaseViews.Build([new(Notes, "Notes", "x")], Tiny, Gap);
        var work = GridGeometry.AllowedPixels(Tiny);

        Assert.Equal(3, views.Sum(v => v.Widgets.Count));
        Assert.All(views.SelectMany(v => v.Widgets), w =>
        {
            var c = Dips(w, Tiny);
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
        var shown = ShowcaseViews.ViewId(Notes);
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
        ShowcaseViews.Replace(state, ShowcaseViews.Build([new(Notes, "Notes", "x")], Tiny, Gap));
        var second = ShowcaseViews.ViewId(Notes, 1);
        state.MonitorViews["display-a"] = second;
        state.ActiveViewId = second;

        // On a larger screen the kind fits one view, so its second page goes.
        ShowcaseViews.Replace(state, ShowcaseViews.Build([new(Notes, "Notes", "x")], FullHd, Gap));

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

    [Fact]
    public void S1815_a_styled_kind_gets_one_view_per_style_each_widget_in_that_style()
    {
        var clock = ModuleCatalog.Widgets.Select(w => w.Id).First(id => WidgetStyles.For(id).Count >= 2);
        var styles = WidgetStyles.For(clock);
        var catalog = ShowcaseKind.Catalog();
        var views = ShowcaseViews.Build(catalog, FullHd, Gap);

        Assert.True(styles.Count >= 2);
        foreach (var style in styles)
        {
            var view = Assert.Single(views, v => v.Id == ShowcaseViews.ViewId(clock, style: style.Name));
            Assert.Contains(style.Label.Text(), view.Name, StringComparison.Ordinal);
            Assert.Equal(3, view.Widgets.Count);
            Assert.All(view.Widgets, w => Assert.Equal(style.Value, WidgetStyles.Current(w.Config)));
        }

        // A kind without styles keeps its one plain view; every id stays unique.
        var plain = ModuleCatalog.Widgets.Select(w => w.Id).First(k => !WidgetStyles.Offers(k));
        Assert.Single(views, v => v.Id == ShowcaseViews.ViewId(plain));
        Assert.Equal(catalog.Count, views.Count);
        var ids = views.SelectMany(v => v.Widgets).Select(w => w.Id).Concat(views.Select(v => v.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
