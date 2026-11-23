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

    private static void Near(double expected, double actual) => Assert.Equal(expected, actual, 6);


    [Fact]
    public void S2444_a_screen_not_measured_yet_has_no_limits_and_shows_the_rect_as_stored()
    {
        Assert.Null(SizeFit.Limits(WidgetSizing.Range(new(32, 32), new(280, 140), new(560, 420)), new ScreenMetrics(0, 0), 1));
        var stored = new FracRect(0.9, 0.9, 0.9, 0.01);
        Assert.Equal(stored, SizeFit.Shown(stored, null, Screen));
    }

    [Fact]
    public void S2446_the_painted_box_is_the_stored_rect_fitted_into_the_sizing_at_its_top_left()
    {
        var limits = SizeFit.Limits(Meter, Screen, 1);

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
        var limits = SizeFit.Limits(Meter, Screen, 1);
        var shown = SizeFit.Shown(new FracRect(0.99, 0.5, 0.01, 0.15), limits, Screen);

        Near(0.16, shown.W);
        Assert.True(shown.X <= 1 - (GridGeometry.MinVisibleDip / 1000) + 1e-9);
    }


    [Fact]
    public void S2445_a_resized_edge_stops_at_min_and_max_and_the_anchored_edge_stays()
    {
        var limits = SizeFit.Limits(Meter, Screen, 1);
        var start = new FracRect(0.2, 0.2, 0.2, 0.14);

        var grown = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Right, 0.5, 0, Screen), Screen, ResizeHandle.Right, limits);
        Near(0.2, grown.X);
        Near(0.48, grown.W);

        var shrunk = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Left, 0.15, 0, Screen), Screen, ResizeHandle.Left, limits);
        Near(0.4, shrunk.Right);
        Near(0.16, shrunk.W);
    }


    [Fact]
    public void S2445_a_fixed_axis_does_not_move_under_a_corner()
    {
        var face = WidgetSizing.Range(new(200, 100), new(200, 100), new(400, 100));
        var limits = SizeFit.Limits(face, Screen, 1)!;
        Assert.True(limits.Height.Fixed);

        var start = new FracRect(0.2, 0.2, 0.25, 0.1);
        var dragged = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.BottomRight, 0.05, 0.2, Screen), Screen, ResizeHandle.BottomRight, limits);
        Near(0.1, dragged.H);
        Near(0.3, dragged.W);
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

    /// <summary>[S-17100] A one-readout face of 200 × 100 (its parts 176 × 76 in the inset), relaxed as the descriptor hands it out.</summary>
    private static SizeLimits FaceLimits
        => SizeFit.Limits(SizeStretch.Relax(WidgetSizing.Range(new(48, 32), new(200, 100), new(400, 100)).ScalesAsOne()), Screen, 1)!;

    /// <summary>The face's most height at a width, in DIPs, on the 10-DIP grid: the whole cells that hold it.</summary>
    private static double FaceMost(double width) => Math.Ceiling((24 + ((width - 24) * 76 / 176.0)) / 10 - 1e-9) / 100;

    [Fact]
    public void S17100_a_face_is_painted_no_taller_than_its_shape_at_its_width()
    {
        // Owner, 2026-11-23: "the height resize should be limited to not exceed where the spacings at the bottom or top are not
        // the same as they are in the other widgets too": a stored rect taller than that (placed before) is shown at it.
        var shown = SizeFit.Shown(new FracRect(0.1, 0.2, 0.3, 0.4), FaceLimits, Screen);
        Near(0.3, shown.W);
        Near(FaceMost(300), shown.H);
        Near(0.15, shown.H);

        var inside = new FracRect(0.1, 0.2, 0.3, 0.12);
        Assert.Equal(inside, SizeFit.Shown(inside, FaceLimits, Screen));
    }

    [Fact]
    public void S17100_a_resize_stops_the_height_at_the_shape_and_a_narrower_card_follows_it_down()
    {
        var start = new FracRect(0.2, 0.2, 0.3, 0.1);
        var taller = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Bottom, 0, 0.5, Screen), Screen, ResizeHandle.Bottom, FaceLimits);
        Near(0.15, taller.H);
        Near(0.2, taller.Y);

        // A corner grows both: the height stops where the new width puts it.
        var corner = GridGeometry.Settle(GridGeometry.Resize(new FracRect(0.2, 0.2, 0.2, 0.1), ResizeHandle.BottomRight, 0.18, 0.5, Screen), Screen, ResizeHandle.BottomRight, FaceLimits);
        Near(0.38, corner.W);
        Near(FaceMost(380), corner.H);

        // The width made narrower takes the height down with it, its top kept.
        var narrower = GridGeometry.Settle(GridGeometry.Resize(taller, ResizeHandle.Right, -0.1, 0, Screen), Screen, ResizeHandle.Right, FaceLimits);
        Near(0.2, narrower.W);
        Near(0.1, narrower.H);
        Near(0.2, narrower.Y);

        // Shift+Down at the shape's height moves nothing.
        Assert.Equal(taller, KeyboardNudge.Resize(taller, NudgeDirection.Down, NudgeStep.Cell, Screen, FaceLimits));
    }

    [Fact]
    public void S17100_a_block_laid_out_holds_each_face_to_its_shape_at_its_width()
    {
        var laid = GridFit.Layout([new FracRect(0.1, 0.1, 0.3, 0.5)], [FaceLimits], Screen);
        Near(0.3, laid[0].W);
        Near(0.15, laid[0].H);
    }

    [Fact]
    public void S14500_a_resized_edge_lands_on_every_whole_cell_between_min_and_max()
    {
        // Owner, 2026-11-20: "the widgets should not have the size increments anymore but should be incrementable in steps
        // of one". A list's range: a row at least, twenty at most; its bottom edge stops on each cell between.
        var list = SizeFit.Limits(WidgetSizing.Range(new(220, 30), new(300, 180), new(520, 600)), Screen, 1);
        var start = new FracRect(0.2, 0.2, 0.3, 0.18);

        foreach (var cells in new[] { 3, 4, 10, 11, 13, 17, 59, 60 })
        {
            var asked = (cells / 100.0) - 0.18;
            var landed = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Bottom, 0, asked, Screen), Screen, ResizeHandle.Bottom, list);
            Near(cells / 100.0, landed.H);
        }
    }
}
