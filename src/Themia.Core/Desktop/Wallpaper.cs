using Themia.Core.Geometry;
using Themia.Core.Widgets;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-2113] How Windows lays its wallpaper on a display: <c>DESKTOP_WALLPAPER_POSITION</c>, value for
/// value, so the host can cast what <c>IDesktopWallpaper::GetPosition</c> returns.
/// </summary>
public enum WallpaperFit
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5,
}

/// <summary>
/// [S-2112] The wallpaper of one display as the cards' backdrop: already placed for that display (its
/// fit and the background colour around it) at <see cref="WallpaperLayout.Downscale"/> of its pixels
/// per side. BGRA, premultiplied, opaque, row after row with no padding.
/// </summary>
public sealed record WallpaperPicture
{
    public WallpaperPicture(int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length != (long)width * height * 4)
        {
            throw new ArgumentException($"a {width}×{height} picture needs {(long)width * height * 4} bytes, not {pixels.Length}", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }
}

/// <summary>
/// [S-2112] The seam the backdrop reads the wallpaper through: a pure contract here, Windows' answer in
/// the host (<c>WindowsWallpaper</c>, <c>IDesktopWallpaper</c>), a stand-in picture in a debug render.
/// </summary>
public interface IWallpaperSource
{
    /// <summary>
    /// The picture Windows shows on the display at <paramref name="display"/> (its bounds in virtual
    /// screen pixels), placed for it ([S-2113]) and scaled down, or null for none: a picture that could
    /// not be read or decoded (logged), or no display Windows knows there. A solid colour is a picture
    /// of that colour.
    /// </summary>
    Task<WallpaperPicture?> PictureAsync(PixelRect display, CancellationToken cancel);

    /// <summary>Windows changed what it shows: a new wallpaper, a slideshow step, a colour, a display.</summary>
    event Action? Changed;
}

/// <summary>Where the image goes in the picture, in picture pixels; <see cref="Repeat"/> tiles it from there.</summary>
public readonly record struct WallpaperPlan(int X, int Y, int Width, int Height, bool Repeat);

/// <summary>[S-2113] [S-2114] The geometry of a backdrop picture: placing the image, composing, and a card's part.</summary>
public static class WallpaperLayout
{
    /// <summary>[S-2112] A backdrop keeps one pixel in this many per side: blurred anyway, and cheap.</summary>
    public const int Downscale = 12;

    /// <summary>[S-2112] The picture's size for a display of <paramref name="display"/>'s pixels.</summary>
    public static (int Width, int Height) PictureSize(PixelRect display)
        => (Math.Max(1, (int)Math.Ceiling(display.Width / Downscale)), Math.Max(1, (int)Math.Ceiling(display.Height / Downscale)));

    /// <summary>
    /// [S-2113] Where an image of <paramref name="imageWidth"/> × <paramref name="imageHeight"/> pixels lies
    /// on <paramref name="display"/>, relative to its top-left, in display pixels, and whether it repeats:
    /// Fill covers it (cropped, centred), Fit fits inside it (the background colour around), Stretch
    /// is its size exactly, Center is the image's own size centred (cropped when larger), Tile repeats
    /// it at its own size from the top-left, Span fills <paramref name="desktop"/> (the bounding box of
    /// every display) and the display shows its part.
    /// </summary>
    public static (PixelRect Rect, bool Repeat) Place(WallpaperFit fit, double imageWidth, double imageHeight, PixelRect display, PixelRect desktop)
    {
        if (!(imageWidth > 0) || !(imageHeight > 0) || !(display.Width > 0) || !(display.Height > 0))
        {
            return (new PixelRect(0, 0, display.Width, display.Height), false);
        }

        switch (fit)
        {
            case WallpaperFit.Stretch:
                return (new PixelRect(0, 0, display.Width, display.Height), false);
            case WallpaperFit.Center:
                return (new PixelRect((display.Width - imageWidth) / 2, (display.Height - imageHeight) / 2, imageWidth, imageHeight), false);
            case WallpaperFit.Tile:
                return (new PixelRect(0, 0, imageWidth, imageHeight), true);
            case WallpaperFit.Fit:
                return (Scaled(display, Math.Min(display.Width / imageWidth, display.Height / imageHeight)), false);
            case WallpaperFit.Span when desktop.Width > 0 && desktop.Height > 0:
            {
                var scale = Math.Max(desktop.Width / imageWidth, desktop.Height / imageHeight);
                var w = imageWidth * scale;
                var h = imageHeight * scale;
                return (new PixelRect(desktop.X + ((desktop.Width - w) / 2) - display.X, desktop.Y + ((desktop.Height - h) / 2) - display.Y, w, h), false);
            }

            default:
                return (Scaled(display, Math.Max(display.Width / imageWidth, display.Height / imageHeight)), false);
        }

        PixelRect Scaled(PixelRect area, double scale)
        {
            var w = imageWidth * scale;
            var h = imageHeight * scale;
            return new PixelRect((area.Width - w) / 2, (area.Height - h) / 2, w, h);
        }
    }

    /// <summary>[S-2112] A placement in display pixels, brought into the picture: one image size for every copy.</summary>
    public static WallpaperPlan Plan(PixelRect placed, bool repeat)
        => new(
            (int)Math.Round(placed.X / Downscale),
            (int)Math.Round(placed.Y / Downscale),
            Math.Max(1, (int)Math.Round(placed.Width / Downscale)),
            Math.Max(1, (int)Math.Round(placed.Height / Downscale)),
            repeat);

    /// <summary>
    /// [S-2113] The picture: <paramref name="background"/> everywhere, the image (<paramref name="plan"/>'s
    /// size, BGRA premultiplied) laid over it where the plan puts it, repeated when it tiles. The image's
    /// own transparency shows the background colour, as Windows does. Never reads outside either buffer.
    /// </summary>
    public static WallpaperPicture Compose(int width, int height, RgbColor background, WallpaperPlan plan, ReadOnlySpan<byte> image)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "a picture has at least one pixel");
        }

        if (image.Length != (long)plan.Width * plan.Height * 4 || plan.Width <= 0 || plan.Height <= 0)
        {
            throw new ArgumentException($"a {plan.Width}×{plan.Height} image needs {(long)plan.Width * plan.Height * 4} bytes, not {image.Length}", nameof(image));
        }

        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = ((y * width) + x) * 4;
                var ix = x - plan.X;
                var iy = y - plan.Y;
                if (plan.Repeat)
                {
                    ix = Mod(ix, plan.Width);
                    iy = Mod(iy, plan.Height);
                }

                if (ix < 0 || iy < 0 || ix >= plan.Width || iy >= plan.Height)
                {
                    pixels[at] = background.B;
                    pixels[at + 1] = background.G;
                    pixels[at + 2] = background.R;
                    pixels[at + 3] = 255;
                    continue;
                }

                // Premultiplied over an opaque colour: c + (1 - a) · bg.
                var from = ((iy * plan.Width) + ix) * 4;
                var rest = 255 - image[from + 3];
                pixels[at] = (byte)Math.Min(255, image[from] + ((rest * background.B) + 127) / 255);
                pixels[at + 1] = (byte)Math.Min(255, image[from + 1] + ((rest * background.G) + 127) / 255);
                pixels[at + 2] = (byte)Math.Min(255, image[from + 2] + ((rest * background.R) + 127) / 255);
                pixels[at + 3] = 255;
            }
        }

        return new WallpaperPicture(width, height, pixels);
    }

    /// <summary>[S-2112] A display Windows draws in one colour (no picture, or one it shows none of): that colour.</summary>
    public static WallpaperPicture Solid(PixelRect display, RgbColor colour)
    {
        var (width, height) = PictureSize(display);
        return Compose(width, height, colour, new WallpaperPlan(0, 0, 1, 1, false), [colour.B, colour.G, colour.R, 255]);
    }

    /// <summary>
    /// [S-2114] The part of a display's picture behind a card, in picture pixels: the card's rectangle
    /// on the display (in DIPs, from the display's top-left) scaled from the display's DIP size to the
    /// picture's. It may reach past the picture where the card does past the display.
    /// </summary>
    public static PixelRect Part(PixelRect card, double displayWidth, double displayHeight, WallpaperPicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (!(displayWidth > 0) || !(displayHeight > 0))
        {
            return default;
        }

        var sx = picture.Width / displayWidth;
        var sy = picture.Height / displayHeight;
        return new PixelRect(card.X * sx, card.Y * sy, card.Width * sx, card.Height * sy);
    }

    private static int Mod(int value, int by) => ((value % by) + by) % by;
}

/// <summary>
/// [S-2115] The backdrop's blur: box blurs over a BGRA pixel buffer, horizontal then vertical, a few
/// passes (three approach a Gaussian). Integer arithmetic, so the same input always gives the same
/// output; an edge repeats its last pixel, so the picture does not darken toward the display's edge,
/// and nothing is ever read outside the buffer.
/// </summary>
public static class BoxBlur
{
    /// <summary>[S-2115] The backdrop's own strength: radius 3 at 1/12 scale, three passes (σ ≈ 40 display pixels).</summary>
    public const int BackdropRadius = 3;

    public const int BackdropPasses = 3;

    /// <summary>Blur <paramref name="pixels"/> (<paramref name="width"/> × <paramref name="height"/>, four bytes a pixel) in place.</summary>
    public static void Apply(byte[] pixels, int width, int height, int radius, int passes)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length != (long)width * height * 4)
        {
            throw new ArgumentException($"a {width}×{height} buffer needs {(long)width * height * 4} bytes, not {pixels.Length}", nameof(pixels));
        }

        if (radius <= 0 || passes <= 0)
        {
            return;
        }

        var scratch = new byte[pixels.Length];
        for (var pass = 0; pass < passes; pass++)
        {
            // Rows into the scratch buffer, then columns back.
            Line(pixels, scratch, lines: height, length: width, step: 4, stride: width * 4, radius);
            Line(scratch, pixels, lines: width, length: height, step: width * 4, stride: 4, radius);
        }
    }

    /// <summary>
    /// One box pass along every line: <paramref name="lines"/> lines of <paramref name="length"/> pixels,
    /// <paramref name="step"/> bytes between neighbours on a line and <paramref name="stride"/> between lines.
    /// A running sum per channel; indexes are clamped into the line, so a read never leaves it.
    /// </summary>
    private static void Line(byte[] from, byte[] to, int lines, int length, int step, int stride, int radius)
    {
        var window = (2 * radius) + 1;
        Span<int> sums = stackalloc int[4];

        for (var line = 0; line < lines; line++)
        {
            var start = line * stride;
            sums.Clear();

            for (var i = -radius; i <= radius; i++)
            {
                var at = start + (Math.Clamp(i, 0, length - 1) * step);
                for (var c = 0; c < 4; c++)
                {
                    sums[c] += from[at + c];
                }
            }

            for (var i = 0; i < length; i++)
            {
                var at = start + (i * step);
                for (var c = 0; c < 4; c++)
                {
                    to[at + c] = (byte)((sums[c] + (window / 2)) / window);
                }

                var leaving = start + (Math.Clamp(i - radius, 0, length - 1) * step);
                var entering = start + (Math.Clamp(i + radius + 1, 0, length - 1) * step);
                for (var c = 0; c < 4; c++)
                {
                    sums[c] += from[entering + c] - from[leaving + c];
                }
            }
        }
    }
}
