using Themia.Core.Geometry;
using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2460]..[S-2465] Several selected widgets resized as one block by the selection's frame (owner, 2026-11-20: "add the
/// possibility to resize multiple widgets with each other. they should still snap to the grid of course. this means in
/// some cases you need to work with common denominators and stuff"). A 1920 × 1080 screen: a grid cell is exactly 10 DIPs,
/// so every rect here is written in cells.
/// </summary>
public sealed class BlockResizeTests
{
    private static readonly ScreenMetrics Screen = new(1920, 1080);

    private const int Cols = 192;
    private const int Rows = 108;

    /// <summary>A rect of whole cells.</summary>
    private static FracRect C(double x, double y, double w, double h) => new(x / Cols, y / Rows, w / Cols, h / Rows);

    /// <summary>A rect back in cells, rounded to the float noise.</summary>
    private static (double X, double Y, double W, double H) Cells(FracRect r)
        => (Math.Round(r.X * Cols, 6), Math.Round(r.Y * Rows, 6), Math.Round(r.W * Cols, 6), Math.Round(r.H * Rows, 6));

    /// <summary>A free sizing on the grid: <paramref name="minW"/>..<paramref name="maxW"/> by <paramref name="minH"/>..<paramref name="maxH"/> cells.</summary>
    private static SizeLimits Free(double minW = 4, double maxW = 120, double minH = 4, double maxH = 80)
        => new(new AxisLimits(minW / Cols, maxW / Cols, Lines: Cols), new AxisLimits(minH / Rows, maxH / Rows, Lines: Rows));

    /// <summary>A fixed size (a desktop icon): <paramref name="w"/> by <paramref name="h"/> cells.</summary>
    private static SizeLimits Fixed(double w, double h)
        => new(new AxisLimits(w / Cols, w / Cols, Lines: Cols), new AxisLimits(h / Rows, h / Rows, Lines: Rows));

    private static SizeLimits?[] FreeAll(int n) => [.. Enumerable.Range(0, n).Select(_ => (SizeLimits?)Free())];

    private static IReadOnlyList<FracRect> Drag(FracRect[] start, ResizeHandle handle, double dxCells, double dyCells, SizeLimits?[]? limits = null)
        => BlockResize.Resize(start, limits ?? FreeAll(start.Length), handle, dxCells / Cols, dyCells / Rows, Screen);

    // ---- proportional, on the grid -------------------------------------------------------------------

    [Fact]
    public void S2460_two_equal_widgets_side_by_side_grow_a_cell_each_together_on_their_common_step()
    {
        FracRect[] row = [C(10, 10, 20, 12), C(30, 10, 20, 12)];
        Assert.Equal(2, BlockResize.CommonStep(row, horizontal: true, Screen));

        // Under half the step: nothing moves. Half of it and more: each widget one cell wider, still touching.
        Assert.Equal(row, Drag(row, ResizeHandle.Right, 0.9, 0));
        var grown = Drag(row, ResizeHandle.Right, 1.0, 0);
        Assert.Equal((10, 10, 21, 12), Cells(grown[0]));
        Assert.Equal((31, 10, 21, 12), Cells(grown[1]));

        // Ten cells: five each.
        var far = Drag(row, ResizeHandle.Right, 10, 0);
        Assert.Equal((10, 10, 25, 12), Cells(far[0]));
        Assert.Equal((35, 10, 25, 12), Cells(far[1]));
    }

    [Fact]
    public void S2460_mixed_sizes_scale_in_proportion_on_a_common_step_of_whole_cells()
    {
        // 20 and 30 cells: every 5 cells of the block are 2 of the first and 3 of the second.
        FracRect[] row = [C(10, 10, 20, 12), C(30, 10, 30, 12)];
        Assert.Equal(5, BlockResize.CommonStep(row, horizontal: true, Screen));

        var grown = Drag(row, ResizeHandle.Right, 10, 0);
        Assert.Equal((10, 10, 24, 12), Cells(grown[0]));
        Assert.Equal((34, 10, 36, 12), Cells(grown[1]));

        // 3 cells is nearer 5 than none: a whole step.
        var step = Drag(row, ResizeHandle.Right, 3, 0);
        Assert.Equal((22, 33), (Cells(step[0]).W, Cells(step[1]).W));

        var shrunk = Drag(row, ResizeHandle.Right, -25, 0);
        Assert.Equal((10, 10, 10, 12), Cells(shrunk[0]));
        Assert.Equal((20, 10, 15, 12), Cells(shrunk[1]));
    }

    [Fact]
    public void S2461_sizes_with_no_fine_common_step_go_cell_by_cell_each_edge_on_its_nearest_line()
    {
        // 17 and 23 cells have no common divisor: their exact step would be the whole block (40 cells).
        FracRect[] row = [C(10, 10, 17, 12), C(27, 10, 23, 12)];
        Assert.Equal(1, BlockResize.CommonStep(row, horizontal: true, Screen));

        var grown = Drag(row, ResizeHandle.Right, 3, 0);
        // 17 × 43 / 40 = 18.3: the shared edge on its nearest line, the block exactly 43.
        Assert.Equal((10, 10, 18, 12), Cells(grown[0]));
        Assert.Equal((28, 10, 25, 12), Cells(grown[1]));
    }

    [Fact]
    public void S2461_the_common_step_counts_every_edge_of_every_member_and_gap()
    {
        // Three equal columns: a cell each, the block three; a column over two rows: the same columns.
        Assert.Equal(3, BlockResize.CommonStep([C(0, 0, 10, 5), C(10, 0, 10, 5), C(20, 0, 10, 5)], true, Screen));
        Assert.Equal(1, BlockResize.CommonStep([C(0, 0, 10, 5), C(0, 5, 10, 5)], true, Screen));

        // A gap is an edge too: 20, a 2-cell gap and 20 would be a 21-cell step, too coarse.
        Assert.Equal(1, BlockResize.CommonStep([C(0, 0, 20, 5), C(22, 0, 20, 5)], true, Screen));

        // An edge off the whole cells (a magnet's flush edge): cell by cell. The whole block off them by the same is not.
        Assert.Equal(1, BlockResize.CommonStep([C(0, 0, 20, 5), C(20.5, 0, 19.5, 5)], true, Screen));
        Assert.Equal(2, BlockResize.CommonStep([C(0.3, 0, 20, 5), C(20.3, 0, 20, 5)], true, Screen));
    }

    [Theory]
    [InlineData(ResizeHandle.Left)]
    [InlineData(ResizeHandle.TopLeft)]
    public void S2460_the_block_scales_about_the_opposite_edge_which_never_moves(ResizeHandle handle)
    {
        FracRect[] row = [C(10, 10, 20, 12), C(30, 10, 20, 12)];

        var grown = Drag(row, handle, -4, 0);

        // Anchored on the right (50): the left edge four cells out, two each.
        Assert.Equal((6, 10, 22, 12), Cells(grown[0]));
        Assert.Equal((28, 10, 22, 12), Cells(grown[1]));
    }

    [Fact]
    public void S2460_a_corner_scales_both_axes_a_grid_of_four_stays_a_grid()
    {
        FracRect[] grid = [C(10, 10, 20, 10), C(30, 10, 20, 10), C(10, 20, 20, 10), C(30, 20, 20, 10)];

        var grown = Drag(grid, ResizeHandle.BottomRight, 6, 4);

        Assert.Equal((10, 10, 23, 12), Cells(grown[0]));
        Assert.Equal((33, 10, 23, 12), Cells(grown[1]));
        Assert.Equal((10, 22, 23, 12), Cells(grown[2]));
        Assert.Equal((33, 22, 23, 12), Cells(grown[3]));
    }

    [Fact]
    public void S2460_a_side_handle_leaves_the_other_axis_alone()
    {
        FracRect[] mixed = [C(10, 10, 20, 10), C(30, 14, 20, 10)];

        var grown = Drag(mixed, ResizeHandle.Bottom, 0, 6);

        Assert.All(grown.Zip(mixed), p => Assert.Equal((p.Second.X, p.Second.W), (p.First.X, p.First.W)));
        Assert.NotEqual(mixed, grown);
    }

    // ---- each member's sizing stops the block --------------------------------------------------------

    [Fact]
    public void S2462_the_block_stops_where_one_member_reaches_its_max()
    {
        FracRect[] row = [C(10, 10, 20, 12), C(30, 10, 20, 12)];
        SizeLimits?[] limits = [Free(maxW: 22), Free()];

        var grown = Drag(row, ResizeHandle.Right, 30, 0, limits);

        Assert.Equal((10, 10, 22, 12), Cells(grown[0]));
        Assert.Equal((32, 10, 22, 12), Cells(grown[1]));
    }

    [Fact]
    public void S2462_the_block_stops_where_one_member_reaches_its_min()
    {
        FracRect[] row = [C(10, 10, 20, 12), C(30, 10, 20, 12)];
        SizeLimits?[] limits = [Free(), Free(minW: 15)];

        var shrunk = Drag(row, ResizeHandle.Left, 30, 0, limits);

        Assert.Equal((20, 10, 15, 12), Cells(shrunk[0]));
        Assert.Equal((35, 10, 15, 12), Cells(shrunk[1]));
        Assert.Equal(50, Cells(shrunk[1]).X + Cells(shrunk[1]).W);
    }

    [Fact]
    public void S14500_a_list_member_takes_whole_cells_and_its_neighbour_stays_joined()
    {
        // A header of 6 cells over a list of 8 cells at least (its rows stretch to any height, [S-14500]): the list on whole
        // cells, the header on its top.
        var list = new SizeLimits(new AxisLimits(4.0 / Cols, 120.0 / Cols, Lines: Cols), new AxisLimits(8.0 / Rows, 80.0 / Rows, Lines: Rows));
        FracRect[] stack = [C(10, 10, 30, 6), C(10, 16, 30, 20)];

        foreach (var dy in new[] { 3.0, 6, 9, 14, -5 })
        {
            var grown = Drag(stack, ResizeHandle.Bottom, 0, dy, [Free(), list]);
            var (header, rows) = (Cells(grown[0]), Cells(grown[1]));

            Assert.Equal(header.Y + header.H, rows.Y);
            Assert.True(rows.H >= 8);
            Assert.Equal(10, header.Y);
        }
    }

    [Fact]
    public void S2462_a_fixed_member_keeps_its_size_and_moves_with_its_edge()
    {
        // A desktop icon of 8 × 10 cells right of a widget: the widget scales, the icon only moves, still beside it.
        FracRect[] pair = [C(10, 10, 20, 10), C(30, 10, 8, 10)];
        SizeLimits?[] limits = [Free(), Fixed(8, 10)];

        var grown = Drag(pair, ResizeHandle.Right, 14, 0, limits);

        Assert.Equal(8, Cells(grown[1]).W);
        Assert.Equal(Cells(grown[0]).X + Cells(grown[0]).W, Cells(grown[1]).X);
        Assert.True(Cells(grown[0]).W > 20);
    }

    [Fact]
    public void S2462_a_fixed_member_lands_on_exactly_its_own_size_as_the_desktop_paints_it()
    {
        // A member of 3 cells fixed in height under the drive's reading: grown by the corner, it moves down and keeps exactly
        // its 3 cells, bit for bit the size its sizing paints (it once landed on 3 cells plus float noise, which the desktop
        // painted 3 cells: not where it landed).
        FracRect[] column = [C(45, 29, 26, 9), C(45, 38, 26, 3), C(45, 41, 26, 3)];
        var metric = new SizeLimits(Free(20, 192).Width, new AxisLimits(3.0 / Rows, 3.0 / Rows, Rows));
        SizeLimits?[] limits = [Free(), metric, metric];

        var grown = Drag(column, ResizeHandle.BottomRight, 10, 4, limits);

        Assert.True(Cells(grown[0]).H > 9);
        for (var i = 1; i < grown.Count; i++)
        {
            Assert.Equal(3.0 / Rows, grown[i].H);
            Assert.Equal(grown[i], SizeFit.Shown(grown[i], metric, Screen));
        }
    }

    [Fact]
    public void S2460_an_axis_every_member_is_fixed_on_has_no_handle_and_does_not_resize()
    {
        FracRect[] icons = [C(10, 10, 8, 10), C(18, 10, 8, 10)];
        SizeLimits?[] limits = [Fixed(8, 10), Fixed(8, 10)];

        Assert.Equal((false, false), BlockResize.Free(limits, 2));
        Assert.Equal(icons, Drag(icons, ResizeHandle.BottomRight, 10, 10, limits));
        Assert.Equal((true, false), BlockResize.Free([Fixed(8, 10), new SizeLimits(Free().Width, Fixed(8, 10).Height)], 2));
    }

    // ---- touching, apart, order --------------------------------------------------------------------

    [Fact]
    public void S2463_widgets_apart_stay_apart_by_a_cell_however_far_the_block_shrinks()
    {
        FracRect[] row = [C(10, 10, 20, 12), C(31, 10, 20, 12)];
        SizeLimits?[] small = [Free(minW: 1), Free(minW: 1)];

        var shrunk = Drag(row, ResizeHandle.Right, -38, 0, small);

        Assert.True(Cells(shrunk[1]).X - (Cells(shrunk[0]).X + Cells(shrunk[0]).W) >= 1);
    }

    [Fact]
    public void S2463_random_blocks_keep_their_edges_on_lines_their_joins_their_gaps_and_their_order()
    {
        var random = new Random(2460);
        var handles = Enum.GetValues<ResizeHandle>();
        for (var run = 0; run < 400; run++)
        {
            var block = RandomBlock(random);
            var limits = block.Select(_ => (SizeLimits?)Free(minW: 2, minH: 2)).ToArray();
            var handle = handles[random.Next(handles.Length)];
            var (dx, dy) = (random.Next(-40, 41) + random.NextDouble(), random.Next(-30, 31) + random.NextDouble());

            var resized = Drag(block, handle, dx, dy, limits);
            var why = $"run {run}: {handle} by ({dx:0.00}, {dy:0.00})";

            // The anchored edges stay where they were.
            var (before, after) = (BlockGeometry.Bounds(block), BlockGeometry.Bounds([.. resized]));
            if (!GridGeometry.MovesLeft(handle))
            {
                Assert.True(Math.Abs(before.X - after.X) < 1e-9, why);
            }

            if (!GridGeometry.MovesTop(handle))
            {
                Assert.True(Math.Abs(before.Y - after.Y) < 1e-9, why);
            }

            for (var i = 0; i < block.Length; i++)
            {
                // Every edge on a whole cell (the blocks start on whole cells), every member within its sizing.
                var r = Cells(resized[i]);
                Assert.True(new[] { r.X, r.Y, r.W, r.H }.All(v => Math.Abs(v - Math.Round(v)) < 1e-6), why);
                Assert.True(r.W >= 2 - 1e-6 && r.H >= 2 - 1e-6, why);

                for (var j = 0; j < block.Length; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    // Touching stays touching, apart stays apart, and side by side keeps its side.
                    Assert.Equal(Touch(block[i], block[j]), Touch(resized[i], resized[j]));
                    Assert.Equal(BlockGeometry.Over(block[i], block[j]), BlockGeometry.Over(resized[i], resized[j]));
                    Assert.True(block[i].Right > block[j].X + 1e-9 || resized[i].Right <= resized[j].X + 1e-9, why);
                    Assert.True(block[i].Bottom > block[j].Y + 1e-9 || resized[i].Bottom <= resized[j].Y + 1e-9, why);
                }
            }
        }
    }

    /// <summary>A block of a few widgets on whole cells: rows of columns that touch or stand apart, the rows touching or not.</summary>
    private static FracRect[] RandomBlock(Random random)
    {
        var rects = new List<FracRect>();
        var y = 20.0;
        for (var row = random.Next(1, 3); row > 0; row--)
        {
            var x = 40.0;
            var h = random.Next(4, 16);
            for (var col = random.Next(1, 4); col > 0; col--)
            {
                var w = random.Next(4, 24);
                rects.Add(C(x, y, w, h));
                x += w + (random.Next(3) == 0 ? random.Next(1, 4) : 0);
            }

            y += h + (random.Next(3) == 0 ? random.Next(1, 4) : 0);
        }

        return [.. rects];
    }

    /// <summary>Two rects meet edge to edge, overlapping along it.</summary>
    private static bool Touch(FracRect a, FracRect b)
    {
        static bool Near(double p, double q) => Math.Abs(p - q) < 1e-9;
        static bool Along(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0) > 1e-9;
        return (Along(a.X, a.Right, b.X, b.Right) && (Near(a.Bottom, b.Y) || Near(b.Bottom, a.Y)))
            || (Along(a.Y, a.Bottom, b.Y, b.Bottom) && (Near(a.Right, b.X) || Near(b.Right, a.X)));
    }

    // ---- the work area and the widgets that stay ----------------------------------------------------

    [Fact]
    public void S2462_a_block_hanging_over_the_edge_stops_before_a_member_keeps_less_than_the_visible_minimum()
    {
        // Mostly off the left edge; the right handle pulls everything towards the anchored left edge, off the screen.
        FracRect[] row = [C(-15, 10, 20, 12), C(5, 10, 20, 12)];

        var shrunk = Drag(row, ResizeHandle.Right, -30, 0);

        var visible = (shrunk[0].Right - 0) * Screen.Width;
        Assert.True(visible >= GridGeometry.MinVisiblePx - 1e-6);
        Assert.True(visible < GridGeometry.MinVisiblePx + 10);
        Assert.Equal(shrunk[0].Right, shrunk[1].X, 9);
    }

    [Fact]
    public void S2462_S6608_S855_a_block_grows_over_a_taskbar_as_over_the_monitors_edge()
    {
        // A taskbar 80 DIPs tall: the allowed area ends at row 100 of 108. Owner, 2026-11-21: "the widgets should be able
        // to go over the screen".
        var taskbar = Screen with { Allowed = new FracRect(0, 0, 1, 100.0 / Rows) };
        FracRect[] stack = [C(10, 80, 20, 8), C(10, 88, 20, 8)];

        var grown = BlockResize.Resize(stack, FreeAll(2), ResizeHandle.Bottom, 0, 10.0 / Rows, taskbar);

        Assert.True(Cells(grown[1]).Y + Cells(grown[1]).H > 100 + 1e-6, $"{grown[1]} stopped at the taskbar");
        Assert.Equal(Cells(grown[0]).Y + Cells(grown[0]).H, Cells(grown[1]).Y, 6);

        // Past the monitor's own edge it goes on the same.
        Assert.True(Drag(stack, ResizeHandle.Bottom, 0, 30)[1].Bottom > 1);
    }

    [Fact]
    public void S2462_a_block_may_grow_past_the_screens_edge()
    {
        FracRect[] row = [C(150, 10, 20, 12), C(170, 10, 20, 12)];

        var grown = Drag(row, ResizeHandle.Right, 20, 0);

        Assert.True(grown[1].Right > 1);
    }

    [Fact]
    public void S2460_a_blocks_resize_goes_over_a_widget_that_stays()
    {
        // [S-11959] retired (owner, 2026-11-21): nothing keeps widgets apart. Dragged 30 cells right, the row grows a cell
        // each step all the way, over the widget standing at 56 (kept apart once stopped it flush there, at 23 each).
        FracRect[] row = [C(10, 10, 20, 12), C(30, 10, 20, 12)];

        var grown = Drag(row, ResizeHandle.Right, 30, 0);

        Assert.True(Cells(grown[1]).X + Cells(grown[1]).W > 66);
        Assert.Equal(Cells(grown[0]).W, Cells(grown[1]).W);
    }

    // ---- the keys ------------------------------------------------------------------------------------

    [Fact]
    public void S2464_shift_arrows_resize_the_block_by_its_right_or_bottom_edge_one_common_step_a_press()
    {
        FracRect[] row = [C(10, 10, 20, 12), C(30, 10, 20, 12)];

        var right = BlockResize.Nudge(row, FreeAll(2), NudgeDirection.Right, NudgeStep.Cell, Screen);
        Assert.Equal((10, 10, 21, 12), Cells(right[0]));
        Assert.Equal((31, 10, 21, 12), Cells(right[1]));

        var left = BlockResize.Nudge(row, FreeAll(2), NudgeDirection.Left, NudgeStep.Cell, Screen);
        Assert.Equal((10, 10, 19, 12), Cells(left[0]));
        Assert.Equal((29, 10, 19, 12), Cells(left[1]));

        var far = BlockResize.Nudge(row, FreeAll(2), NudgeDirection.Right, NudgeStep.Far, Screen);
        Assert.Equal((25, 25), (Cells(far[0]).W, Cells(far[1]).W));

        var down = BlockResize.Nudge(row, FreeAll(2), NudgeDirection.Down, NudgeStep.Cell, Screen);
        Assert.Equal((10, 10, 20, 13), Cells(down[0]));
        Assert.Equal((30, 10, 20, 13), Cells(down[1]));
    }

    [Fact]
    public void S2464_a_press_on_a_list_under_a_header_changes_the_block_and_keeps_it_joined()
    {
        // [S-14500] A list takes every cell: a press grows the block on its common step, the two still joined.
        var list = new SizeLimits(new AxisLimits(4.0 / Cols, 120.0 / Cols, Lines: Cols), new AxisLimits(8.0 / Rows, 80.0 / Rows, Lines: Rows));
        FracRect[] stack = [C(10, 10, 30, 6), C(10, 16, 30, 20)];

        var down = BlockResize.Nudge(stack, [Free(), list], NudgeDirection.Down, NudgeStep.Cell, Screen);

        Assert.NotEqual(stack, down);
        Assert.Equal(Cells(down[0]).Y + Cells(down[0]).H, Cells(down[1]).Y);
    }
}
