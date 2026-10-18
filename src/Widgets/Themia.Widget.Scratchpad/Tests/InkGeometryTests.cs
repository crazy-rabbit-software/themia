using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Scratchpad.Tests;

/// <summary>
/// The scratchpad's geometry. A drawing surface is the one thing in the app that cannot be tested
/// through its view at all, so everything that decides what is stored and what the eraser cuts is
/// pinned here.
/// </summary>
public class InkGeometryTests
{
    // -----------------------------------------------------------------------------------------
    // Packing: the storage round trip
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_stroke_survives_being_written_and_read_back()
    {
        List<InkPoint> stroke = [new(0, 0), new(0.25, 0.5), new(0.5, 0.75), new(1, 1)];

        var round = InkGeometry.Unpack(InkGeometry.Pack(stroke));

        Assert.Equal(stroke, round);
    }

    [Fact]
    public void Packing_flattens_to_x_y_pairs()
        => Assert.Equal([0.25, 0.5, 0.75, 1.0], InkGeometry.Pack([new(0.25, 0.5), new(0.75, 1.0)]));

    [Fact]
    public void Packing_rounds_to_four_places_so_a_point_does_not_cost_seventeen_digits()
        => Assert.Equal([0.1235, 0.5], InkGeometry.Pack([new(0.12345678, 0.5)]));

    [Fact]
    public void An_empty_stroke_packs_and_unpacks_to_nothing()
    {
        Assert.Empty(InkGeometry.Pack([]));
        Assert.Empty(InkGeometry.Pack(null));
        Assert.Empty(InkGeometry.Unpack([]));
        Assert.Empty(InkGeometry.Unpack(null));
    }

    // -----------------------------------------------------------------------------------------
    // Unpacking: the trust boundary
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_truncated_file_loses_the_half_point_and_keeps_the_rest()
        => Assert.Equal([new InkPoint(0.1, 0.2)], InkGeometry.Unpack([0.1, 0.2, 0.3]));

    [Fact]
    public void A_point_with_no_number_in_it_is_dropped_rather_than_drawn_through_infinity()
    {
        var points = InkGeometry.Unpack([0.1, 0.2, double.NaN, 0.4, 0.5, double.PositiveInfinity, 0.7, 0.8]);

        Assert.Equal([new InkPoint(0.1, 0.2), new InkPoint(0.7, 0.8)], points);
    }

    [Fact]
    public void A_point_outside_the_pad_is_clamped_to_its_edge_rather_than_dropped()
    {
        // A captured pointer routinely leaves the widget; the line should stop at the edge, which is
        // where it was heading, rather than the stroke losing its end.
        var points = InkGeometry.Unpack([-4, 0.5, 0.5, 9]);

        Assert.Equal([new InkPoint(0, 0.5), new InkPoint(0.5, 1)], points);
    }

    [Fact]
    public void A_stroke_longer_than_the_budget_is_cut_rather_than_read_whole()
    {
        var packed = new List<double>();
        for (var i = 0; i < InkGeometry.MaxPointsPerStroke * 2; i++)
        {
            packed.Add(0.5);
            packed.Add(0.5);
        }

        Assert.Equal(InkGeometry.MaxPointsPerStroke, InkGeometry.Unpack(packed).Count);
    }

    // -----------------------------------------------------------------------------------------
    // Thinning
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_sample_inside_the_last_one_is_not_kept()
        => Assert.False(InkGeometry.ShouldKeep(new InkPoint(0.5, 0.5), new InkPoint(0.5, 0.5 + (InkGeometry.MinSegment / 2))));

    [Fact]
    public void A_sample_a_full_segment_away_is_kept()
        => Assert.True(InkGeometry.ShouldKeep(new InkPoint(0.5, 0.5), new InkPoint(0.5, 0.5 + InkGeometry.MinSegment)));

    [Fact]
    public void A_sample_that_is_not_a_number_is_never_kept()
        => Assert.False(InkGeometry.ShouldKeep(new InkPoint(0.5, 0.5), new InkPoint(double.NaN, 0.9)));

    [Fact]
    public void Thinning_measures_the_real_distance_not_one_axis()
    {
        // Half a segment on each axis is more than a whole one diagonally.
        var step = InkGeometry.MinSegment * 0.8;
        Assert.True(InkGeometry.ShouldKeep(new InkPoint(0.5, 0.5), new InkPoint(0.5 + step, 0.5 + step)));
    }

    // -----------------------------------------------------------------------------------------
    // The pen
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, InkGeometry.MinWidth)]
    [InlineData(-3, InkGeometry.MinWidth)]
    [InlineData(1000, InkGeometry.MaxWidth)]
    [InlineData(4, 4)]
    public void A_stored_pen_width_is_clamped_to_something_drawable(double stored, double expected)
        => Assert.Equal(expected, InkGeometry.ClampWidth(stored));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_pen_width_that_is_not_a_number_falls_back_rather_than_reaching_a_StrokeThickness(double stored)
        => Assert.Equal(InkGeometry.DefaultWidth, InkGeometry.ClampWidth(stored));

    [Fact]
    public void Clamping_a_raw_pointer_position_keeps_it_on_the_pad()
    {
        Assert.Equal(new InkPoint(0, 1), InkGeometry.Clamp(new InkPoint(-2, 3)));
        Assert.Equal(new InkPoint(0.4, 0.6), InkGeometry.Clamp(new InkPoint(0.4, 0.6)));
        Assert.Equal(new InkPoint(0, 0), InkGeometry.Clamp(new InkPoint(double.NaN, 0.5)));
    }

    // -----------------------------------------------------------------------------------------
    // The eraser
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void The_eraser_cuts_a_stroke_it_crosses_between_two_stored_points()
    {
        // The point of measuring against segments: thinning leaves long straight runs, and a probe
        // in the middle of one is nowhere near either end.
        List<InkPoint> line = [new(0, 0.5), new(1, 0.5)];

        Assert.True(InkGeometry.Touches(line, new InkPoint(0.5, 0.52), 0.05));
        Assert.False(InkGeometry.Touches(line, new InkPoint(0.5, 0.8), 0.05));
    }

    [Fact]
    public void The_eraser_cuts_a_single_point_dot()
    {
        List<InkPoint> dot = [new(0.5, 0.5)];

        Assert.True(InkGeometry.Touches(dot, new InkPoint(0.52, 0.5), 0.05));
        Assert.False(InkGeometry.Touches(dot, new InkPoint(0.7, 0.5), 0.05));
    }

    [Fact]
    public void The_eraser_measures_to_the_end_of_a_segment_not_to_the_infinite_line()
    {
        List<InkPoint> line = [new(0.4, 0.5), new(0.6, 0.5)];

        // On the line the segment lies on, but well past its end.
        Assert.False(InkGeometry.Touches(line, new InkPoint(0.9, 0.5), 0.05));
    }

    [Fact]
    public void An_eraser_with_no_size_and_a_stroke_with_no_points_touch_nothing()
    {
        Assert.False(InkGeometry.Touches([new(0.5, 0.5)], new InkPoint(0.5, 0.5), 0));
        Assert.False(InkGeometry.Touches([new(0.5, 0.5)], new InkPoint(0.5, 0.5), double.NaN));
        Assert.False(InkGeometry.Touches([], new InkPoint(0.5, 0.5), 0.1));
        Assert.False(InkGeometry.Touches(null, new InkPoint(0.5, 0.5), 0.1));
    }

    [Theory]
    [InlineData(0.5, 0.5, 0.0)]     // on the segment
    [InlineData(0.5, 0.6, 0.1)]     // straight off it
    [InlineData(1.5, 0.5, 0.5)]     // past the far end
    [InlineData(-0.5, 0.5, 0.5)]    // past the near end
    public void Distance_to_a_segment_is_measured_from_the_nearest_point_on_it(double x, double y, double expected)
        => Assert.Equal(expected, InkGeometry.DistanceToSegment(new InkPoint(0, 0.5), new InkPoint(1, 0.5), new InkPoint(x, y)), 6);

    [Fact]
    public void A_zero_length_segment_is_measured_as_a_point()
        => Assert.Equal(0.25, InkGeometry.DistanceToSegment(new InkPoint(0.5, 0.5), new InkPoint(0.5, 0.5), new InkPoint(0.75, 0.5)), 6);
}
