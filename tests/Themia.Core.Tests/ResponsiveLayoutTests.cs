using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-662] [S-663] What a view measures its fixed parts with: its slot, and whole lines.</summary>
public class ResponsiveLayoutTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(double.NaN, 100)]
    [InlineData(100, double.PositiveInfinity)]
    [InlineData(-5, 300)]
    public void S663_an_unmeasured_area_says_so(double width, double height)
        => Assert.False(new WidgetArea(width, height).IsMeasured);

    [Fact]
    public void S663_deflate_never_goes_negative()
    {
        var inner = new WidgetArea(20, 10).Deflate(16, 8);

        Assert.Equal(0, inner.Width);
        Assert.Equal(0, inner.Height);
    }

    [Theory]
    [InlineData(100, 20, 0, 5)]
    [InlineData(99, 20, 0, 4)]
    [InlineData(99.6, 20, 0, 5)]    // rounding tolerance
    [InlineData(19, 20, 0, 0)]
    [InlineData(100, 20, 5, 4)]     // 4 lines + 3 gaps = 95
    [InlineData(115, 20, 5, 4)]     // 5 lines + 4 gaps = 120 > 115
    [InlineData(120, 20, 5, 5)]
    [InlineData(0, 20, 0, 0)]
    [InlineData(100, 0, 0, 0)]
    [InlineData(double.NaN, 20, 0, 0)]
    public void S662_whole_lines_count_only_lines_that_fit_whole(double height, double line, double gap, int expected)
        => Assert.Equal(expected, LayoutMath.WholeLines(height, line, gap));

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 2 / 3.0)]
    [InlineData(2.0, 0.5)]
    [InlineData(0.0, 1.0)]
    [InlineData(double.NaN, 1.0)]
    public void S662_the_one_rounding_token_is_a_device_pixel_and_pure_sizing_takes_it_at_100(double raster, double pixel)
    {
        Assert.Equal(pixel, LayoutMath.Pixel(raster), 9);
        Assert.Equal(LayoutMath.Tolerance, LayoutMath.Pixel(1) / 2);
        Assert.Equal(LayoutMath.Slack, 2 * LayoutMath.Pixel(1));
    }
}
