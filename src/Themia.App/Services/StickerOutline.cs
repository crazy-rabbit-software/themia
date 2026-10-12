using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Themia.App.Services;

/// <summary>[S-775] An outline ready to draw, and the picture size and pad it was worked out for.</summary>
/// <param name="Bitmap">The band, in the outline's colour, on a canvas <paramref name="Pad"/> larger on every side.</param>
/// <param name="PictureWidth">The picture's width in the pixels the outline was worked out at.</param>
/// <param name="PictureHeight">The picture's height in those pixels.</param>
/// <param name="Pad">The band's width in those pixels.</param>
internal sealed record StickerOutlineImage(WriteableBitmap Bitmap, int PictureWidth, int PictureHeight, int Pad);

/// <summary>
/// [S-775] [S-777] The outline of a picture's own shape, for a sticker. WinUI has no stroke for an
/// image's alpha: a composition drop shadow on the alpha mask is soft, not an edge, and is not drawn
/// by the debug renders. So the picture is decoded once more at the size it is shown (its alpha is
/// all that is read), the band is worked out on the thread pool by <see cref="StickerRules.Outline"/>,
/// and drawn as a bitmap under the picture. Asked again only when the picture, the colour, the
/// width or the size class of the frame changes.
/// </summary>
internal static class StickerOutline
{
    private const string Scope = "sticker-outline";

    /// <summary>Past this a file is not read for its outline (the picture itself still shows).</summary>
    private const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Work out the outline of the picture at <paramref name="path"/>, fitted into a
    /// <paramref name="frameWidth"/> x <paramref name="frameHeight"/> DIP frame turned by
    /// <paramref name="degrees"/>, <paramref name="strokeDips"/> wide. UI thread (the bitmap is made
    /// on it); null, logged, when the picture cannot be read or has no shape to follow.
    /// </summary>
    public static async Task<StickerOutlineImage?> BuildAsync(
        string path,
        double frameWidth,
        double frameHeight,
        double rasterScale,
        double degrees,
        double strokeDips,
        string colour,
        CancellationToken cancel)
    {
        if (!HexColor.TryParse(colour, out var red, out var green, out var blue))
        {
            (red, green, blue) = (255, 255, 255);
        }

        var longest = (int)Math.Min(StickerRules.MaxOutlineSide, Math.Ceiling(Math.Max(frameWidth, frameHeight) * rasterScale / 64) * 64);
        if (longest <= 0)
        {
            return null;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            var properties = await file.GetBasicPropertiesAsync();
            if (properties.Size > MaxFileBytes)
            {
                Log.Info(Scope, $"{properties.Size} bytes is too large a picture to outline; it is shown without one");
                return null;
            }

            using var stream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            cancel.ThrowIfCancellationRequested();

            if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0)
            {
                return null;
            }

            // Never scaled up: an outline worked out at more pixels than the picture has is no sharper.
            var scale = Math.Min(1.0, longest / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
            using var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform
                {
                    ScaledWidth = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * scale)),
                    ScaledHeight = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * scale)),
                    InterpolationMode = BitmapInterpolationMode.Fant,
                },
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);
            cancel.ThrowIfCancellationRequested();

            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var pixels = new byte[width * height * 4];
            bitmap.CopyToBuffer(pixels.AsBuffer());

            var pad = StickerRules.OutlinePad(frameWidth, frameHeight, width, height, degrees, strokeDips);
            var band = await Task.Run(() => Band(pixels, width, height, pad, red, green, blue), cancel).ConfigureAwait(true);

            if (band is null)
            {
                return null;
            }

            var outline = new WriteableBitmap(width + (2 * pad), height + (2 * pad));
            band.CopyTo(outline.PixelBuffer);
            outline.Invalidate();
            return new StickerOutlineImage(outline, width, height, pad);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or System.Runtime.InteropServices.COMException)
        {
            // A file that went away or one with no codec: the picture shows (or says why) without an outline.
            Log.Warn(Scope, $"could not outline '{Path.GetFileName(path)}'", ex);
            return null;
        }
    }

    /// <summary>The band in premultiplied BGRA, or null when the picture has no opaque pixel to follow. Thread pool.</summary>
    private static byte[]? Band(byte[] bgra, int width, int height, int pad, byte red, byte green, byte blue)
    {
        var alpha = new byte[width * height];

        for (var i = 0; i < alpha.Length; i++)
        {
            alpha[i] = bgra[(i * 4) + 3];
        }

        var coverage = StickerRules.Outline(alpha, width, height, pad);

        if (Array.TrueForAll(coverage, c => c == 0))
        {
            return null;
        }

        var band = new byte[coverage.Length * 4];

        for (var i = 0; i < coverage.Length; i++)
        {
            var c = coverage[i];
            band[(i * 4) + 0] = (byte)((blue * c) / 255);
            band[(i * 4) + 1] = (byte)((green * c) / 255);
            band[(i * 4) + 2] = (byte)((red * c) / 255);
            band[(i * 4) + 3] = c;
        }

        return band;
    }
}
