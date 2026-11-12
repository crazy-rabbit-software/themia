using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Themia.App.Shell;

/// <summary>
/// An element in a live window, or pixels, written to a PNG file: the Debug renders' one writer
/// (<c>RenderPng</c>) and the add page's self-check ([S-11854]).
/// </summary>
internal static class PngFile
{
    /// <summary>Draw <paramref name="element"/> as it is on screen and write it to <paramref name="path"/>.</summary>
    public static async Task SaveAsync(UIElement element, string path)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        var pixels = await bitmap.GetPixelsAsync();
        await EncodeAsync(path, bitmap.PixelWidth, bitmap.PixelHeight, pixels.ToArray()).ConfigureAwait(true);
    }

    /// <summary>Premultiplied BGRA <paramref name="pixels"/> of <paramref name="width"/> × <paramref name="height"/> to <paramref name="path"/>.</summary>
    public static async Task EncodeAsync(string path, int width, int height, byte[] pixels)
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
