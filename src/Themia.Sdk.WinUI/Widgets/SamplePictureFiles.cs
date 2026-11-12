#if DEBUG
using System.Runtime.InteropServices.WindowsRuntime;
using Themia.Sdk.Samples;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Themia.Sdk.WinUI;

/// <summary>
/// Debug renders only ([S-2056]): the sample day's pictures (<see cref="SamplePictures"/>) as PNG
/// bytes, for a preview that takes a picture as a file or as bytes (the music art, the gallery, the
/// folder widget's pictures folder).
/// </summary>
public static class SamplePictureFiles
{
    /// <summary>Picture <paramref name="index"/> as a PNG of <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static Task<byte[]> PngAsync(int index, int width, int height) =>
        EncodeAsync(SamplePictures.Pixels(index, width, height), width, height);

    /// <summary>The sample track's album cover (<see cref="SamplePictures.AlbumCover"/>) as a PNG of <paramref name="size"/> square.</summary>
    public static Task<byte[]> CoverPngAsync(int size) =>
        EncodeAsync(SamplePictures.AlbumCover(size, size), size, size);

    /// <summary>
    /// [S-2210] The sample picture named <paramref name="slug"/> (<see cref="SamplePictures.WallpaperSlugs"/>)
    /// painted at <paramref name="width"/> × <paramref name="height"/> off the UI thread, as a PNG; null for a name there is not.
    /// </summary>
    public static async Task<byte[]?> WallpaperPngAsync(string slug, int width, int height)
    {
        var pixels = await Task.Run(() => SamplePictures.Wallpaper(slug, width, height)).ConfigureAwait(true);
        return pixels is null ? null : await EncodeAsync(pixels, width, height).ConfigureAwait(true);
    }

    private static async Task<byte[]> EncodeAsync(byte[] pixels, int width, int height)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync();

        var bytes = new byte[stream.Size];
        stream.Seek(0);
        await stream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
        return bytes;
    }

    /// <summary>
    /// <paramref name="count"/> pictures written as <c>%TEMP%\themia-sample\<paramref name="folder"/></c> with the
    /// given names (cycled), landscape at 1280 × 800 unless a name asks otherwise; the folder's path, or null (logged).
    /// </summary>
    public static async Task<string?> WriteAsync(string folder, IReadOnlyList<string> names, int width = 1280, int height = 800)
    {
        ArgumentNullException.ThrowIfNull(names);
        var files = new List<SampleFile>();
        for (var i = 0; i < names.Count; i++)
        {
            var bytes = await PngAsync(i, width, height).ConfigureAwait(true);
            files.Add(new SampleFile(names[i], bytes.Length, 24 * (i + 1) * 3.5, Bytes: bytes));
        }

        return SampleFiles.Write(folder, files);
    }
}
#endif
