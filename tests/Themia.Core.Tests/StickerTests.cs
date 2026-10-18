using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-774]..[S-777] The Gallery as a sticker: any-angle fit, the outline along the picture's shape.</summary>
public sealed class StickerTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(180, 180)]
    [InlineData(-180, 180)]
    [InlineData(270, -90)]
    [InlineData(-270, 90)]
    [InlineData(725, 5)]
    [InlineData(-190, 170)]
    public void S774_any_angle_is_one_turn_in_minus_180_to_180(int degrees, int expected)
        => Assert.Equal(expected, StickerRules.NormalizeDegrees(degrees));

    [Fact]
    public void S774_an_upright_picture_fits_by_its_own_shape()
    {
        var box = StickerRules.FitRotated(400, 300, 0, aspect: 2.0);

        Assert.Equal(400, box.Width, 6);
        Assert.Equal(200, box.Height, 6);
    }

    [Fact]
    public void S468_a_quarter_turn_without_an_aspect_is_the_old_axis_swap()
    {
        var turned = StickerRules.FitRotated(320, 240, 90, aspect: null);
        var upright = StickerRules.FitRotated(320, 240, 0, aspect: null);
        var half = StickerRules.FitRotated(320, 240, 180, aspect: null);

        Assert.Equal(240, turned.Width, 6);
        Assert.Equal(320, turned.Height, 6);
        Assert.Equal(320, upright.Width, 6);
        Assert.Equal(240, upright.Height, 6);
        Assert.Equal(320, half.Width, 6);
        Assert.Equal(240, half.Height, 6);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(45)]
    [InlineData(-30)]
    [InlineData(135)]
    [InlineData(179)]
    public void S774_a_turned_box_fits_the_frame_exactly_on_one_axis(double degrees)
    {
        const double frameW = 300, frameH = 200, aspect = 1.5;
        var box = StickerRules.FitRotated(frameW, frameH, degrees, aspect);
        var r = degrees * Math.PI / 180;
        var spanW = (box.Width * Math.Abs(Math.Cos(r))) + (box.Height * Math.Abs(Math.Sin(r)));
        var spanH = (box.Width * Math.Abs(Math.Sin(r))) + (box.Height * Math.Abs(Math.Cos(r)));

        Assert.Equal(aspect, box.Width / box.Height, 6);
        Assert.True(spanW <= frameW + 1e-6 && spanH <= frameH + 1e-6);
        Assert.True(Math.Abs(spanW - frameW) < 1e-6 || Math.Abs(spanH - frameH) < 1e-6);
    }

    [Fact]
    public void S774_an_empty_or_broken_frame_fits_nothing()
    {
        Assert.Equal(new BoxSize(0, 0), StickerRules.FitRotated(0, 100, 10, 1));
        Assert.Equal(new BoxSize(0, 0), StickerRules.FitRotated(double.NaN, 100, 10, 1));
    }

    [Fact]
    public void S775_one_pixel_grows_into_a_round_band()
    {
        const int pad = 3;
        var coverage = StickerRules.Outline([255], 1, 1, pad);
        var size = 1 + (2 * pad);

        Assert.Equal(size * size, coverage.Length);
        Assert.Equal(255, coverage[(pad * size) + pad]); // the pixel itself
        Assert.Equal(255, coverage[(pad * size) + 1]); // two pixels to its left: inside the band
        Assert.Equal(255, coverage[(1 * size) + pad]); // and above
        Assert.Equal(128, coverage[(pad * size) + 0]); // exactly the pad away: the half-pixel soft edge
        Assert.Equal(0, coverage[0]); // a corner of the square is outside the circle
        Assert.Equal(0, coverage[(size * size) - 1]);
    }

    [Fact]
    public void S775_faint_pixels_are_not_the_shape_and_nothing_opaque_is_no_outline()
    {
        var faint = StickerRules.Outline([100, 100, 100, 100], 2, 2, 2);
        var none = StickerRules.Outline([], 0, 0, 2);

        Assert.All(faint, c => Assert.Equal(0, c));
        Assert.Equal(16, none.Length);
        Assert.All(none, c => Assert.Equal(0, c));
    }

    [Fact]
    public void S775_the_band_follows_the_shape_not_the_picture_box()
    {
        // A 5x5 picture with only its middle pixel opaque: the band is round about that pixel and
        // never reaches the picture's own corners.
        var alpha = new byte[25];
        alpha[12] = 255;
        var coverage = StickerRules.Outline(alpha, 5, 5, 1);
        const int size = 7;

        Assert.Equal(255, coverage[(3 * size) + 3]);
        Assert.Equal(128, coverage[(3 * size) + 2]); // one pixel out, at a pad of one: the soft edge
        Assert.Equal(0, coverage[(1 * size) + 1]); // the picture's own top-left corner
        Assert.Equal(0, coverage[(3 * size) + 0]);
    }

    [Fact]
    public void S775_a_square_gains_the_pad_on_every_side_with_a_soft_edge()
    {
        var alpha = Enumerable.Repeat((byte)255, 16).ToArray();
        var coverage = StickerRules.Outline(alpha, 4, 4, 2);
        const int size = 8;

        Assert.Equal(255, coverage[(4 * size) + 1]); // one pixel left of the square: inside the band
        Assert.Equal(255, coverage[(1 * size) + 4]);
        Assert.Equal(128, coverage[(4 * size) + 0]); // the pad away: the soft edge
        Assert.InRange(coverage[0], (byte)0, (byte)254); // the diagonal corner is past the round band's edge
    }

    [Fact]
    public void S775_a_wrong_alpha_length_is_refused()
        => Assert.Throws<ArgumentException>(() => StickerRules.Outline([1, 2, 3], 2, 2, 1));

    [Fact]
    public void S775_the_pad_shows_the_asked_width_once_fitted()
    {
        // A 1000x500 picture in a 400x200 frame is shown at 0.4 DIP a pixel or a little less once padded.
        var pad = StickerRules.OutlinePad(400, 200, 1000, 500, 0, 6);
        var box = StickerRules.FitRotated(400, 200, 0, (1000 + (2.0 * pad)) / (500 + (2.0 * pad)));
        var shown = pad * box.Width / (1000 + (2.0 * pad));

        Assert.InRange(shown, 5.5, 6.5);
        Assert.Equal(0, StickerRules.OutlinePad(400, 200, 0, 500, 0, 6));
        Assert.Equal(0, StickerRules.OutlinePad(400, 200, 1000, 500, 0, 0));
    }
}
