using Themia.Core.Geometry;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11807] [S-11808] With the snap grid on, every widget's cell is whole grid cells on both axes: its
/// kind's sizing stays content's, the host fits it to the grid (Min up, a free Max down but never under
/// Min, steps up), and a ready-made group is laid out on the grid as one block.
/// </summary>
public sealed class GridFitTests
{
    /// <summary>A 1000 × 1000 DIP screen: 100 grid lines each way, a cell of 10 DIPs, a fraction of 0.001 one DIP.</summary>
    private static readonly ScreenMetrics Screen = new(1000, 1000);

    /// <summary>2560 × 1440 at 150 %: 1706.67 × 960 DIPs, 171 columns of 9.98 DIPs (no whole DIP), 96 rows of 10.</summary>
    private static readonly ScreenMetrics Odd = new(2560 / 1.5, 1440 / 1.5);

    /// <summary>A range whose every end is between grid lines.</summary>
    private static WidgetSizing Ragged => WidgetSizing.Range(new(95, 33), new(143, 77), new(487, 203));

    private static WidgetSizing List(int rows) => WidgetSizing.Range(new(220, 1), new(300, 1), new(520, 1))
        .WithHeightSteps(new SizeSteps(Base: 76, Step: 28, MinCount: 1, MaxCount: 20, Count: rows));

    private static void Near(double expected, double actual) => Assert.Equal(expected, actual, 6);

    /// <summary>Whether a fraction is whole cells of <paramref name="lines"/> lines.</summary>
    private static bool Whole(double frac, int lines) => Math.Abs((frac * lines) - Math.Round(frac * lines)) < 1e-6;

    private static SizeLimits OnGrid(WidgetSizing sizing, ScreenMetrics screen, double scale = 1)
        => SizeFit.Limits(sizing, screen, scale)!;

    [Fact]
    public void S11807_min_rounds_up_and_a_free_max_rounds_down_to_whole_cells()
    {
        var limits = OnGrid(Ragged, Screen);

        Near(0.10, limits.Width.Min);
        Near(0.48, limits.Width.Max);
        Near(0.04, limits.Height.Min);
        Near(0.20, limits.Height.Max);
        Assert.True(limits.Width.OnGrid && limits.Height.OnGrid);
    }

    [Fact]
    public void S11807_every_size_the_axis_gives_is_whole_cells_and_never_less_than_was_asked_inside_the_range()
    {
        var limits = OnGrid(Ragged, Screen);

        foreach (var dip in new[] { 1.0, 95, 96, 99.9999, 100, 100.0000001, 143, 150, 333.3, 479, 480, 481, 487, 2000 })
        {
            var w = limits.Width.Range(dip / 1000);
            Assert.True(Whole(w, 100), $"{dip} gave {w * 1000} DIPs");
            Assert.InRange(w, limits.Width.Min - 1e-9, limits.Width.Max + 1e-9);
            if (dip >= 100 && dip <= 480)
            {
                Assert.True(w >= (dip / 1000) - 1e-6, $"{dip} gave less: {w * 1000}");
            }
        }
    }

    [Fact]
    public void S11807_the_painted_box_of_a_stored_rect_is_whole_cells_at_its_top_left()
    {
        // A rect stored at the kind's natural size before the grid held sizes (143 × 77 DIPs).
        var shown = SizeFit.Shown(new FracRect(0.25, 0.4, 0.143, 0.077), OnGrid(Ragged, Screen), Screen);

        Near(0.25, shown.X);
        Near(0.4, shown.Y);
        Near(0.15, shown.W);
        Near(0.08, shown.H);
    }

    [Fact]
    public void S11807_a_range_narrower_than_a_cell_becomes_one_fixed_size_of_whole_cells()
    {
        // 95..98 wide: Min up to 100, Max down to 90 but never under Min: fixed at 100, no handle.
        var limits = OnGrid(WidgetSizing.Range(new(95, 40), new(96, 40), new(98, 40)), Screen);

        Near(0.10, limits.Width.Min);
        Near(0.10, limits.Width.Max);
        Assert.True(limits.Width.Fixed);
        Assert.True(limits.Height.Fixed);
        Near(0.04, limits.Height.Min);
    }

    [Fact]
    public void S11807_a_widget_whose_max_is_under_one_cell_is_one_cell()
    {
        var limits = OnGrid(WidgetSizing.Fixed(new(4, 7)), Screen);

        Near(0.01, limits.Width.Min);
        Near(0.01, limits.Width.Max);
        Near(0.01, limits.Height.Max);
        Near(0.01, limits.Width.Range(0.0001));
    }

    [Fact]
    public void S11807_a_fixed_size_that_is_whole_cells_stays_exactly_that()
    {
        var limits = OnGrid(WidgetSizing.Fixed(new(340, 40)), Screen);

        Near(0.34, limits.Width.Min);
        Near(0.34, limits.Width.Max);
        Near(0.04, limits.Height.Range(0.5));
    }

    [Fact]
    public void S11807_the_content_scale_is_inside_the_whole_cells()
    {
        // 160 × 1.5 = 240; 112 × 1.5 = 168 → 170; 480 × 1.5 = 720 ([S-11884]: no gap round the card).
        var limits = OnGrid(WidgetSizing.Range(new(160, 112), new(220, 144), new(480, 200)), Screen, scale: 1.5);

        Near(0.24, limits.Width.Min);
        Near(0.17, limits.Height.Min);
        Near(0.72, limits.Width.Max);
    }

    [Fact]
    public void S11807_a_stepped_axis_keeps_its_steps_each_rounded_up_to_whole_cells()
    {
        // Rows: 76 + 28 n. One row 104 → 110, twenty 636 → 640; the config's six 244 → 250.
        var limits = OnGrid(List(rows: 6), Screen);

        Near(0.11, limits.Height.Min);
        Near(0.64, limits.Height.Max);
        Near(0.25, limits.Height.Hold(0.9));

        // A drag to 300 DIPs: seven rows (272 → 280) and eight (300 → 300); eight is nearest.
        Near(0.30, limits.Height.Range(0.300));
        Near(0.28, limits.Height.Range(0.284));
        foreach (var n in Enumerable.Range(1, 20))
        {
            var step = (76 + (28 * n)) / 1000.0;
            var landed = limits.Height.Range(step);
            Assert.True(Whole(landed, 100));
            Assert.True(landed >= step - 1e-9);
        }
    }

    [Fact]
    public void S11807_the_config_a_cell_means_is_the_largest_step_its_whole_cells_hold()
    {
        var limits = OnGrid(List(rows: 6), Screen);

        // 250 DIPs (six rows rounded up) holds six rows (244), not seven (272).
        Near(0.244, limits.Height.Holds(0.25));
        Near(0.272, limits.Height.Holds(0.28));

        // Steps smaller than a cell: the largest that fits.
        var fine = new AxisLimits(0.05, 0.2, StepBase: 0.05, Step: 0.004, Lines: 100);
        Near(0.074, fine.Holds(0.075));
    }

    [Fact]
    public void S11807_a_resize_lands_the_moved_edge_on_a_line_and_the_size_on_whole_cells()
    {
        var limits = OnGrid(Ragged, Screen);
        var start = new FracRect(0.2, 0.2, 0.15, 0.08);

        foreach (var dx in new[] { 0.0004, 0.0031, 0.0177, -0.0213, 0.5 })
        {
            var settled = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Right, dx, 0, Screen), Screen, ResizeHandle.Right, limits);
            Near(0.2, settled.X);
            Assert.True(Whole(settled.W, 100), $"{dx}: {settled.W * 1000}");
            Assert.True(Whole(settled.Right, 100));
        }

        // Alt (the snap aid suspended for the gesture) frees where the edge is, never the whole cells.
        var free = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Right, 0.0123, 0, Screen), Screen, ResizeHandle.Right, limits);
        Assert.True(Whole(free.W, 100));
    }

    [Fact]
    public void S11807_a_shift_arrow_on_the_grid_is_at_least_a_whole_cell_and_lands_on_whole_cells()
    {
        var limits = OnGrid(Ragged, Screen);
        var rect = new FracRect(0.2, 0.2, 0.15, 0.08);

        foreach (var step in new[] { Shell.NudgeStep.Cell, Shell.NudgeStep.Far })
        {
            var smaller = KeyboardNudge.Resize(rect, Shell.NudgeDirection.Left, step, Screen, limits);
            var larger = KeyboardNudge.Resize(rect, Shell.NudgeDirection.Down, step, Screen, limits);

            Assert.True(smaller.W < rect.W - 1e-9, $"{step} did not shrink");
            Assert.True(Whole(smaller.W, 100) && Whole(larger.H, 100));
            Assert.True(larger.H > rect.H + 1e-9, $"{step} did not grow");
        }
    }


    [Fact]
    public void S11807_fitting_to_the_grid_twice_changes_nothing()
    {
        var once = OnGrid(List(rows: 6), Odd);
        var twice = GridFit.OnGrid(once, Odd);

        Assert.Equal(once, twice);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void S11807_at_any_display_scale_a_card_on_a_line_ends_on_a_line_and_shares_its_pixels(double raster)
    {
        foreach (var screen in new[] { Odd, new ScreenMetrics(1920 / raster, 1080 / raster), new ScreenMetrics(3440 / raster, 1440 / raster) })
        {
            var (cols, rows) = GridGeometry.GridDims(screen);
            var limits = OnGrid(Ragged, screen, scale: raster);
            var stored = GridGeometry.Settle(new FracRect(0.3137, 0.2719, 0.1, 0.1), screen);
            var shown = SizeFit.Shown(stored, limits, screen);
            var px = GridGeometry.ToPixels(shown, screen, raster);

            var left = (int)Math.Round(shown.X * cols);
            var right = (int)Math.Round(shown.Right * cols);
            var top = (int)Math.Round(shown.Y * rows);
            var bottom = (int)Math.Round(shown.Bottom * rows);
            Assert.True(Whole(shown.W, cols) && Whole(shown.H, rows));

            // The drawn line (an even width, centred on a pixel edge) and the card's edge are the same device pixel.
            Near(GridGeometry.CrispLine(GridFit.LineDip(left, cols, screen.Width), raster, 2), px.X);
            Near(GridGeometry.CrispLine(GridFit.LineDip(right, cols, screen.Width), raster, 2), px.X + px.Width);
            Near(GridGeometry.CrispLine(GridFit.LineDip(top, rows, screen.Height), raster, 2), px.Y);
            Near(GridGeometry.CrispLine(GridFit.LineDip(bottom, rows, screen.Height), raster, 2), px.Y + px.Height);
        }
    }

    [Fact]
    public void S11807_a_scale_or_display_change_refits_the_size_to_the_new_screens_cells()
    {
        var stored = new FracRect(0.1, 0.1, 0.143, 0.077);
        foreach (var (screen, scale) in new[] { (Screen, 1.0), (Screen, 1.3), (Odd, 1.0), (Odd, 1.5), (new ScreenMetrics(1280, 720), 0.75) })
        {
            var (cols, rows) = GridGeometry.GridDims(screen);
            var shown = SizeFit.Shown(stored, OnGrid(Ragged, screen, scale), screen);
            Assert.True(Whole(shown.W, cols), $"{screen} × {scale}: {shown.W * cols} cells wide");
            Assert.True(Whole(shown.H, rows), $"{screen} × {scale}: {shown.H * rows} cells tall");
        }
    }

    [Fact]
    public void S11808_a_group_keeps_its_shared_edges_on_whole_cells_and_nothing_overlaps()
    {
        // Now playing's shape: a 96 cover, the 244 track beside it, the 340 progress under both.
        IReadOnlyList<FracRect> boxes =
        [
            new(0, 0, 0.096, 0.096),
            new(0.096, 0, 0.244, 0.096),
            new(0, 0.096, 0.340, 0.040),
        ];

        var laid = GridFit.Layout(boxes, [null, null, null], Screen);

        Assert.Equal(new FracRect(0, 0, 0.10, 0.10), Round(laid[0]));
        Assert.Equal(new FracRect(0.10, 0, 0.25, 0.10), Round(laid[1]));
        Assert.Equal(new FracRect(0, 0.10, 0.35, 0.04), Round(laid[2]));
    }

    [Fact]
    public void S11808_a_group_member_held_to_its_limits_stays_inside_its_neighbours()
    {
        // The progress is fixed at 340: it cannot reach the track's new end (350) and keeps its own size.
        IReadOnlyList<FracRect> boxes = [new(0, 0, 0.096, 0.096), new(0.096, 0, 0.244, 0.096), new(0, 0.096, 0.340, 0.040)];
        var fixedBar = OnGrid(WidgetSizing.Fixed(new(340, 40)), Screen);

        var laid = GridFit.Layout(boxes, [null, null, fixedBar], Screen);

        Assert.Equal(new FracRect(0, 0.10, 0.34, 0.04), Round(laid[2]));
        Assert.Equal(new FracRect(0.10, 0, 0.25, 0.10), Round(laid[1]));
    }

    [Fact]
    public void S11808_space_between_boxes_keeps_its_nearest_whole_cells_and_the_corner_lands_on_a_line()
    {
        IReadOnlyList<FracRect> boxes = [new(0.1234, 0.2, 0.1, 0.1), new(0.2534, 0.2, 0.1, 0.1)];

        var laid = GridFit.Layout(boxes, [null, null], Screen);

        Assert.Equal(new FracRect(0.12, 0.2, 0.10, 0.10), Round(laid[0]));
        Assert.Equal(new FracRect(0.25, 0.2, 0.10, 0.10), Round(laid[1]));
    }

    [Fact]
    public void S11808_a_grid_of_ragged_boxes_lays_out_on_whole_cells_with_every_touch_kept()
    {
        // Two by two of 143 × 77, as a ready-made This PC is laid.
        IReadOnlyList<FracRect> boxes =
        [
            new(0, 0, 0.143, 0.077), new(0.143, 0, 0.143, 0.077),
            new(0, 0.077, 0.143, 0.077), new(0.143, 0.077, 0.143, 0.077),
        ];
        var limits = OnGrid(Ragged, Odd);
        var laid = GridFit.Layout(boxes, [limits, limits, limits, limits], Odd);
        var (cols, rows) = GridGeometry.GridDims(Odd);

        foreach (var r in laid)
        {
            Assert.True(Whole(r.X, cols) && Whole(r.Right, cols) && Whole(r.Y, rows) && Whole(r.Bottom, rows));
        }

        Near(laid[0].Right, laid[1].X);
        Near(laid[2].Right, laid[3].X);
        Near(laid[0].Bottom, laid[2].Y);
        Near(laid[1].Bottom, laid[3].Y);
    }


    [Fact]
    public void S11809_a_pinned_axis_is_laid_at_its_pinned_size_the_desktop_paints()
    {
        // A list pinned to its rows' height: the block gives it that height, never the box's, so it is painted where it was laid.
        var list = SizeFit.Limits(List(4), Screen, 1)!;
        IReadOnlyList<FracRect> boxes = [new(0, 0, 0.3, 0.05), new(0, 0.05, 0.3, 0.1)];

        var laid = GridFit.Layout(boxes, [list, null], Screen);

        Near(list.Height.Pinned!.Value, laid[0].H);
        Near(laid[0].Bottom, laid[1].Y);
        Assert.Equal(laid[0], SizeFit.Shown(laid[0], list, Screen));
    }

    private static FracRect Round(FracRect r) => new(Math.Round(r.X, 6), Math.Round(r.Y, 6), Math.Round(r.W, 6), Math.Round(r.H, 6));
}
