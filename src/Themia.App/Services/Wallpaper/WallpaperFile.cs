using System.Runtime.InteropServices.WindowsRuntime;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Windows.Graphics.Imaging;

namespace Themia.App.Services.Wallpaper;

/// <summary>
/// [S-2112] [S-2113] A wallpaper file made into a display's backdrop picture: decoded straight to the
/// size it has in the picture (the decoder scales, so a photo of many megapixels is never held at full
/// size), placed by its fit over the background colour. Shared by Windows' source and a render's stand-in.
/// </summary>
internal static class WallpaperFile
{
    private const string Scope = "wallpaper";

    /// <summary>A wallpaper larger than this is not read: no picture Windows shows is near it.</summary>
    private const long MaxBytes = 512L * 1024 * 1024;

    /// <summary>
    /// The picture for <paramref name="display"/>: the file at <paramref name="path"/> placed by
    /// <paramref name="fit"/> over <paramref name="background"/>, or the colour alone for no file. Null
    /// (and logged) for a file that cannot be read or decoded: absent and corrupt are told apart.
    /// </summary>
    public static async Task<WallpaperPicture?> PlaceAsync(string path, WallpaperFit fit, RgbColor background, PixelRect display, PixelRect desktop, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return WallpaperLayout.Solid(display, background);
        }

        var name = Path.GetFileName(path);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                Log.Warn(Scope, $"the wallpaper '{name}' is not there; the cards draw without the blurred wallpaper");
                return null;
            }

            if (info.Length == 0 || info.Length > MaxBytes)
            {
                Log.Warn(Scope, $"the wallpaper '{name}' is {info.Length} bytes; not decoded");
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancel).ConfigureAwait(false);
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer()).AsTask(cancel).ConfigureAwait(false);
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancel).ConfigureAwait(false);
            if (decoder.OrientedPixelWidth == 0 || decoder.OrientedPixelHeight == 0)
            {
                Log.Warn(Scope, $"the wallpaper '{name}' has no pixels");
                return null;
            }

            var (placed, repeat) = WallpaperLayout.Place(fit, decoder.OrientedPixelWidth, decoder.OrientedPixelHeight, display, desktop);
            var plan = WallpaperLayout.Plan(placed, repeat);

            // The transform's scale is in the decoder's own orientation; a quarter-turned picture swaps the two.
            var turned = decoder.OrientedPixelWidth != decoder.PixelWidth;
            var data = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform
                {
                    ScaledWidth = (uint)(turned ? plan.Height : plan.Width),
                    ScaledHeight = (uint)(turned ? plan.Width : plan.Height),
                    InterpolationMode = BitmapInterpolationMode.Fant,
                },
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb).AsTask(cancel).ConfigureAwait(false);

            var pixels = data.DetachPixelData();
            var (width, height) = WallpaperLayout.PictureSize(display);
            return WallpaperLayout.Compose(width, height, background, plan, pixels);
        }
        catch (OperationCanceledException)
        {
            return null; // ignore: a newer change asked for the picture again.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, $"the wallpaper '{name}' could not be read: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // Not a picture Windows decodes (or one it shows through a filter Themia has not got): no blur.
            Log.Error(Scope, $"the wallpaper '{name}' could not be decoded", ex);
            return null;
        }
    }
}
