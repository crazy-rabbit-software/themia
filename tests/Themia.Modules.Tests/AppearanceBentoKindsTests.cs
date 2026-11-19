using Themia.Core.Geometry;
using Themia.Core.Settings;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2311] The Appearance preview's bento over the real widgets: every tile a kind there is, on a box its content fits.</summary>
public sealed class AppearanceBentoKindsTests
{
    [Fact]
    public void S2311_every_bento_tile_holds_its_kinds_least_size_on_the_grid()
    {
        // A 1920-wide display's grid: its cell in DIPs, as the preview lays the bento on it.
        var screen = new ScreenMetrics(1920, 1080);
        var (cols, rows) = GridGeometry.GridDims(screen);
        var (cellW, cellH) = (screen.Width / cols, screen.Height / rows);

        var tiles = AppearanceBento.Tiles;
        var boxes = AppearanceBento.Boxes(tiles);
        for (var i = 0; i < tiles.Count; i++)
        {
            Assert.True(ModuleCatalog.TryGet(tiles[i].Id, out var descriptor), $"{tiles[i].Kind} is no widget");
            var min = descriptor!.SizingOf().Min;
            Assert.True(boxes[i].Width * cellW >= min.Width - 0.5, $"{tiles[i].Kind}: {boxes[i].Width * cellW} wide, its content needs {min.Width}");
            Assert.True(boxes[i].Height * cellH >= min.Height - 0.5, $"{tiles[i].Kind}: {boxes[i].Height * cellH} tall, its content needs {min.Height}");
        }
    }
}
