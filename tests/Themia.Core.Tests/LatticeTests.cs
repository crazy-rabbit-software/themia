using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11893] The one set of rules every geometry rule compares by: one edge, across, overlaps, meets, touches, the same
/// rect, all with one tolerance, float noise; and a painted cell on the device lattice.
/// </summary>
public sealed class LatticeTests
{
    [Fact]
    public void S11893_one_tolerance_float_noise_and_the_grids_own()
    {
        Assert.Equal(Themia.Core.Widgets.GridCells.Noise, Lattice.Epsilon);
        Assert.True(Lattice.Same(0.3, 0.1 + 0.2));
        Assert.False(Lattice.Same(0.5, 0.5 + (1.0 / 3840)));
    }

    [Fact]
    public void S11893_rects_that_only_touch_do_not_overlap_nor_does_float_noise()
    {
        var a = new FracRect(0.1, 0.1, 0.2, 0.2);
        Assert.False(Lattice.Overlaps(a, new FracRect(0.3, 0.1, 0.2, 0.2)));
        Assert.False(Lattice.Overlaps(a, new FracRect(0.3 - 1e-9, 0.1, 0.2, 0.2)));
        Assert.True(Lattice.Overlaps(a, new FracRect(0.3 - (1.0 / 1920), 0.1, 0.2, 0.2)));
        Assert.False(Lattice.Overlaps(a, new FracRect(double.NaN, 0.1, 0.2, 0.2)));
    }

    [Fact]
    public void S11893_rects_meet_side_to_side_along_a_stretch_never_at_a_corner()
    {
        var a = new FracRect(0.1, 0.1, 0.2, 0.2);
        Assert.Equal((false, true), Lattice.Meeting(a, new FracRect(0.3, 0.15, 0.1, 0.1)));
        Assert.Equal((false, false), Lattice.Meeting(new FracRect(0.3, 0.15, 0.1, 0.1), a));
        Assert.Equal((true, true), Lattice.Meeting(a, new FracRect(0.1 + 0.05, 0.1 + 0.2, 0.1, 0.1)));
        Assert.Null(Lattice.Meeting(a, new FracRect(0.3, 0.3, 0.1, 0.1)));
        Assert.Null(Lattice.Meeting(a, new FracRect(0.3 + (1.0 / 1920), 0.1, 0.1, 0.1)));
        Assert.True(Lattice.Follows(a, new FracRect(0.15, 0.3, 0.1, 0.1), vertical: true));
        Assert.False(Lattice.Follows(a, new FracRect(0.15, 0.3, 0.1, 0.1), vertical: false));
        Assert.False(Lattice.Follows(new FracRect(0.15, 0.3, 0.1, 0.1), a, vertical: true));
    }

    [Fact]
    public void S11893_touching_rects_are_one_set_through_every_neighbour()
    {
        FracRect[] rects = [new(0, 0, 0.1, 0.1), new(0.1, 0, 0.1, 0.1), new(0.5, 0.5, 0.1, 0.1), new(0.1, 0.1, 0.1, 0.1)];
        var sets = Lattice.Touching(rects);
        Assert.Equal(2, sets.Count);
        Assert.Equal([0, 1, 3], sets[0]);
        Assert.Equal([2], sets[1]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2)]
    public void S11893_a_painted_cell_stands_on_whole_device_pixels_and_neighbours_share_one(double scale)
    {
        var screen = new ScreenMetrics(2560 / scale, 1440 / scale);
        var (cols, _) = GridGeometry.GridDims(screen);
        var left = Lattice.Device(GridGeometry.PaintedCell(new FracRect(10.0 / cols, 0.1, 7.0 / cols, 0.1), screen, scale), scale);
        var right = Lattice.Device(GridGeometry.PaintedCell(new FracRect(17.0 / cols, 0.1, 3.0 / cols, 0.1), screen, scale), scale);
        Assert.Equal(Math.Round(left.X), left.X);
        Assert.Equal(left.Right, right.X);
        Assert.Equal((false, true), Lattice.Meeting(left, right));
        Assert.Equal(new DeviceRect(0, 0, 3, 3), Lattice.Device(new DipRect(0, 0, 2, 2), 1.5));
        Assert.Equal(new DeviceRect(0, 0, 2, 2), Lattice.Device(new DipRect(0, 0, 2, 2), double.NaN));
    }
}
