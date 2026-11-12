using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2444]..[S-2447] The host's side of the sizing contract: limits, the layout's fit, the resize's stops.</summary>
public sealed class SizeFitTests
{
    /// <summary>A 1000 × 1000 DIP screen: a fraction of 0.001 is one DIP, so the arithmetic reads plainly.</summary>
    private static readonly ScreenMetrics Screen = new(1000, 1000);

    private static WidgetSizing Meter => WidgetSizing.Range(new(160, 112), new(220, 144), new(480, 200));

    private static WidgetSizing List(int rows) => WidgetSizing.Range(new(220, 1), new(300, 1), new(520, 1))
        .WithHeightSteps(new SizeSteps(Base: 76, Step: 28, MinCount: 1, MaxCount: 20, Count: rows));

    private static void Near(double expected, double actual) => Assert.Equal(expected, actual, 6);

    [Fact]
    public void S2444_limits_are_the_sizing_times_the_content_scale_plus_the_gap_as_fractions_of_the_cell()
    {
        var limits = SizeFit.Limits(Meter, Screen, scale: 1.5, gapPx: 8)!;

        Near(((160 * 1.5) + 8) / 1000, limits.Width.Min);
        Near(((480 * 1.5) + 8) / 1000, limits.Width.Max);
        Near(((112 * 1.5) + 8) / 1000, limits.Height.Min);
        Assert.False(limits.Height.Stepped);
    }

    [Fact]
    public void S2443_a_legacy_kind_has_no_limits_and_is_shown_as_stored()
    {
        Assert.Null(SizeFit.Limits(WidgetSizing.Legacy(280, 140), Screen, 1, 0));
        var stored = new FracRect(0.9, 0.9, 0.9, 0.01);
        Assert.Equal(stored, SizeFit.Shown(stored, null, Screen));
    }

    [Fact]
    public void S2446_the_painted_box_is_the_stored_rect_fitted_into_the_sizing_at_its_top_left()
    {
        var limits = SizeFit.Limits(Meter, Screen, 1, 0);

        // Too big for this display's DIPs (stored on a larger one): held to Max, the top-left kept.
        var shown = SizeFit.Shown(new FracRect(0.1, 0.2, 0.7, 0.5), limits, Screen);
        Near(0.1, shown.X);
        Near(0.2, shown.Y);
        Near(0.48, shown.W);
        Near(0.2, shown.H);

        // In range: shown exactly as stored.
        var inside = new FracRect(0.1, 0.2, 0.3, 0.15);
        Assert.Equal(inside, SizeFit.Shown(inside, limits, Screen));
    }

    [Fact]
    public void S2446_a_grown_box_that_would_leave_the_work_area_is_kept_in_reach()
    {
        var limits = SizeFit.Limits(Meter, Screen, 1, 0);
        var shown = SizeFit.Shown(new FracRect(0.99, 0.5, 0.01, 0.15), limits, Screen);

        Near(0.16, shown.W);
        Assert.True(shown.X <= 1 - (GridGeometry.MinVisiblePx / 1000) + 1e-9);
    }

    [Fact]
    public void S2446_an_axis_that_follows_the_config_is_laid_out_at_the_configs_count()
    {
        var limits = SizeFit.Limits(List(rows: 6), Screen, 1, 0);
        var shown = SizeFit.Shown(new FracRect(0.1, 0.1, 0.3, 0.6), limits, Screen);

        Near((76 + (6 * 28)) / 1000.0, shown.H);
        Near(0.3, shown.W);
    }

    [Fact]
    public void S2445_a_resized_edge_stops_at_min_and_max_and_the_anchored_edge_stays()
    {
        var limits = SizeFit.Limits(Meter, Screen, 1, 0);
        var start = new FracRect(0.2, 0.2, 0.2, 0.14);

        var grown = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Right, 0.5, 0, Screen), Screen, snapEnabled: true, ResizeHandle.Right, limits);
        Near(0.2, grown.X);
        Near(0.48, grown.W);

        var shrunk = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Left, 0.15, 0, Screen), Screen, snapEnabled: true, ResizeHandle.Left, limits);
        Near(0.4, shrunk.Right);
        Near(0.16, shrunk.W);
    }

    [Fact]
    public void S2445_a_stepped_axis_snaps_to_its_steps_not_the_grid()
    {
        var limits = SizeFit.Limits(List(rows: 6), Screen, 1, 0);
        var start = new FracRect(0.2, 0.1, 0.3, (76 + (6 * 28)) / 1000.0);

        // Dragged down 40 DIPs: one row (28) is nearer than two (56).
        var moved = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Bottom, 0, 0.040, Screen), Screen, snapEnabled: true, ResizeHandle.Bottom, limits);
        Near((76 + (7 * 28)) / 1000.0, moved.H);
        Near(0.1, moved.Y);

        // Dragged far up: one row is the least.
        var least = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Bottom, 0, -0.5, Screen), Screen, snapEnabled: true, ResizeHandle.Bottom, limits);
        Near((76 + 28) / 1000.0, least.H);
    }

    [Fact]
    public void S2445_a_fixed_axis_does_not_move_under_a_corner()
    {
        var face = WidgetSizing.Range(new(200, 100), new(200, 100), new(400, 100));
        var limits = SizeFit.Limits(face, Screen, 1, 0)!;
        Assert.True(limits.Height.Fixed);

        var start = new FracRect(0.2, 0.2, 0.25, 0.1);
        var dragged = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.BottomRight, 0.05, 0.2, Screen), Screen, snapEnabled: false, ResizeHandle.BottomRight, limits);
        Near(0.1, dragged.H);
        Near(0.3, dragged.W);
    }

    [Fact]
    public void S2445_a_key_press_on_a_stepped_axis_is_one_step()
    {
        var limits = SizeFit.Limits(List(rows: 6), Screen, 1, 0);
        var start = new FracRect(0.2, 0.1, 0.3, (76 + (6 * 28)) / 1000.0);

        var grown = KeyboardNudge.Resize(start, NudgeDirection.Down, NudgeStep.Cell, Screen, snapEnabled: true, limits);
        Near((76 + (7 * 28)) / 1000.0, grown.H);
        var shrunk = KeyboardNudge.Resize(start, NudgeDirection.Up, NudgeStep.Cell, Screen, snapEnabled: true, limits);
        Near((76 + (5 * 28)) / 1000.0, shrunk.H);
    }

    [Fact]
    public void S2444_a_fixed_axis_has_no_handle_and_a_corner_moves_the_free_axis_alone()
    {
        Assert.Equal((false, false), EditMarkers.Moves(ResizeHandle.Top, freeWidth: true, freeHeight: false));
        Assert.Equal((true, false), EditMarkers.Moves(ResizeHandle.Right, freeWidth: true, freeHeight: false));
        Assert.Equal((true, false), EditMarkers.Moves(ResizeHandle.BottomRight, freeWidth: true, freeHeight: false));
        Assert.Equal((false, false), EditMarkers.Moves(ResizeHandle.TopLeft, freeWidth: false, freeHeight: false));
        Assert.Equal((true, true), EditMarkers.Moves(ResizeHandle.TopLeft, freeWidth: true, freeHeight: true));
    }

    [Fact]
    public void S2444_a_header_the_kind_did_not_count_adds_its_line_and_one_it_counted_and_hides_takes_it_away()
    {
        var withHeader = SizeFit.ForHeader(Meter, HeaderPlacement.Hidden, HeaderPlacement.Top);
        Assert.Equal(Meter.Min with { Height = 112 + SizeFit.HeaderCost }, withHeader.Min);

        var hidden = SizeFit.ForHeader(List(6), HeaderPlacement.Top, HeaderPlacement.Hidden);
        Assert.Equal(76 - SizeFit.HeaderCost, hidden.HeightSteps!.Base);
        Assert.Equal(76 - SizeFit.HeaderCost + (6 * 28), hidden.Natural.Height);
        Assert.Empty(hidden.Problems());

        var side = SizeFit.ForHeader(Meter, HeaderPlacement.Top, HeaderPlacement.Left);
        Assert.Equal(new SizeDip(160 + SizeFit.EdgeHeaderCost, 112 - SizeFit.HeaderCost), side.Min);
    }

    [Fact]
    public void S5700_a_header_at_the_foot_or_a_side_costs_its_own_inset_too_as_the_view_keeps_its_own()
    {
        // Under a header the content starts at 36; without one above, at the inset the card adds (12).
        Assert.Equal(36, FamilySpace.Inset + SizeFit.HeaderCost);

        // At the foot: the view's own bottom inset, then the strip's gap, line and inset on the edge.
        var foot = SizeFit.ForHeader(Meter, HeaderPlacement.Top, HeaderPlacement.Bottom);
        Assert.Equal(Meter.Min with { Height = 112 - SizeFit.HeaderCost + 4 + 20 + 12 }, foot.Min);
        Assert.Equal(36, SizeFit.EdgeHeaderCost);

        var hidden = SizeFit.ForHeader(Meter, HeaderPlacement.Hidden, HeaderPlacement.Right);
        Assert.Equal(new SizeDip(160 + 36, 112), hidden.Min);
    }

    [Fact]
    public void S2442_the_wanted_size_is_the_cell_less_the_gap_at_widget_scale_one()
    {
        var wanted = SizeFit.Wanted(new FracRect(0, 0, 0.308, 0.158), Screen, scale: 2, gapPx: 8);
        Assert.Equal(150, wanted.Width, 6);
        Assert.Equal(75, wanted.Height, 6);
    }
}
