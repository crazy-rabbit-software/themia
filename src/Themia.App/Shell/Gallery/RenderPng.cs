#if DEBUG
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Themia.App.Shell.Gallery;

/// <summary>The one way every debug render flag writes what it drew: an element in a live window, to a PNG.</summary>
internal static class RenderPng
{
    /// <summary>
    /// The longest side a <see cref="RenderTargetBitmap"/> draws at: a larger element comes back
    /// scaled down to it (seen: 5120 × 2880 came back 4096 × 2275), so anything larger is drawn in tiles.
    /// </summary>
    public const int MaxSide = 4096;

    public static async Task SaveAsync(UIElement element, string path)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        var pixels = await bitmap.GetPixelsAsync();
        await EncodeAsync(path, bitmap.PixelWidth, bitmap.PixelHeight, pixels.ToArray()).ConfigureAwait(true);
    }

    /// <summary>
    /// [S-1074] A picture of exactly <paramref name="width"/> × <paramref name="height"/> pixels, drawn
    /// tile by tile: <paramref name="show"/> makes <paramref name="element"/> show one tile (at most
    /// <paramref name="step"/> a side, at most <see cref="MaxSide"/>), which must then come out that
    /// size, or this throws before anything is written. One pixel more at a tile's far edges is
    /// cropped: an end that is not a whole DIP of the window (683.33 DIPs at 150 %) is rounded up
    /// when the bitmap is sized, while its start is exact.
    /// </summary>
    public static async Task SaveTiledAsync(UIElement element, string path, int width, int height, int step, Func<(int X, int Y, int Width, int Height), Task> show)
    {
        var all = new byte[checked(width * height * 4)];
        step = Math.Clamp(step, 1, MaxSide);
        for (var y = 0; y < height; y += step)
        {
            for (var x = 0; x < width; x += step)
            {
                var tile = (X: x, Y: y, Width: Math.Min(step, width - x), Height: Math.Min(step, height - y));
                await show(tile).ConfigureAwait(true);

                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(element);
                if (bitmap.PixelWidth - tile.Width is not (0 or 1) || bitmap.PixelHeight - tile.Height is not (0 or 1))
                {
                    throw new InvalidOperationException($"the tile at {x}, {y} came out {bitmap.PixelWidth} x {bitmap.PixelHeight} pixels, not {tile.Width} x {tile.Height}");
                }

                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                for (var row = 0; row < tile.Height; row++)
                {
                    Buffer.BlockCopy(pixels, row * bitmap.PixelWidth * 4, all, (((y + row) * width) + x) * 4, tile.Width * 4);
                }
            }
        }

        await EncodeAsync(path, width, height, all).ConfigureAwait(true);
    }

    private static async Task EncodeAsync(string path, int width, int height, byte[] pixels)
    {
        // Encoded straight into the file's stream. Reading the encoded bytes back out of an
        // InMemoryRandomAccessStream through a DataReader blocked the UI thread for good in a Native
        // AOT build, so the render never finished; a file stream has nothing to read back.
        using var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var stream = file.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)width,
            (uint)height,
            96,
            96,
            pixels);
        await encoder.FlushAsync();
    }
}
#endif
