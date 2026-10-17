#if DEBUG
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Themia.App.Shell.Gallery;

/// <summary>The one way every debug render flag writes what it drew: an element in a live window, to a PNG.</summary>
internal static class RenderPng
{
    public static async Task SaveAsync(UIElement element, string path)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        var pixels = await bitmap.GetPixelsAsync();

        // Encoded straight into the file's stream. Reading the encoded bytes back out of an
        // InMemoryRandomAccessStream through a DataReader blocked the UI thread for good in a Native
        // AOT build, so the render never finished; a file stream has nothing to read back.
        using var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var stream = file.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight,
            96,
            96,
            pixels.ToArray());
        await encoder.FlushAsync();
    }
}
#endif
