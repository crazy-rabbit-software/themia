using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11873] A block (several selected widgets, moved together) keeps its cards' spacing while it moves: it moves by
/// whole cells ([S-11892]), so every card stays on the grid's lines, and every line is drawn on its whole device pixel,
/// so two cards that share a line share its pixel wherever the block goes.
/// </summary>
public sealed class BlockMoveTests
{
    /// <summary>Screens at 100 % to 250 %, their pitch a whole number of device pixels or not.</summary>
    public static TheoryData<double> Scales => [1.0, 1.25, 1.5, 1.75, 2.0, 2.5];

    [Theory]
    [MemberData(nameof(Scales))]
    public void S11873_a_selection_moved_keeps_the_cards_that_share_a_line_on_one_pixel(double scale)
    {
        var screen = new ScreenMetrics(2560 / scale, 1440 / scale);
        var (cols, rows) = GridGeometry.GridDims(screen);
        FracRect Cells(int x, int y, int w, int h) => new((double)x / cols, (double)y / rows, (double)w / cols, (double)h / rows);

        // A column of two cards and one card beside it, all three touching, and a fourth a cell apart.
        FracRect[] group = [Cells(30, 20, 13, 9), Cells(30, 29, 13, 7), Cells(43, 20, 10, 16), Cells(54, 20, 4, 4)];

        // A drag in small fractional steps across the screen, both ways, as the pointer reports it.
        for (var i = 1; i < 240; i++)
        {
            var delta = BlockGeometry.Move(group, (i * 0.00137) - 0.11, (i * 0.00071) - 0.05, screen);
            var cells = group.Select(r => GridGeometry.PaintedCell(BlockGeometry.Apply(r, delta, screen), screen, scale)).ToArray();

            // Touching stays touching, to the pixel: the column's seam, and the card beside it on both.
            Assert.Equal(cells[0].Y + cells[0].Height, cells[1].Y, 9);
            Assert.Equal(cells[0].X + cells[0].Width, cells[2].X, 9);
            Assert.Equal(cells[1].X + cells[1].Width, cells[2].X, 9);
            Assert.Equal(cells[0].Y, cells[2].Y, 9);
            Assert.Equal(cells[1].Y + cells[1].Height, cells[2].Y + cells[2].Height, 9);

            // Apart stays apart: the grid's own cell between them, within a pixel of its pitch.
            Assert.InRange(cells[3].X - (cells[2].X + cells[2].Width), (screen.Width / cols) - 1.5, (screen.Width / cols) + 1.5);
        }
    }

    [Theory]
    [InlineData(1920, 1080, 1.0)]
    [InlineData(1280, 720, 1.5)]
    [InlineData(1280, 720, 2.0)]
    public void S11873_where_the_pitch_is_whole_pixels_every_cards_pixels_keep_their_places(double width, double height, double scale)
    {
        // 10 DIPs a cell, whole pixels at these scales: the whole block's pixels move as one.
        var screen = new ScreenMetrics(width, height);
        var (cols, rows) = GridGeometry.GridDims(screen);
        FracRect[] group = [new(8.0 / cols, 4.0 / rows, 8.0 / cols, 10.0 / rows), new(20.0 / cols, 6.0 / rows, 30.0 / cols, 12.0 / rows)];
        var start = group.Select(r => GridGeometry.PaintedCell(r, screen, scale)).ToArray();
        for (var i = 1; i < 120; i++)
        {
            var delta = BlockGeometry.Move(group, i * 0.00313, i * 0.00177, screen);
            var moved = group.Select(r => GridGeometry.PaintedCell(BlockGeometry.Apply(r, delta, screen), screen, scale)).ToArray();
            var (ox, oy) = (moved[0].X - start[0].X, moved[0].Y - start[0].Y);
            for (var c = 0; c < group.Length; c++)
            {
                Assert.Equal(start[c].Width, moved[c].Width, 9);
                Assert.Equal(start[c].Height, moved[c].Height, 9);
                Assert.Equal(start[c].X + ox, moved[c].X, 9);
                Assert.Equal(start[c].Y + oy, moved[c].Y, 9);
            }
        }
    }

    [Fact]
    public void S11873_a_whole_dip_and_its_pixel_round_half_up()
    {
        Assert.Equal(3, GridGeometry.WholeDip(2.5));
        Assert.Equal(-2, GridGeometry.WholeDip(-2.5));
        Assert.Equal(5 / 1.5, GridGeometry.DevicePixel(3, 1.5), 9);
        Assert.Equal(8 / 1.5, GridGeometry.DevicePixel(5, 1.5), 9);
    }
}
