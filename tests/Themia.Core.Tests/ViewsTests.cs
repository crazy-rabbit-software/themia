using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-900]..[S-916] View operations and the desktop view bar's rules.</summary>
public sealed class ViewsTests
{
    private static AppState ThreeViews()
    {
        var state = new AppState();
        foreach (var name in new[] { "A", "B", "C" })
        {
            var view = ViewState.Create(name);
            view.Id = name.ToLowerInvariant();
            state.Views.Add(view);
        }

        state.ActiveViewId = "a";
        return state;
    }

    private static List<string> Order(AppState state) => [.. state.Views.Select(v => v.Id)];

    // ---- duplicate ----------------------------------------------------------------------------

    [Fact]
    public void S900_duplicate_inserts_a_copy_after_the_source_with_fresh_widget_ids()
    {
        var state = ThreeViews();
        var source = state.Views[0];
        source.Icon = "";
        var clock = WidgetInstance.Create(WidgetKind.Clock, new FracRect(0.1, 0.1, 0.2, 0.2));
        source.Widgets.Add(clock);

        var copy = ViewOps.Duplicate(state, "a");

        Assert.NotNull(copy);
        Assert.Equal(["a", copy!.Id, "b", "c"], Order(state));
        Assert.Equal("A (Copy)", copy.Name);
        Assert.Equal("", copy.Icon);
        Assert.Single(copy.Widgets);
        Assert.NotEqual(clock.Id, copy.Widgets[0].Id);
        Assert.Equal(clock.Rect, copy.Widgets[0].Rect);
        Assert.NotSame(clock.Config, copy.Widgets[0].Config);
        Assert.Single(source.Widgets);
    }

    [Fact]
    public void S900_duplicating_an_unknown_view_changes_nothing()
    {
        var state = ThreeViews();

        Assert.Null(ViewOps.Duplicate(state, "zz"));
        Assert.Equal(["a", "b", "c"], Order(state));
    }

    // ---- reorder ------------------------------------------------------------------------------

    [Theory]
    [InlineData("a", "c", new[] { "b", "c", "a" })]
    [InlineData("c", "a", new[] { "c", "a", "b" })]
    [InlineData("a", "b", new[] { "b", "a", "c" })]
    public void S901_reorder_moves_a_view_into_the_targets_slot(string dragged, string target, string[] expected)
    {
        var state = ThreeViews();

        Assert.True(ViewOps.Reorder(state.Views, dragged, target));
        Assert.Equal(expected, Order(state));
    }

    [Fact]
    public void S901_reorder_is_by_id_so_a_filtered_bar_moves_the_right_view()
    {
        // The bar offers a, c (b hidden on this display); dragging a onto c's slot moves a past b too.
        var state = ThreeViews();

        Assert.True(ViewOps.Reorder(state.Views, "a", "c"));
        Assert.Equal(["b", "c", "a"], Order(state));
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("a", "zz")]
    [InlineData("zz", "a")]
    public void S901_reorder_refuses_the_same_or_unknown_views(string dragged, string target)
    {
        var state = ThreeViews();

        Assert.False(ViewOps.Reorder(state.Views, dragged, target));
        Assert.Equal(["a", "b", "c"], Order(state));
    }

    [Fact]
    public void S901_move_by_one_stops_at_either_end()
    {
        var state = ThreeViews();

        Assert.False(ViewOps.MoveBy(state.Views, "a", -1));
        Assert.False(ViewOps.MoveBy(state.Views, "c", +1));
        Assert.True(ViewOps.MoveBy(state.Views, "a", +1));
        Assert.Equal(["b", "a", "c"], Order(state));
        Assert.True(ViewOps.MoveBy(state.Views, "c", -1));
        Assert.Equal(["b", "c", "a"], Order(state));
    }

    // ---- delete -------------------------------------------------------------------------------

    [Fact]
    public void S902_delete_repairs_everything_that_named_the_view()
    {
        var state = ThreeViews();
        state.ActiveViewId = "b";
        state.DefaultViewForNewDisplays = "b";
        state.MonitorViews["m1"] = "b";
        state.MonitorViews["m2"] = "c";
        ViewBarRules.Update(state, "m1", bar => bar.VisibleViews = ["b"]);
        ViewBarRules.Update(state, "m2", bar => bar.VisibleViews = ["b", "c"]);

        Assert.True(ViewOps.Delete(state, "b"));

        Assert.Equal(["a", "c"], Order(state));
        Assert.Equal("a", state.ActiveViewId);
        Assert.Null(state.DefaultViewForNewDisplays);
        Assert.False(state.MonitorViews.ContainsKey("m1"));
        Assert.Equal("c", state.MonitorViews["m2"]);

        // A whitelist left empty is "all views" again, not an empty bar.
        Assert.Empty(state.ViewBars["m1"].VisibleViews);
        Assert.Equal(2, ViewBarRules.VisibleViews(state.Views, state.ViewBars["m1"]).Count);
        Assert.Equal(["c"], state.ViewBars["m2"].VisibleViews);
    }

    [Fact]
    public void S902_the_last_view_cannot_be_deleted()
    {
        var state = new AppState();
        state.Views.Add(ViewState.Create("Only"));

        Assert.False(ViewOps.Delete(state, state.Views[0].Id));
        Assert.Single(state.Views);
    }

    // ---- shortcut -----------------------------------------------------------------------------

    [Theory]
    [InlineData(1, "a")]
    [InlineData(3, "c")]
    [InlineData(4, null)]
    [InlineData(0, null)]
    [InlineData(10, null)]
    public void S904_ctrl_number_picks_the_nth_view_in_order(int number, string? expected)
        => Assert.Equal(expected, ViewOps.ForShortcut(ThreeViews().Views, number));

    // ---- view bar: settings, visibility, whitelist ----------------------------------------------

    [Fact]
    public void S906_a_display_without_an_entry_gets_the_defaults_and_nothing_is_stored()
    {
        var state = ThreeViews();

        var bar = ViewBarRules.For(state, "m1");

        Assert.Equal(ViewBarAnchor.BottomLeft, bar.Anchor);
        Assert.Equal(ViewBarSize.Medium, bar.Size);
        Assert.False(bar.IconOnly);
        Assert.False(bar.Hidden);
        Assert.Empty(bar.VisibleViews);
        Assert.Empty(state.ViewBars);
    }

    [Fact]
    public void S906_settings_are_per_display()
    {
        var state = ThreeViews();

        ViewBarRules.Update(state, "m1", bar => bar.Anchor = ViewBarAnchor.TopRight);

        Assert.Equal(ViewBarAnchor.TopRight, ViewBarRules.For(state, "m1").Anchor);
        Assert.Equal(ViewBarAnchor.BottomLeft, ViewBarRules.For(state, "m2").Anchor);
    }

    [Fact]
    public void S906_load_repairs_unknown_enums_and_drops_missing_or_repeated_views()
    {
        var state = ThreeViews();
        state.ViewBars["m1"] = new ViewBarSettings
        {
            Anchor = (ViewBarAnchor)99,
            Size = (ViewBarSize)7,
            VisibleViews = ["a", "gone", "a", "c"],
        };
        state.ViewBars[" "] = new ViewBarSettings();

        var repairs = AppStateStore.Validate(state);

        Assert.NotEmpty(repairs);
        Assert.False(state.ViewBars.ContainsKey(" "));
        Assert.Equal(ViewBarAnchor.BottomLeft, state.ViewBars["m1"].Anchor);
        Assert.Equal(ViewBarSize.Medium, state.ViewBars["m1"].Size);
        Assert.Equal(["a", "c"], state.ViewBars["m1"].VisibleViews);
    }

    [Fact]
    public void S906_view_bars_round_trip_through_the_state_file()
    {
        var state = ThreeViews();
        ViewBarRules.Update(state, "m1", bar =>
        {
            bar.Anchor = ViewBarAnchor.RightCenter;
            bar.Size = ViewBarSize.ExtraLarge;
            bar.IconOnly = true;
            bar.Hidden = true;
            bar.VisibleViews = ["b"];
        });

        var json = System.Text.Json.JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        var bar = parsed.State!.ViewBars["m1"];
        Assert.Equal(ViewBarAnchor.RightCenter, bar.Anchor);
        Assert.Equal(ViewBarSize.ExtraLarge, bar.Size);
        Assert.True(bar.IconOnly);
        Assert.True(bar.Hidden);
        Assert.Equal(["b"], bar.VisibleViews);
    }

    [Fact]
    public void S907_outside_edit_mode_one_view_shows_no_bar_unless_picked_on_purpose()
    {
        var state = new AppState();
        state.Views.Add(ViewState.Create("Only"));
        var bar = new ViewBarSettings();

        Assert.Equal(ViewBarShow.None, ViewBarRules.Show(false, bar, state.Views));

        bar.VisibleViews = [state.Views[0].Id];
        Assert.Equal(ViewBarShow.Shown, ViewBarRules.Show(false, bar, state.Views));
    }

    [Fact]
    public void S907_hidden_is_gone_outside_edit_mode_and_a_faded_preview_in_it()
    {
        var state = ThreeViews();
        var bar = new ViewBarSettings();

        Assert.Equal(ViewBarShow.Shown, ViewBarRules.Show(false, bar, state.Views));
        Assert.Equal(ViewBarShow.Shown, ViewBarRules.Show(true, bar, state.Views));

        bar.Hidden = true;
        Assert.Equal(ViewBarShow.None, ViewBarRules.Show(false, bar, state.Views));
        Assert.Equal(ViewBarShow.HiddenPreview, ViewBarRules.Show(true, bar, state.Views));
    }

    [Fact]
    public void S907_edit_mode_shows_the_bar_even_with_one_view()
    {
        var state = new AppState();
        state.Views.Add(ViewState.Create("Only"));

        Assert.Equal(ViewBarShow.Shown, ViewBarRules.Show(true, new ViewBarSettings(), state.Views));
    }

    [Fact]
    public void S908_the_whitelist_filters_in_view_order_and_a_stale_one_offers_all()
    {
        var state = ThreeViews();

        var picked = ViewBarRules.VisibleViews(state.Views, new ViewBarSettings { VisibleViews = ["c", "a"] });
        Assert.Equal(["a", "c"], picked.Select(v => v.Id));

        var stale = ViewBarRules.VisibleViews(state.Views, new ViewBarSettings { VisibleViews = ["gone"] });
        Assert.Equal(["a", "b", "c"], stale.Select(v => v.Id));
    }

    [Fact]
    public void S908_toggling_cannot_remove_the_last_view_and_ticking_all_means_all()
    {
        var state = ThreeViews();
        var bar = new ViewBarSettings();

        bar.VisibleViews = ViewBarRules.ToggleVisible(state.Views, bar, "b", on: false);
        Assert.Equal(["a", "c"], bar.VisibleViews);

        bar.VisibleViews = ViewBarRules.ToggleVisible(state.Views, bar, "a", on: false);
        bar.VisibleViews = ViewBarRules.ToggleVisible(state.Views, bar, "c", on: false);
        Assert.Equal(["c"], bar.VisibleViews);

        bar.VisibleViews = ViewBarRules.ToggleVisible(state.Views, bar, "a", on: true);
        bar.VisibleViews = ViewBarRules.ToggleVisible(state.Views, bar, "b", on: true);
        Assert.Empty(bar.VisibleViews);
    }

    // ---- view bar: placement and dragging -------------------------------------------------------

    /// <summary>A 1000 x 700 work area starting at (0, 40): a taskbar along the top.</summary>
    private static readonly PixelRect Area = new(0, 40, 1000, 700);

    [Theory]
    [InlineData(ViewBarAnchor.TopLeft, 16, 56)]
    [InlineData(ViewBarAnchor.TopCenter, 450, 56)]
    [InlineData(ViewBarAnchor.TopRight, 884, 56)]
    [InlineData(ViewBarAnchor.LeftCenter, 16, 370)]
    [InlineData(ViewBarAnchor.RightCenter, 884, 370)]
    [InlineData(ViewBarAnchor.BottomLeft, 16, 684)]
    [InlineData(ViewBarAnchor.BottomCenter, 450, 684)]
    [InlineData(ViewBarAnchor.BottomRight, 884, 684)]
    public void S909_each_anchor_sits_a_margin_inside_the_work_area(ViewBarAnchor anchor, double x, double y)
    {
        var (px, py) = ViewBarRules.Place(Area, 100, 40, anchor);

        Assert.Equal(x, px, 3);
        Assert.Equal(y, py, 3);
    }

    [Fact]
    public void S909_a_bar_under_the_edit_toolbar_moves_below_it_and_one_beside_it_stays()
    {
        var toolbar = new PixelRect(300, 64, 400, 48);

        var (_, centreY) = ViewBarRules.Place(Area, 100, 40, ViewBarAnchor.TopCenter, toolbar);
        Assert.True(centreY >= toolbar.Y + toolbar.Height, $"top-centre bar at {centreY} still under the toolbar");

        var (_, leftY) = ViewBarRules.Place(Area, 100, 40, ViewBarAnchor.TopLeft, toolbar);
        Assert.Equal(56, leftY, 3);
    }

    [Fact]
    public void S909_a_bar_wider_than_the_area_is_pinned_to_its_top_left_corner()
    {
        var (x, y) = ViewBarRules.Place(new PixelRect(10, 20, 200, 100), 400, 300, ViewBarAnchor.BottomRight);

        Assert.Equal(10, x, 3);
        Assert.Equal(20, y, 3);
    }

    [Theory]
    [InlineData(ViewBarAnchor.LeftCenter, true)]
    [InlineData(ViewBarAnchor.RightCenter, true)]
    [InlineData(ViewBarAnchor.TopCenter, false)]
    [InlineData(ViewBarAnchor.BottomLeft, false)]
    public void S910_only_the_side_middles_are_vertical(ViewBarAnchor anchor, bool vertical)
        => Assert.Equal(vertical, ViewBarRules.IsVertical(anchor));

    [Theory]
    [InlineData(50, 60, ViewBarAnchor.TopLeft)]
    [InlineData(500, 60, ViewBarAnchor.TopCenter)]
    [InlineData(950, 60, ViewBarAnchor.TopRight)]
    [InlineData(50, 390, ViewBarAnchor.LeftCenter)]
    [InlineData(950, 390, ViewBarAnchor.RightCenter)]
    [InlineData(500, 350, ViewBarAnchor.TopCenter)]
    [InlineData(500, 420, ViewBarAnchor.BottomCenter)]
    [InlineData(50, 720, ViewBarAnchor.BottomLeft)]
    [InlineData(500, 720, ViewBarAnchor.BottomCenter)]
    [InlineData(950, 720, ViewBarAnchor.BottomRight)]
    public void S913_a_dragged_bar_drops_on_the_third_the_pointer_is_in(double x, double y, ViewBarAnchor expected)
        => Assert.Equal(expected, ViewBarRules.PickAnchor(Area, x, y));

    [Fact]
    public void S914_the_nearest_chip_centre_wins_even_past_the_ends()
    {
        double[] centres = [20, 80, 140];

        Assert.Equal(0, ViewBarRules.NearestSlot(centres, -50));
        Assert.Equal(1, ViewBarRules.NearestSlot(centres, 100));
        Assert.Equal(2, ViewBarRules.NearestSlot(centres, 500));
        Assert.Equal(-1, ViewBarRules.NearestSlot([], 10));
    }

    [Fact]
    public void S1707_rename_trims_the_name()
    {
        var state = ThreeViews();

        Assert.True(ViewOps.Rename(state, "b", "  Work  "));
        Assert.Equal("Work", state.FindView("b")!.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void S1707_a_blank_name_is_refused_and_the_old_one_kept(string? name)
    {
        var state = ThreeViews();

        Assert.False(ViewOps.Rename(state, "b", name));
        Assert.Equal("B", state.FindView("b")!.Name);
    }

    [Fact]
    public void S1707_the_same_name_or_an_unknown_view_changes_nothing()
    {
        var state = ThreeViews();

        Assert.False(ViewOps.Rename(state, "b", " B "));
        Assert.False(ViewOps.Rename(state, "missing", "New"));
    }
}
