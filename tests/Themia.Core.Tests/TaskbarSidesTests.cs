using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-855] Taskbars on any side, and one view on two displays whose taskbars are on different sides (owner, 2026-11-20:
/// "when i show the same view on two screens it calculates the center without the taskbar this leads to hidden
/// widgets on both screens. (happens on left and right taskbars like i have it.)"). His displays: two 2560 × 1440
/// panels at 150 % (1706.7 × 960 DIPs), the taskbar 48 DIPs (72 pixels) wide on the left of one and on the right of
/// the other. [S-6608] A gesture still takes a widget over a taskbar's edge, keeping the visible minimum (owner,
/// 2026-11-21: "the widgets should be able to go over the screen"); placement and a change of the area keep it clear.
/// </summary>
public sealed class TaskbarSidesTests
{
    private sealed class Card : WidgetConfig
    {
        public override WidgetConfig Clone() => new Card();
    }

    private static readonly DeviceRect Monitor = new(0, 0, 2560, 1440);

    /// <summary>The taskbar's width as a fraction of the panel's.</summary>
    private const double T = 72.0 / 2560;

    private const double Eps = 1e-9;

    private static ScreenMetrics Screen(DeviceRect work)
        => new(2560 / 1.5, 1440 / 1.5) { Allowed = GridGeometry.AllowedArea(Monitor, work) };

    private static readonly ScreenMetrics LeftBar = Screen(new DeviceRect(72, 0, 2488, 1440));
    private static readonly ScreenMetrics RightBar = Screen(new DeviceRect(0, 0, 2488, 1440));
    private static readonly ScreenMetrics TopBar = Screen(new DeviceRect(0, 72, 2560, 1368));
    private static readonly ScreenMetrics BottomBar = Screen(new DeviceRect(0, 0, 2560, 1368));
    private static readonly ScreenMetrics AutoHide = Screen(Monitor);

    /// <summary>The view's area on both of the owner's displays, as the host hands it to a gesture on either.</summary>
    private static ScreenMetrics Both(ScreenMetrics on) => on with { Allowed = GridGeometry.SharedAllowed([LeftBar.Allowed, RightBar.Allowed]) };

    /// <summary>Whether a stored rect is wholly inside a display's own work area.</summary>
    private static bool Inside(FracRect r, ScreenMetrics display)
    {
        var a = display.Allowed;
        return r.X >= a.X - Eps && r.Right <= a.Right + Eps && r.Y >= a.Y - Eps && r.Bottom <= a.Bottom + Eps;
    }

    private static WidgetInstance Widget(string id, FracRect rect) => new() { Id = id, Config = new Card(), Rect = rect };

    /// <summary>How much of a stored rect a display's work area shows on each axis, in DIPs.</summary>
    private static (double X, double Y) Visible(FracRect r, ScreenMetrics display)
    {
        var a = display.Allowed;
        return (
            (Math.Min(r.Right, a.Right) - Math.Max(r.X, a.X)) * display.Width,
            (Math.Min(r.Bottom, a.Bottom) - Math.Max(r.Y, a.Y)) * display.Height);
    }

    // ---- the area a view shown on several displays takes new geometry in ------------------------------------

    [Fact]
    public void S855_a_view_on_two_displays_takes_new_geometry_where_both_work_areas_hold_it()
    {
        var shared = GridGeometry.SharedAllowed([LeftBar.Allowed, RightBar.Allowed]);

        Assert.Equal(T, shared.X, 9);
        Assert.Equal(1 - T, shared.Right, 9);
        Assert.Equal((0.0, 1.0), (shared.Y, shared.Bottom));
        Assert.Equal(LeftBar.Allowed, GridGeometry.SharedAllowed([LeftBar.Allowed]));
        Assert.Equal(FracRect.Full, GridGeometry.SharedAllowed([]));

        // A top and a bottom taskbar: the band between them.
        var tall = GridGeometry.SharedAllowed([TopBar.Allowed, BottomBar.Allowed]);
        Assert.Equal(T * 2560 / 1440, tall.Y, 9);
        Assert.Equal(1368.0 / 1440, tall.Bottom, 9);
    }

    [Fact]
    public void S855_areas_that_share_nothing_give_the_first_never_an_empty_area()
        => Assert.Equal(new FracRect(0, 0, 0.4, 1), GridGeometry.SharedAllowed([new FracRect(0, 0, 0.4, 1), new FracRect(0.6, 0, 0.4, 1)]));

    [Theory]
    [InlineData(0.995)]
    [InlineData(0.005)]
    public void S855_a_widget_placed_at_either_displays_far_edge_lands_clear_of_both_taskbars(double x)
    {
        foreach (var display in new[] { LeftBar, RightBar })
        {
            var placed = GridGeometry.PlaceAt(x, 0.5, 300, 200, Both(display));

            Assert.True(Inside(placed, LeftBar), $"{placed} under the left display's taskbar");
            Assert.True(Inside(placed, RightBar), $"{placed} under the right display's taskbar");
        }
    }

    [Fact]
    public void S855_placed_at_a_work_areas_centre_a_widget_lands_centred_there()
    {
        foreach (var display in new[] { LeftBar, RightBar })
        {
            var a = display.Allowed;
            var centre = a.X + (a.W / 2);
            var placed = GridGeometry.PlaceAt(centre, 0.5, 300, 200, Both(display));

            // On the grid ([S-06]): within a cell of the work area's centre, not the monitor's.
            var cell = 1.0 / GridGeometry.GridDims(display).Cols;
            Assert.InRange(placed.X + (placed.W / 2), centre - cell, centre + cell);
            Assert.True(Math.Abs(placed.X + (placed.W / 2) - 0.5) > cell, "centred on the monitor, not the work area");
        }
    }

    [Fact]
    public void S6608_S855_a_block_dragged_on_one_display_goes_over_the_other_displays_taskbar_to_the_visible_minimum()
    {
        // Owner, 2026-11-21: "the widgets should be able to go over the screen".
        FracRect[] start = [new(0.6, 0.4, 0.15, 0.2), new(0.75, 0.4, 0.15, 0.2)];
        var screen = Both(LeftBar);

        var delta = BlockGeometry.Move(start, 0.5, 0, screen);
        var right = BlockGeometry.Apply(start[1], delta, screen);

        Assert.True(right.Right > 1 - T, $"{right} stopped at the taskbar");
        Assert.Equal(GridGeometry.MinVisibleDip, Visible(right, screen).X, 6);
        Assert.Equal(0.15, right.X - BlockGeometry.Apply(start[0], delta, screen).X, 9);
    }

    [Fact]
    public void S6609_S855_a_block_dragged_just_past_a_taskbars_edge_lands_flush_on_it()
    {
        FracRect[] start = [new(0.6, 0.4, 0.15, 0.2), new(0.75, 0.4, 0.15, 0.2)];
        var screen = Both(LeftBar);

        // The block's right edge 5 DIPs past the right display's taskbar: inside the magnet.
        var delta = BlockGeometry.Move(start, 1 - T - 0.9 + (5 / screen.Width), 0, screen);

        Assert.Equal(1 - T, BlockGeometry.Apply(start[1], delta, screen).Right, 9);
    }

    // ---- a gesture goes over a taskbar's edge as over the monitor's, keeping the visible minimum --------------------

    public static TheoryData<string, ScreenMetrics, FracRect> PushedOntoTaskbars => new()
    {
        { "left", LeftBar, new FracRect(-2, 0.4, 0.15, 0.2) },
        { "right", RightBar, new FracRect(2, 0.4, 0.15, 0.2) },
        { "top", TopBar, new FracRect(0.4, -2, 0.15, 0.2) },
        { "bottom", BottomBar, new FracRect(0.4, 2, 0.15, 0.2) },
    };

    [Theory]
    [MemberData(nameof(PushedOntoTaskbars))]
    public void S6608_S855_a_widget_pushed_onto_a_taskbar_on_any_side_goes_behind_it_keeping_the_visible_minimum(string side, ScreenMetrics screen, FracRect pushed)
    {
        var settled = GridGeometry.Settle(pushed, screen);

        Assert.False(Inside(settled, screen), $"{side}: {settled} stopped at the taskbar");
        var (x, y) = Visible(settled, screen);
        Assert.Equal(GridGeometry.MinVisibleDip, side is "left" or "right" ? x : y, 6);
        var snapped = GridGeometry.Snap(pushed, screen);
        Assert.Equal((snapped.W, snapped.H), (settled.W, settled.H));

        // [S-11] [S-855] Placed there instead, it lands wholly clear of the taskbar.
        Assert.True(Inside(GridGeometry.Land(settled, screen), screen), $"{side}: placed under the taskbar");
    }

    [Fact]
    public void S855_S6608_over_the_edge_still_hangs_a_widget_over_the_monitors_own_edge()
    {
        // A bottom taskbar: the top, left and right are the monitor's edges.
        var settled = GridGeometry.Settle(new FracRect(0.4, -2, 0.15, 0.2), BottomBar);
        Assert.Equal((GridGeometry.MinVisibleDip / BottomBar.Height) - settled.H, settled.Y, 9);

        // An auto-hidden taskbar reserves nothing: every edge is the monitor's.
        Assert.True(GridGeometry.Strips(AutoHide.Allowed).None);
        Assert.True(GridGeometry.Settle(new FracRect(0.4, 2, 0.15, 0.2), AutoHide).Bottom > 1);
    }

    [Fact]
    public void S6608_S855_a_resized_edge_passes_a_taskbars_edge_as_the_monitors()
    {
        var start = new FracRect(0.4, 0.4, 0.2, 0.2);

        var overBar = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Left, -0.6, 0, LeftBar), LeftBar, ResizeHandle.Left);
        Assert.True(overBar.X < T, $"{overBar} stopped at the taskbar");
        Assert.Equal(start.Right, overBar.Right, 12);

        var overEdge = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Right, 0.8, 0, LeftBar), LeftBar, ResizeHandle.Right);
        Assert.True(overEdge.Right > 1);
    }

    // ---- a view laid on no work area yet is settled clear of every taskbar of the displays showing it -------------

    /// <summary>
    /// The owner's view as laid out on one display or the other: flush with the left display's taskbar and the right
    /// monitor edge, flush with the left monitor edge and the right display's taskbar, a joined pair at the right
    /// monitor edge with a neighbour 20 DIPs before it, a clock at the left display's work area's centre.
    /// </summary>
    private static AppState Owners()
    {
        var state = new AppState();
        var view = ViewState.Create("Home");
        view.Id = "home";
        view.Widgets.AddRange(
        [
            Widget("flush-left-taskbar", new FracRect(T, 0.10, 0.1, 0.12)),
            Widget("flush-left-edge", new FracRect(0, 0.30, 0.1, 0.12)),
            Widget("flush-right-edge", new FracRect(0.9, 0.10, 0.1, 0.12)),
            Widget("flush-right-taskbar", new FracRect(0.9 - T, 0.30, 0.1, 0.12)),
            Widget("neighbour", new FracRect(0.7 - (20 / LeftBar.Width), 0.55, 0.1, 0.12)),
            Widget("pair-a", new FracRect(0.8, 0.55, 0.1, 0.12)),
            Widget("pair-b", new FracRect(0.9, 0.55, 0.1, 0.12)),
            Widget("centred", new FracRect(T + ((1 - T - 0.1) / 2), 0.75, 0.1, 0.12)),
        ]);
        state.Views.Add(view);
        return state;
    }

    private static IReadOnlyList<SurfaceMetrics> OwnersDisplays(bool settled = true)
        => [new SurfaceMetrics("home", LeftBar, 1, settled), new SurfaceMetrics("home", RightBar, 1, Settled: true)];

    [Fact]
    public void S855_one_view_on_two_displays_ends_with_every_widget_inside_each_work_area()
    {
        var state = Owners();
        var before = state.Views[0].Widgets.ToDictionary(w => w.Id, w => w.Rect);
        Assert.True(WorkAreaSettle.Waits(state, OwnersDisplays()));

        var moved = WorkAreaSettle.Apply(state, OwnersDisplays());

        Assert.True(moved > 0);
        foreach (var widget in state.Views[0].Widgets)
        {
            Assert.True(Inside(widget.Rect, LeftBar), $"{widget.Id} {widget.Rect} under the left display's taskbar");
            Assert.True(Inside(widget.Rect, RightBar), $"{widget.Id} {widget.Rect} under the right display's taskbar");
            Assert.Equal((before[widget.Id].W, before[widget.Id].H), (widget.Rect.W, widget.Rect.H));
        }

        // Nothing clear of both was moved; the pair still meets; the neighbour it came over moved with it, its gap kept.
        var at = state.Views[0].Widgets.ToDictionary(w => w.Id, w => w.Rect);
        Assert.Equal(before["flush-left-taskbar"], at["flush-left-taskbar"]);
        Assert.Equal(before["centred"], at["centred"]);
        Assert.Equal(at["pair-a"].Right, at["pair-b"].X, 12);
        Assert.Equal(before["pair-b"].X - before["neighbour"].Right, at["pair-b"].X - at["neighbour"].Right, 9);
        Assert.False(WorkAreaSettle.Waits(state, OwnersDisplays()));
        Assert.Equal(0, WorkAreaSettle.Apply(state, OwnersDisplays()));
    }

    [Fact]
    public void S855_a_layout_made_against_one_work_area_slides_whole_onto_the_other_where_it_fits()
    {
        // Laid out on the display whose taskbar is on the right: from the left monitor edge to 600 DIPs in, gaps and joins.
        var state = new AppState();
        var view = ViewState.Create("Work");
        view.Id = "work";
        var rects = new[] { new FracRect(0, 0.1, 0.1, 0.2), new FracRect(0.1, 0.1, 0.1, 0.2), new FracRect(0.25, 0.1, 0.1, 0.2), new FracRect(0, 0.5, 0.35, 0.3) };
        view.Widgets.AddRange(rects.Select((r, i) => Widget("w" + i, r)));
        state.Views.Add(view);

        // Now shown on the left-taskbar display too.
        WorkAreaSettle.Apply(state, [new SurfaceMetrics("work", RightBar, 1, true), new SurfaceMetrics("work", LeftBar, 1, true)]);

        for (var i = 0; i < rects.Length; i++)
        {
            Assert.Equal(rects[i].X + T, view.Widgets[i].Rect.X, 9);
            Assert.Equal(rects[i].Y, view.Widgets[i].Rect.Y, 12);
        }
    }

    [Fact]
    public void S855_a_widget_a_taskbar_grew_over_on_one_display_moves_clear_of_it()
    {
        var state = new AppState();
        var view = ViewState.Create("Home");
        view.Id = "home";
        view.Widgets.Add(Widget("low", new FracRect(0.3, 0.9, 0.2, 0.1)));
        view.Widgets.Add(Widget("over-the-top", new FracRect(0.6, -0.05, 0.2, 0.1)));
        state.Views.Add(view);

        WorkAreaSettle.Apply(state, [new SurfaceMetrics("home", BottomBar, 1, true)]);

        Assert.Equal(BottomBar.Allowed.Bottom, view.Widgets[0].Rect.Bottom, 9);
        Assert.Equal(0.3, view.Widgets[0].Rect.X, 12);

        // [S-6608] Hanging over the monitor's own top edge is the user's: left as it is.
        Assert.Equal(new FracRect(0.6, -0.05, 0.2, 0.1), view.Widgets[1].Rect);
    }

    [Fact]
    public void S855_S854_a_view_one_of_whose_displays_has_not_settled_waits()
    {
        var state = Owners();
        var before = state.Views[0].Widgets.Select(w => w.Rect).ToList();

        Assert.Equal(0, WorkAreaSettle.Apply(state, OwnersDisplays(settled: false)));
        Assert.Equal(before, state.Views[0].Widgets.Select(w => w.Rect).ToList());
    }

    [Fact]
    public void S855_S11887_desktop_icons_are_left_to_their_own_settle()
    {
        var state = Owners();
        var icon = new WidgetInstance { Id = "icon", Config = new DeskIconFake { Item = @"C:\Users\a\Desktop\a.txt" }, Rect = new FracRect(0, 0.9, 0.04, 0.08) };
        state.Views[0].Widgets.Add(icon);

        WorkAreaSettle.Apply(state, OwnersDisplays());

        Assert.Equal(new FracRect(0, 0.9, 0.04, 0.08), icon.Rect);
    }

    [Fact]
    public void S855_a_set_wider_than_the_shared_room_is_kept_in_it_widget_by_widget()
    {
        // Two joined widgets as wide as the whole monitor: no one translation holds them.
        IReadOnlyList<FracRect> rects = [new FracRect(0, 0.1, 0.5, 0.2), new FracRect(0.5, 0.1, 0.5, 0.2)];

        var settled = WorkAreaSettle.Settle(rects, FracRect.Full, Both(LeftBar));

        Assert.True(settled.All(r => Inside(r, LeftBar) && Inside(r, RightBar)), string.Join(" ", settled));
    }

    // ---- what the user hung over a taskbar stays; what a change of the area would newly hide moves clear -----------

    [Fact]
    public void S6608_S855_a_widget_hung_over_a_taskbar_stays_there_through_every_settle()
    {
        // Owner, 2026-11-21: "the widgets should be able to go over the screen". Laid on both displays, then dragged
        // partly over the left display's taskbar and partly over the right display's.
        var state = new AppState();
        var view = ViewState.Create("Home");
        view.Id = "home";
        view.WorkArea = Both(LeftBar).Allowed;
        var overLeft = new FracRect(T - 0.03, 0.2, 0.1, 0.12);
        var overRight = new FracRect(1 - T - 0.07, 0.5, 0.1, 0.12);
        view.Widgets.AddRange([Widget("over-left", overLeft), Widget("over-right", overRight)]);
        state.Views.Add(view);

        Assert.False(WorkAreaSettle.Waits(state, OwnersDisplays()));
        Assert.Equal(0, WorkAreaSettle.Apply(state, OwnersDisplays()));
        Assert.Equal(0, WorkAreaSettle.Apply(state, OwnersDisplays()));

        Assert.Equal(overLeft, view.Widgets[0].Rect);
        Assert.Equal(overRight, view.Widgets[1].Rect);
    }

    [Fact]
    public void S6608_S855_one_wholly_behind_a_taskbar_slides_out_only_to_the_visible_minimum()
    {
        var state = new AppState();
        var view = ViewState.Create("Home");
        view.Id = "home";
        view.WorkArea = Both(LeftBar).Allowed;
        view.Widgets.Add(Widget("hidden", new FracRect(-0.05, 0.5, 0.06, 0.1)));
        state.Views.Add(view);

        Assert.True(WorkAreaSettle.Waits(state, OwnersDisplays()));
        Assert.Equal(1, WorkAreaSettle.Apply(state, OwnersDisplays()));

        var r = view.Widgets[0].Rect;
        Assert.Equal(GridGeometry.MinVisibleDip, Visible(r, Both(LeftBar)).X, 6);
        Assert.True(r.X < T, "pulled wholly clear: only the visible minimum is the settle's");
        Assert.Equal((0.06, 0.5), (r.W, r.Y));
    }

    [Fact]
    public void S855_a_view_shown_on_another_display_moves_what_its_taskbar_would_hide_and_keeps_the_users_overhang()
    {
        // Laid on the left display alone (its taskbar on the left): one widget hung over that taskbar by the user, one
        // flush with the monitor's right edge, a pair joined there, one in the middle.
        var state = new AppState();
        var view = ViewState.Create("Home");
        view.Id = "home";
        view.WorkArea = LeftBar.Allowed;
        var hung = new FracRect(T - 0.02, 0.1, 0.1, 0.12);
        var middle = new FracRect(0.45, 0.75, 0.1, 0.12);
        view.Widgets.AddRange(
        [
            Widget("hung-over-left-taskbar", hung),
            Widget("flush-right-edge", new FracRect(0.9, 0.1, 0.1, 0.12)),
            Widget("pair-a", new FracRect(0.8, 0.4, 0.1, 0.12)),
            Widget("pair-b", new FracRect(0.9, 0.4, 0.1, 0.12)),
            Widget("middle", middle),
        ]);
        state.Views.Add(view);

        // Now shown on the right display too, whose taskbar is on the right.
        Assert.True(WorkAreaSettle.Waits(state, OwnersDisplays()));
        WorkAreaSettle.Apply(state, OwnersDisplays());

        var at = view.Widgets.ToDictionary(w => w.Id, w => w.Rect);
        Assert.Equal(1 - T, at["flush-right-edge"].Right, 9);
        Assert.Equal(1 - T, at["pair-b"].Right, 9);
        Assert.Equal(at["pair-a"].Right, at["pair-b"].X, 12);
        Assert.Equal(hung, at["hung-over-left-taskbar"]);
        Assert.Equal(middle, at["middle"]);
        Assert.True(WorkAreaSettle.Same(Both(LeftBar).Allowed, view.WorkArea!.Value));
        Assert.False(WorkAreaSettle.Waits(state, OwnersDisplays()));
    }

    [Fact]
    public void S855_a_taskbar_that_grew_moves_what_lay_clear_of_it_and_keeps_what_hung_over_it()
    {
        var state = new AppState();
        var view = ViewState.Create("Home");
        view.Id = "home";
        view.WorkArea = Screen(new DeviceRect(0, 0, 2560, 1392)).Allowed;
        var hung = new FracRect(0.6, 0.9, 0.2, 0.1);
        view.Widgets.AddRange([Widget("flush", new FracRect(0.3, (1392.0 / 1440) - 0.1, 0.2, 0.1)), Widget("hung", hung)]);
        state.Views.Add(view);

        // The taskbar grew from 48 to 72 pixels.
        WorkAreaSettle.Apply(state, [new SurfaceMetrics("home", BottomBar, 1, true)]);

        Assert.Equal(BottomBar.Allowed.Bottom, view.Widgets[0].Rect.Bottom, 9);
        Assert.Equal(hung, view.Widgets[1].Rect);
    }

    [Fact]
    public void S855_the_area_a_view_is_laid_on_is_remembered_and_a_new_one_alone_waits()
    {
        var state = new AppState();
        var view = ViewState.Create("Home");
        view.Id = "home";
        var middle = new FracRect(0.45, 0.4, 0.1, 0.12);
        view.Widgets.Add(Widget("middle", middle));
        state.Views.Add(view);

        Assert.True(WorkAreaSettle.Waits(state, OwnersDisplays()));
        Assert.Equal(0, WorkAreaSettle.Apply(state, OwnersDisplays()));
        Assert.True(WorkAreaSettle.Same(Both(LeftBar).Allowed, view.WorkArea!.Value));
        Assert.Equal(middle, view.Widgets[0].Rect);
        Assert.False(WorkAreaSettle.Waits(state, OwnersDisplays()));

        // Shown on the left display alone: another area, remembered.
        Assert.True(WorkAreaSettle.Waits(state, [new SurfaceMetrics("home", LeftBar, 1, true)]));
        WorkAreaSettle.Apply(state, [new SurfaceMetrics("home", LeftBar, 1, true)]);
        Assert.True(WorkAreaSettle.Same(LeftBar.Allowed, view.WorkArea!.Value));
    }

    [Fact]
    public void S855_a_work_area_that_is_no_part_of_the_screen_is_repaired_to_none_loudly()
    {
        var view = ViewState.Create("Home");
        view.WorkArea = new FracRect(double.NaN, 0, 1, 1);
        Assert.Contains(view.Repair(), line => line.Contains("work area", StringComparison.Ordinal));
        Assert.Null(view.WorkArea);

        view.WorkArea = new FracRect(T, 0, 1 - T, 1);
        Assert.DoesNotContain(view.Repair(), line => line.Contains("work area", StringComparison.Ordinal));
        Assert.Equal(new FracRect(T, 0, 1 - T, 1), view.WorkArea);
    }
}
