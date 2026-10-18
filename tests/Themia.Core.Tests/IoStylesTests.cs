using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The styles of the network, disk, battery, ping and drive widgets ([S-6261]..[S-6274]): which
/// styles each kind has, what each draws, and the geometry of the battery, the signal bars, the
/// numbers and the ring faces.
/// </summary>
public sealed class IoStylesTests
{
    private const double Mb = 1024 * 1024;

    // ---- [S-6263] signal bars ------------------------------------------------------------------

    [Theory]
    [InlineData(null, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(-1.0, 0)]
    [InlineData(0.0, 4)]
    [InlineData(50.0, 4)]
    [InlineData(50.5, 3)]
    [InlineData(100.0, 3)]
    [InlineData(150.0, 2)]
    [InlineData(200.0, 2)]
    [InlineData(201.0, 1)]
    [InlineData(5000.0, 1)]
    public void S6263_the_bars_a_probe_earns_follow_the_latency(double? ms, int bars)
        => Assert.Equal(bars, PingSignal.Level(ms));

    [Fact]
    public void S6263_the_bars_rise_left_to_right_on_one_baseline_inside_their_box()
    {
        var bars = PingSignal.Bars(100, 100);

        Assert.Equal(PingSignal.BarCount, bars.Count);
        var height = bars[^1].Height;
        Assert.True(height <= 100 && bars[^1].Right() <= 100.5);

        for (var i = 0; i < bars.Count; i++)
        {
            Assert.Equal(height, bars[i].Y + bars[i].Height, 3);
            if (i > 0)
            {
                Assert.True(bars[i].Height > bars[i - 1].Height);
                Assert.True(bars[i].X > bars[i - 1].Right(), "a gap between bars");
            }
        }

        Assert.Empty(PingSignal.Bars(0, 50));
    }

    // ---- [S-6262] the battery shape ------------------------------------------------------------

    [Fact]
    public void S6262_the_battery_fits_its_box_with_the_cap_centred_and_the_fill_inside()
    {
        var shape = BatteryShape.Fit(300, 100);

        Assert.Equal(shape.Body.Height * BatteryShape.BodyAspect, shape.Body.Width, 0);
        Assert.True(shape.Cap.X + shape.Cap.Width <= 300 && shape.Body.Y >= 0 && shape.Body.Y + shape.Body.Height <= 100);
        Assert.Equal(shape.Body.Y + (shape.Body.Height / 2), shape.Cap.Y + (shape.Cap.Height / 2), 0);
        Assert.Equal(shape.Body.X + shape.Body.Width, shape.Cap.X);

        // Centred across the wider box.
        Assert.Equal(300 - (shape.Cap.X + shape.Cap.Width), shape.Body.X, 0);

        Assert.True(shape.Inner.X > shape.Body.X + shape.Stroke && shape.Inner.Y > shape.Body.Y + shape.Stroke);
        Assert.True(shape.Inner.X + shape.Inner.Width < shape.Body.X + shape.Body.Width - shape.Stroke);
        Assert.Equal(default, BatteryShape.Fit(0, 100));
    }

    [Fact]
    public void S6262_the_fill_is_the_charge_and_any_charge_shows()
    {
        var shape = BatteryShape.Fit(210, 100);

        Assert.Equal(0, BatteryShape.FillWidth(shape, 0));
        Assert.Equal(0, BatteryShape.FillWidth(shape, double.NaN));
        Assert.True(BatteryShape.FillWidth(shape, 0.001) >= shape.Stroke);
        Assert.Equal(shape.Inner.Width / 2, BatteryShape.FillWidth(shape, 0.5), 3);
        Assert.Equal(shape.Inner.Width, BatteryShape.FillWidth(shape, 1));
        Assert.Equal(shape.Inner.Width, BatteryShape.FillWidth(shape, 7));
    }

    // ---- [S-6261] a picture and its figure ----------------------------------------------------

    [Fact]
    public void S6261_a_wide_strip_puts_the_picture_beside_the_figure_and_a_tile_under_it()
    {
        var figure = HeroBound.Of(Formatting.Percent(1));

        var strip = PictureFace.Plan(new WidgetArea(448, 144), BatteryShape.Aspect, figure, wide: true, FluentType.Title);
        Assert.Equal(PictureArrangement.Beside, strip.Arrangement);
        Assert.True(strip.PictureWidth + PictureFace.Gap + HeroLayout.ValueWidth(strip.Figure, figure.Value, figure.Unit) <= 448);

        var tile = PictureFace.Plan(new WidgetArea(288, 224), BatteryShape.Aspect, figure, wide: false, FluentType.Title);
        Assert.Equal(PictureArrangement.Under, tile.Arrangement);
        Assert.True(tile.PictureHeight + PictureFace.Gap + tile.Figure.LineHeight <= 224);
        Assert.Equal(BatteryShape.Aspect, tile.PictureWidth / tile.PictureHeight, 1);

        // No room for a picture worth drawing: the figure alone, never a smudge.
        var sliver = PictureFace.Plan(new WidgetArea(200, 40), BatteryShape.Aspect, figure, wide: false, FluentType.Title);
        Assert.Equal(PictureArrangement.FigureOnly, sliver.Arrangement);
    }

    [Fact]
    public void S6261_the_picture_grows_with_the_tile()
    {
        var figure = HeroBound.Of(Formatting.Latency(999));
        var small = PictureFace.Plan(new WidgetArea(168, 134), PingSignal.Aspect, figure, false, FluentType.Title);
        var large = PictureFace.Plan(new WidgetArea(528, 404), PingSignal.Aspect, figure, false, FluentType.TitleLarge);

        Assert.True(large.PictureHeight > small.PictureHeight);
        Assert.True(large.Figure.FontSize >= small.Figure.FontSize);
    }

    // ---- [S-6264] the network's numbers -------------------------------------------------------

    [Fact]
    public void S6264_two_numbers_stack_on_a_tile_and_stand_side_by_side_on_a_strip()
    {
        var bound = HeroBound.Of(Formatting.Rate(1023 * 1024));

        var tile = MeterNumbers.Plan(new WidgetArea(288, 224), bound, icon: true, gap: 12);
        Assert.False(tile.Side);

        var strip = MeterNumbers.Plan(new WidgetArea(768, 120), bound, icon: true, gap: 12);
        Assert.True(strip.Side);

        // Both lines fit the tile.
        Assert.True((2 * tile.Step.LineHeight) + 12 <= 224);

        var xl = MeterNumbers.Plan(new WidgetArea(768, 544), bound, icon: true, gap: 12);
        Assert.True(xl.Step.FontSize > tile.Step.FontSize);
    }

    // ---- [S-6266] a ring and its lines --------------------------------------------------------

    [Fact]
    public void S6266_a_ring_takes_what_its_lines_leave_beside_on_a_strip_under_on_a_tile()
    {
        double[] lines = [20, 16];

        var strip = RingFace.Plan(new WidgetArea(448, 144), wide: true, lines, 4);
        Assert.True(strip.Beside);
        Assert.Equal(2, strip.Lines);
        Assert.Equal(144, strip.Diameter);

        var tile = RingFace.Plan(new WidgetArea(288, 224), wide: false, lines, 4);
        Assert.False(tile.Beside);
        Assert.Equal(2, tile.Lines);
        Assert.Equal(224 - 20 - 4 - 16 - RingFace.Gap, tile.Diameter);

        // A small tile drops the lines before the ring falls under its least size.
        var small = RingFace.Plan(new WidgetArea(88, 80), wide: false, lines, 4);
        Assert.True(small.Lines < 2);
        Assert.True(small.Diameter >= RingFace.MinRing || small.Lines == 0);

        var alone = RingFace.Plan(new WidgetArea(288, 224), wide: false, [], 0);
        Assert.Equal((false, 224.0, 0), (alone.Beside, alone.Diameter, alone.Lines));
    }

    [Fact]
    public void S6266_the_figure_inside_a_ring_fits_its_hole_and_grows_with_it()
    {
        var bound = HeroBound.Of(Formatting.Percent(1));

        foreach (var diameter in (double[])[48, 96, 200, 400])
        {
            var step = RingFace.Figure(diameter, bound);
            Assert.True(HeroLayout.ValueWidth(step, bound.Value, bound.Unit) <= RingFace.Hole(diameter) || step == FluentType.Caption);
        }

        Assert.True(RingFace.Figure(400, bound).FontSize > RingFace.Figure(96, bound).FontSize);
    }

    // ---- [S-6271] the disk's read and write bars ----------------------------------------------

    [Fact]
    public void S6271_the_bars_read_on_the_graphs_own_top()
    {
        // Idle: a background murmur against the 1 MB/s floor stays a sliver.
        var (read, write) = DiskRules.BarShares(new DiskRates(0.1 * Mb, 0.05 * Mb), []);
        Assert.Equal(0.1, read, 3);
        Assert.Equal(0.05, write, 3);

        // A copy at 40 MB/s: the top is the doubling past it (64 MB/s), the larger bar the larger share.
        (read, write) = DiskRules.BarShares(new DiskRates(32 * Mb, 8 * Mb), []);
        Assert.Equal(0.5, read, 3);
        Assert.Equal(0.125, write, 3);

        // The window's peak holds the top up after the copy ends, as the graph's does.
        (read, _) = DiskRules.BarShares(new DiskRates(2 * Mb, 0), [100 * Mb]);
        Assert.Equal(2.0 / 128, read, 3);

        // A bad rate is no bar.
        (read, _) = DiskRules.BarShares(new DiskRates(double.NaN, 0), []);
        Assert.Equal(0, read);
    }
}

file static class RectExtensions
{
    public static double Right(this Themia.Core.Geometry.PixelRect rect) => rect.X + rect.Width;
}
