using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2460] [S-11872] A block resized keeps every edge its members share shared, on the device pixels they are drawn on.</summary>
public class BlockResizeSharedEdgeTests
{
    [Fact]
    public void S2460_S11872_members_that_touch_still_touch_at_every_step_of_a_block_resize_on_every_display()
    {
        var found = new List<string>();
        foreach (var screen in new[] { new ScreenMetrics(1920, 1080), new ScreenMetrics(1536, 864), new ScreenMetrics(2560, 1440), new ScreenMetrics(1280, 720) })
        {
            var (cols, rows) = GridGeometry.GridDims(screen);
            var limits = new SizeLimits?[]
            {
                new SizeLimits(new AxisLimits(20.0 / cols, 160.0 / cols, cols), new AxisLimits(3.0 / rows, 3.0 / rows, rows)),
                new SizeLimits(new AxisLimits(4.0 / cols, 200.0 / cols, cols), new AxisLimits(3.0 / rows, 30.0 / rows, rows)),
                new SizeLimits(new AxisLimits(20.0 / cols, 160.0 / cols, cols), new AxisLimits(3.0 / rows, 3.0 / rows, rows)),
            };
            FracRect R(int x, int y, int w, int h) => new(x / (double)cols, y / (double)rows, w / (double)cols, h / (double)rows);
            var start = new[] { R(10, 10, 20, 3), R(30, 10, 7, 3), R(37, 10, 23, 3) };
            foreach (var handle in new[] { ResizeHandle.Right, ResizeHandle.Left, ResizeHandle.BottomRight })
            {
                for (var dx = -30.0; dx <= 60; dx += 0.37)
                {
                    var rects = BlockResize.Resize(start, limits, handle, dx / cols, dx / rows, screen);
                    var shown = rects.Select((r, i) => SizeFit.Shown(r, limits[i], screen)).ToArray();
                    for (var i = 0; i + 1 < rects.Count; i++)
                    {
                        var a = GridGeometry.PaintedCell(shown[i], screen, 1.5);
                        var b = GridGeometry.PaintedCell(shown[i + 1], screen, 1.5);
                        if (Math.Abs((a.X + a.Width) - b.X) > 1e-6)
                        {
                            found.Add($"{screen.Width}x{screen.Height} {handle} dx {dx:F2}: {i}->{i + 1} right {a.X + a.Width} left {b.X} stored {rects[i].Right * cols:F6} {rects[i + 1].X * cols:F6} shown {shown[i].Right * cols:F6} {shown[i + 1].X * cols:F6}");
                        }
                    }
                }
            }
        }

        Assert.True(found.Count == 0, string.Join(" | ", found.Take(12)) + " (" + found.Count + " gaps)");
    }
}
