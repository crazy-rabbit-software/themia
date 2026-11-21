using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11895] One grid, one rounding: a screen's grid as whole numbers (<see cref="GridSpace"/>), and every fraction put on
/// a line by one rule, the nearest line, half a cell up, as every edge's DIP and pixel is ([S-11873]).
/// </summary>
public sealed class GridSpaceTests
{
    /// <summary>A screen of 128 × 128 cells, whose cell is a power of two: half a cell is exact as a fraction.</summary>
    private static readonly ScreenMetrics Square = new(1280, 1280);

    private const double Cell = 1.0 / 128;

    [Fact]
    public void S11895_the_grid_is_its_lines_as_whole_numbers()
    {
        var grid = GridGeometry.GridDims(new ScreenMetrics(1920, 1080));
        Assert.Equal(new GridSpace(192, 108), grid);
        var (cols, rows) = grid;
        Assert.Equal((192, 108), (cols, rows));
        Assert.Equal(7 / 192.0, grid.X(7));
        Assert.Equal(7 / 108.0, grid.Y(7));
        Assert.Equal(7, grid.Col(7.4 / 192));
        Assert.Equal(8, grid.Row(7.5 / 108));
        Assert.Equal(new FracRect(3 / 192.0, 4 / 108.0, 5 / 192.0, 6 / 108.0), grid.Frac(new Themia.Core.Widgets.CellBox(3, 4, 5, 6)));
    }

    [Fact]
    public void S11895_the_nearest_line_is_half_up_and_float_noise_is_on_the_line()
    {
        Assert.Equal(3, GridSpace.Nearest(2.5));
        Assert.Equal(4, GridSpace.Nearest(3.5));
        Assert.Equal(-2, GridSpace.Nearest(-2.5));
        Assert.Equal(3, GridSpace.Nearest(2.5 - 1e-9));
        Assert.Equal(2, GridSpace.Nearest(2.4));
        Assert.Equal(2, GridSpace.Up(2.000000001));
        Assert.Equal(3, GridSpace.Up(2.1));
        Assert.Equal(3, GridSpace.Down(2.9999999999));
        Assert.Equal(2, GridSpace.Down(2.9));
    }

    [Fact]
    public void S11895_regression_a_rect_half_a_cell_off_snaps_up_whatever_the_line_s_parity()
    {
        // Until 2026-11-21 the snap rounded half to even: 2.5 cells went down to 2 and 3.5 up to 4, so a rect half a cell
        // off (a placement centred on a line, an odd width) landed left of it or right of it by the line's parity.
        Assert.Equal(3 * Cell, GridGeometry.Snap(new FracRect(2.5 * Cell, 2.5 * Cell, 4 * Cell, 4 * Cell), Square).X);
        Assert.Equal(3 * Cell, GridGeometry.Snap(new FracRect(2.5 * Cell, 2.5 * Cell, 4 * Cell, 4 * Cell), Square).Y);
        Assert.Equal(4 * Cell, GridGeometry.Snap(new FracRect(3.5 * Cell, 3.5 * Cell, 4 * Cell, 4 * Cell), Square).X);
        Assert.Equal(5 * Cell, GridGeometry.Snap(new FracRect(10 * Cell, 10 * Cell, 4.5 * Cell, 4.5 * Cell), Square).W);
    }

    [Fact]
    public void S11895_regression_a_block_moved_half_a_cell_moves_a_whole_cell_on_either_parity()
    {
        FracRect[] block = [new(10 * Cell, 10 * Cell, 4 * Cell, 4 * Cell), new(14 * Cell, 10 * Cell, 4 * Cell, 4 * Cell)];
        Assert.Equal(1 * Cell, BlockGeometry.Move(block, 0.5 * Cell, 0, Square).Dx, 12);
        Assert.Equal(2 * Cell, BlockGeometry.Move(block, 1.5 * Cell, 0, Square).Dx, 12);
        Assert.Equal(3 * Cell, BlockGeometry.Move(block, 2.5 * Cell, 0, Square).Dx, 12);
    }
}
