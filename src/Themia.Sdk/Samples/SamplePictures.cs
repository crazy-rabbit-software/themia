#if DEBUG
namespace Themia.Sdk.Samples;

/// <summary>
/// Debug renders only ([S-2056]): the pictures of the sample day, drawn in code (soft abstract
/// gradients) so no preview shows a picture read from this PC: the music widget's album art, the
/// gallery's pictures and the folder widget's pictures folder. Picture <c>n</c> is the same on every
/// run and different from every other.
/// </summary>
public static class SamplePictures
{
    /// <summary>Calm palettes, three colours each (deep, middle, light), as 0xRRGGBB.</summary>
    private static readonly int[][] Palettes =
    [
        [0x1B2A49, 0x3A6EA5, 0xF2B880],
        [0x2D1E3E, 0x7B4B94, 0xF4A5AE],
        [0x0F3B3A, 0x2F8F83, 0xD9E7A3],
        [0x3B1F1F, 0xB8553A, 0xF6D6A8],
        [0x14213D, 0x4F6D7A, 0xC0D6DF],
        [0x26302A, 0x6B8F71, 0xF0E2B6],
        [0x301934, 0xC05746, 0xF3C677],
        [0x102A43, 0x627D98, 0xF0B429],
        [0x1E2D24, 0x5F9EA0, 0xE8D7F1],
        [0x2B2D42, 0x8D99AE, 0xEF8354],
        [0x22223B, 0x9A8C98, 0xF2E9E4],
        [0x0B3954, 0x087E8B, 0xBFD7EA],
    ];

    /// <summary>The pictures' file names, one per palette: the pictures folder the gallery and the folder widget show.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "Lake at dusk.png", "Evening light.png", "Forest path.png", "Autumn field.png", "Harbour fog.png", "Meadow.png",
        "Sunset over the hills.png", "Old town.png", "Morning mist.png", "Coast.png", "Quiet street.png", "Glacier.png",
    ];

    /// <summary>How many different pictures there are before they repeat.</summary>
    public static int Count => Palettes.Length;

    /// <summary>Picture <paramref name="index"/> as <paramref name="width"/> × <paramref name="height"/> BGRA pixels, opaque, top row first.</summary>
    public static byte[] Pixels(int index, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var palette = Palettes[((index % Palettes.Length) + Palettes.Length) % Palettes.Length];
        var random = new Random(9_173 + (index * 7_919));

        // A diagonal from the deep colour to the middle one, at an angle of its own, and three soft
        // glows (one of each colour) placed by the seed.
        var angle = random.NextDouble() * Math.Tau;
        var (dx, dy) = (Math.Cos(angle), Math.Sin(angle));
        var glows = new (double X, double Y, double Radius, int Colour)[3];
        for (var g = 0; g < glows.Length; g++)
        {
            glows[g] = (random.NextDouble(), random.NextDouble(), 0.35 + (random.NextDouble() * 0.45), palette[(g + 1) % 3]);
        }

        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var v = (y + 0.5) / height;
            for (var x = 0; x < width; x++)
            {
                var u = (x + 0.5) / width;
                var t = Math.Clamp(0.5 + (((u - 0.5) * dx) + ((v - 0.5) * dy)), 0, 1);
                var (r, gr, b) = Mix(palette[0], palette[1], t);

                foreach (var glow in glows)
                {
                    var d = Math.Sqrt(((u - glow.X) * (u - glow.X)) + ((v - glow.Y) * (v - glow.Y))) / glow.Radius;
                    var w = Math.Max(0, 1 - (d * d));
                    w *= w * 0.75;
                    var (cr, cg, cb) = Rgb(glow.Colour);
                    r += (cr - r) * w;
                    gr += (cg - gr) * w;
                    b += (cb - b) * w;
                }

                var at = ((y * width) + x) * 4;
                pixels[at] = (byte)Math.Clamp(b, 0, 255);
                pixels[at + 1] = (byte)Math.Clamp(gr, 0, 255);
                pixels[at + 2] = (byte)Math.Clamp(r, 0, 255);
                pixels[at + 3] = 255;
            }
        }

        return pixels;
    }

    private static (double R, double G, double B) Rgb(int colour) => ((colour >> 16) & 0xFF, (colour >> 8) & 0xFF, colour & 0xFF);

    private static (double R, double G, double B) Mix(int from, int to, double t)
    {
        var (fr, fg, fb) = Rgb(from);
        var (tr, tg, tb) = Rgb(to);
        return (fr + ((tr - fr) * t), fg + ((tg - fg) * t), fb + ((tb - fb) * t));
    }
}
#endif
