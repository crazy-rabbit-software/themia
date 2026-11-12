using Themia.Core.Astronomy;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Turning altitudes into pixels, and the moon disc. [S-11] [S-12] [S-13]
/// <para>
/// Pure geometry, so it is tested here rather than in the view: the widget only hands these points
/// to shapes.
/// </para>
/// </summary>
public class SunPathGeometryTests
{
    /// <summary>The plot the original's tests used, so the ported expectations carry over unchanged.</summary>
    private static readonly SunPathPlot Plot = new(
        X0: 22,
        X1: 178,
        HorizonY: 116,
        AboveHeight: 96,
        AxisMaxDegrees: 66,
        BelowHeight: 24,
        NightDepthDegrees: 27);

    [Fact]
    public void S_11_The_horizon_lands_on_the_horizon_at_either_end()
    {
        Assert.Equal(new PlotPoint(Plot.X0, Plot.HorizonY), Plot.PointAt(new SkySample(0, 0)));
        Assert.Equal(new PlotPoint(Plot.X1, Plot.HorizonY), Plot.PointAt(new SkySample(1, 0)));
    }

    [Fact]
    public void S_11_Progress_is_spaced_evenly_across_the_width()
    {
        Assert.Equal((Plot.X0 + Plot.X1) / 2, Plot.PointAt(new SkySample(0.5, 0)).X, 9);
        Assert.Equal(Plot.X0 + ((Plot.X1 - Plot.X0) * 0.25), Plot.PointAt(new SkySample(0.25, 0)).X, 9);
    }

    [Fact]
    public void S_11_Altitude_scales_linearly_up_to_the_top_of_the_axis()
    {
        Assert.Equal(Plot.HorizonY - Plot.AboveHeight, Plot.PointAt(new SkySample(0.5, Plot.AxisMaxDegrees)).Y, 9);
        Assert.Equal(Plot.HorizonY - (Plot.AboveHeight / 2), Plot.PointAt(new SkySample(0.5, Plot.AxisMaxDegrees / 2)).Y, 9);
    }

    [Fact]
    public void S_11_A_body_that_beats_the_axis_is_clamped_rather_than_drawn_off_the_top()
        => Assert.Equal(Plot.HorizonY - Plot.AboveHeight, Plot.PointAt(new SkySample(0.5, 89)).Y, 9);

    /// <summary>
    /// The night band is normalised against the nadir rather than the daylight axis: to scale, a
    /// winter midnight would be deeper than the whole widget. [S-12]
    /// </summary>
    [Fact]
    public void S_12_Negative_altitude_goes_into_the_compressed_night_band()
    {
        Assert.Equal(Plot.HorizonY + Plot.BelowHeight, Plot.PointAt(new SkySample(0.5, -Plot.NightDepthDegrees)).Y, 9);
        Assert.Equal(Plot.HorizonY + (Plot.BelowHeight / 2), Plot.PointAt(new SkySample(0.5, -Plot.NightDepthDegrees / 2)).Y, 9);
    }

    [Fact]
    public void S_11_A_point_never_leaves_the_plot_box_whatever_it_is_handed()
    {
        foreach (var altitude in new[] { -90.0, -40, -1, 0, 1, 40, 90, 200 })
        {
            foreach (var progress in new[] { -2.0, 0, 0.37, 1, 3 })
            {
                var point = Plot.PointAt(new SkySample(progress, altitude));

                Assert.InRange(point.X, Plot.X0, Plot.X1);
                Assert.InRange(point.Y, Plot.HorizonY - Plot.AboveHeight, Plot.HorizonY + Plot.BelowHeight);
            }
        }
    }

    [Fact]
    public void S_11_The_curve_runs_through_every_reading_in_order()
    {
        var points = SunPathGeometry.Curve(Samples(), Plot);

        Assert.Equal(3, points.Count);
        Assert.True(points[1].Y < points[0].Y); // higher altitude, smaller y
        Assert.True(points[0].X < points[2].X);
    }

    [Fact]
    public void S_11_The_elapsed_daylight_area_closes_back_along_the_horizon()
    {
        var points = SunPathGeometry.Area(Samples(), Plot);

        Assert.Equal(5, points.Count);
        Assert.Equal(Plot.HorizonY, points[^1].Y, 9);
        Assert.Equal(Plot.HorizonY, points[^2].Y, 9);
        Assert.Equal(points[0].X, points[^1].X, 9);
        Assert.Equal(points[2].X, points[^2].X, 9);
    }

    [Fact]
    public void S_11_Nothing_to_draw_produces_nothing_rather_than_a_broken_shape()
    {
        Assert.Empty(SunPathGeometry.Curve([], Plot));
        Assert.Empty(SunPathGeometry.Area([], Plot));
        Assert.Empty(SunPathGeometry.Area([new SkySample(0, 0)], Plot));
    }

    /// <summary>
    /// The layout the view asks for at its current size. The curve is inset to the 13% and 87%
    /// gridlines because the number strip is centred on the same anchors. [S-16]
    /// </summary>
    [Fact]
    public void S_11_The_plot_anchors_the_ends_on_the_label_gridlines()
    {
        var plot = SunPathPlot.For(200, 150, 47.37, 15.6);

        Assert.Equal(26, plot.X0, 9);
        Assert.Equal(174, plot.X1, 9);
        Assert.True(plot.HorizonY < 150, "the horizon must leave room for the night band");
        Assert.True(plot.HorizonY - plot.AboveHeight > 0, "the curve must stay inside the drawing");
    }

    [Fact]
    public void S11806_the_axis_tops_out_at_the_days_own_noon_so_its_curve_uses_the_height()
    {
        Assert.Equal(90 - (47.37 - 15.6), SunPathPlot.For(200, 150, 47.37, 15.6).AxisMaxDegrees, 9);   // Zurich, late summer
        Assert.Equal(90 - (47.37 + 18), SunPathPlot.For(200, 150, 47.37, -18).AxisMaxDegrees, 9);      // Zurich, November
        Assert.Equal(90, SunPathPlot.For(200, 150, 0, 0).AxisMaxDegrees, 9);                           // the tropics cap at overhead
        Assert.Equal(1, SunPathPlot.For(200, 150, 78, -23).AxisMaxDegrees, 9);                         // a polar night: no arc to scale

        // Noon of any day stands just under the box's top, whatever the season: no empty sky over a winter arc.
        foreach (var (latitude, declination) in new[] { (47.37, 23.4), (47.37, -23.4), (69.6, 23.4), (69.6, -5), (-33.9, -23.4) })
        {
            var plot = SunPathPlot.For(256, 128, latitude, declination);
            var noon = plot.PointAt(new SkySample(0.5, plot.AxisMaxDegrees));
            Assert.Equal(SunPathPlot.MarkerHalf, noon.Y, 9);
        }
    }

    [Fact]
    public void S_11_A_degenerate_surface_still_produces_a_usable_plot()
    {
        var plot = SunPathPlot.For(0, 0, 47.37, 15.6);

        Assert.True(plot.AboveHeight > 0);
        Assert.True(plot.NightDepthDegrees > 0);
        Assert.True(double.IsFinite(plot.PointAt(new SkySample(0.5, 10)).Y));
    }

    [Fact]
    public void S_13_The_moon_is_empty_at_new_and_a_full_disc_at_full()
    {
        var (newMin, newMax) = Span(MoonDisc.LitLimb(0, 0, 10, 0));
        Assert.Equal(0, newMin, 2);
        Assert.Equal(10, newMax, 2);

        var (fullMin, fullMax) = Span(MoonDisc.LitLimb(0, 0, 10, 0.5));
        Assert.Equal(-10, fullMin, 2);
        Assert.Equal(10, fullMax, 2);
    }

    [Fact]
    public void S_13_The_right_half_is_lit_at_first_quarter_and_the_left_at_last()
    {
        var (firstMin, firstMax) = Span(MoonDisc.LitLimb(0, 0, 10, 0.25));
        Assert.Equal(0, firstMin, 2);
        Assert.Equal(10, firstMax, 2);

        var (lastMin, lastMax) = Span(MoonDisc.LitLimb(0, 0, 10, 0.75));
        Assert.Equal(-10, lastMin, 2);
        Assert.Equal(0, lastMax, 2);
    }

    /// <summary>
    /// A waxing moon is lit on the right from Zurich and on the left from Sydney. The original's
    /// issue #105 came from a southern-hemisphere user, so this is not a detail to get wrong.
    /// [S-13]
    /// </summary>
    [Fact]
    public void S_13_The_disc_is_mirrored_below_the_equator()
    {
        var (northMin, northMax) = Span(MoonDisc.LitLimb(0, 0, 10, 0.25));
        var (southMin, southMax) = Span(MoonDisc.LitLimb(0, 0, 10, 0.25, southern: true));

        Assert.Equal(-northMax, southMin, 4);
        Assert.Equal(-northMin, southMax, 4);
    }

    [Fact]
    public void S_13_The_terminator_bows_toward_the_lit_edge_for_a_crescent_and_past_centre_for_a_gibbous()
    {
        var (crescentLimb, crescentTerminator) = Widest(MoonDisc.LitLimb(0, 0, 10, 0.1));
        Assert.Equal(10, crescentLimb, 2);
        Assert.True(crescentTerminator > 0, $"a crescent's terminator bowed to {crescentTerminator:F2}");

        var (gibbousLimb, gibbousTerminator) = Widest(MoonDisc.LitLimb(0, 0, 10, 0.4));
        Assert.Equal(10, gibbousLimb, 2);
        Assert.True(gibbousTerminator < 0, $"a gibbous terminator bowed to {gibbousTerminator:F2}");
    }

    [Fact]
    public void S_13_Every_phase_stays_inside_the_disc()
    {
        for (var phase = 0.0; phase < 1; phase += 0.02)
        {
            foreach (var point in MoonDisc.LitLimb(0, 0, 10, phase))
            {
                Assert.True(double.Hypot(point.X, point.Y) <= 10 + 1e-9, $"phase {phase:F2} left the disc");
            }
        }
    }

    [Fact]
    public void S_13_The_outline_is_closed_and_symmetric_top_to_bottom()
    {
        var points = MoonDisc.LitLimb(0, 0, 10, 0.3, samples: 16);

        Assert.Equal(34, points.Count); // two arcs of 17 points
        Assert.Equal(points[0].Y, points[^1].Y, 9);
        Assert.Equal(points[16].Y, points[17].Y, 9);
    }

    private static IReadOnlyList<SkySample> Samples() =>
    [
        new SkySample(0, 0),
        new SkySample(0.5, 40),
        new SkySample(1, 0),
    ];

    private static (double Min, double Max) Span(IReadOnlyList<PlotPoint> points)
    {
        var min = double.MaxValue;
        var max = double.MinValue;

        foreach (var point in points)
        {
            min = Math.Min(min, point.X);
            max = Math.Max(max, point.X);
        }

        return (min, max);
    }

    /// <summary>
    /// The limb and the terminator where the disc is widest, i.e. on the centre line. The outline is
    /// two arcs of <c>steps + 1</c> points, each running top to bottom, so the halfway index of each
    /// is the point at the equator.
    /// </summary>
    private static (double Limb, double Terminator) Widest(IReadOnlyList<PlotPoint> points)
    {
        var steps = (points.Count / 2) - 1;
        return (points[steps / 2].X, points[steps + 1 + (steps / 2)].X);
    }
}
