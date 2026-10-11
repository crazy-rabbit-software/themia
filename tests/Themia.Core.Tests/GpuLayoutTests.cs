using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1207] How several adapters share the GPU tile, and [S-1200] its history.</summary>
public sealed class GpuLayoutTests
{
    [Theory]
    [InlineData(480, 116, 2, 2, 1)]  // a strip: side by side, not two slivers
    [InlineData(120, 76, 2, 1, 2)]   // XS: two "name  number" lines, not two 54 px columns
    [InlineData(200, 106, 2, 1, 2)]
    [InlineData(320, 196, 2, 1, 2)]
    [InlineData(200, 356, 2, 1, 2)]  // a column: stacked
    [InlineData(560, 376, 2, 1, 2)]
    [InlineData(800, 516, 2, 1, 2)]
    [InlineData(800, 516, 3, 1, 3)]
    [InlineData(800, 300, 4, 2, 2)]
    [InlineData(120, 76, 1, 1, 1)]
    public void S1207_adapters_share_the_tile_without_scrolling(double width, double height, int count, int columns, int rows)
    {
        Assert.Equal((columns, rows), GpuLayout.Grid(new WidgetArea(width, height), count, 12));
    }

    [Fact]
    public void S1207_cells_split_the_area_less_the_gaps()
    {
        var cell = GpuLayout.Cell(new WidgetArea(500, 200), 2, 1, 12);

        Assert.Equal(244, cell.Width);
        Assert.Equal(200, cell.Height);
    }

    [Fact]
    public void S1200_the_chart_holds_the_span_at_the_refresh()
    {
        var config = new GpuConfig { GraphSpanSeconds = 300 };

        Assert.Equal(150, config.GraphCapacity);
    }
}
