namespace Themia.Core.Widgets;

/// <summary>A width and a height, in whatever unit the caller works in.</summary>
public readonly record struct BoxSize(double Width, double Height);

/// <summary>
/// [S-774] [S-775] A picture shown as a sticker: turned by any angle and still whole inside its
/// frame, and outlined along its own transparent shape. Pure, so each rule is tested; the drawing
/// is <c>RotatedBox</c>'s and the Gallery's.
/// </summary>
public static class StickerRules
{
    /// <summary>[S-775] The thinnest and thickest outline, in DIPs.</summary>
    public const int MinOutline = 1;

    public const int MaxOutline = 24;

    /// <summary>[S-775] The default outline: about what a die-cut sticker's white edge looks like on a desktop-sized picture.</summary>
    public const int DefaultOutline = 6;

    /// <summary>[S-775] An outline's default colour: the white of a printed sticker's edge.</summary>
    public const string DefaultOutlineColor = "#FFFFFF";

    /// <summary>[S-775] Pixels at least this opaque (of 255) are the picture's shape; fainter ones (a soft shadow) are not outlined.</summary>
    public const byte ShapeAlpha = 128;

    /// <summary>[S-777] The longest side the outline is worked out at, in physical pixels.</summary>
    public const int MaxOutlineSide = 2048;

    /// <summary>[S-774] Any whole-degree angle as one turn in (−180, 180]: 270 is −90, −180 is 180.</summary>
    public static int NormalizeDegrees(int degrees)
    {
        var turn = ((degrees % 360) + 360) % 360;
        return turn > 180 ? turn - 360 : turn;
    }

    /// <summary>
    /// [S-774] [S-468] The largest box of <paramref name="aspect"/> (width / height) that, turned by
    /// <paramref name="degrees"/> about its centre, still fits a <paramref name="frameWidth"/> x
    /// <paramref name="frameHeight"/> frame. A null aspect is the frame's own shape turned with it
    /// (the nearest quarter turn decides which way round), so a quarter turn lays a 320x240 frame's
    /// child out at 240x320, as the video stream's rotation always did.
    /// </summary>
    public static BoxSize FitRotated(double frameWidth, double frameHeight, double degrees, double? aspect)
    {
        if (!(frameWidth > 0) || !(frameHeight > 0) || !double.IsFinite(frameWidth) || !double.IsFinite(frameHeight))
        {
            return new BoxSize(0, 0);
        }

        var radians = (double.IsFinite(degrees) ? degrees : 0) * Math.PI / 180;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));

        // Snap the float noise of a quarter turn, so 90° is exactly a swap and not a hair smaller.
        cos = cos < 1e-9 ? 0 : cos;
        sin = sin < 1e-9 ? 0 : sin;

        var ratio = aspect is { } a && double.IsFinite(a) && a > 0
            ? a
            : sin > cos ? frameHeight / frameWidth : frameWidth / frameHeight;

        // A box of height h and width ratio·h turned by θ spans (ratio·cos + sin)·h across and
        // (ratio·sin + cos)·h down; the frame bounds both.
        var height = Math.Min(frameWidth / ((ratio * cos) + sin), frameHeight / ((ratio * sin) + cos));
        return new BoxSize(ratio * height, height);
    }

    /// <summary>
    /// [S-775] How many of the picture's own pixels the outline adds on each side, so it shows
    /// <paramref name="strokeDips"/> wide once the padded picture is fitted (and turned) into the
    /// frame. The pad changes the shape being fitted, which changes the scale, so it is settled by a
    /// few rounds of fitting; the answer is whole pixels, at least one.
    /// </summary>
    public static int OutlinePad(double frameWidth, double frameHeight, int pictureWidth, int pictureHeight, double degrees, double strokeDips)
    {
        if (pictureWidth <= 0 || pictureHeight <= 0 || !(strokeDips > 0))
        {
            return 0;
        }

        var pad = 0.0;

        for (var round = 0; round < 6; round++)
        {
            var paddedWidth = pictureWidth + (2 * pad);
            var box = FitRotated(frameWidth, frameHeight, degrees, paddedWidth / (pictureHeight + (2 * pad)));

            if (!(box.Width > 0))
            {
                return 0;
            }

            var dipsPerPixel = box.Width / paddedWidth;
            pad = strokeDips / dipsPerPixel;
        }

        return Math.Max(1, (int)Math.Round(pad));
    }

    /// <summary>
    /// [S-775] The outline of a picture's shape: for a <paramref name="width"/> x
    /// <paramref name="height"/> alpha channel (row-major, one byte a pixel), the coverage (0..255) of
    /// a band <paramref name="pad"/> pixels wide around every pixel at least <see cref="ShapeAlpha"/>
    /// opaque, on a canvas grown by <paramref name="pad"/> on every side. Exact Euclidean distance
    /// (two separable passes, linear in the pixels), so the band is round at corners and as wide on a
    /// diagonal as across, with half a pixel of edge softening rather than a stair. The picture is
    /// drawn over it, so only the band beyond its edge shows.
    /// </summary>
    public static byte[] Outline(ReadOnlySpan<byte> alpha, int width, int height, int pad)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(pad);

        if (alpha.Length != width * height)
        {
            throw new ArgumentException($"expected {width * height} alpha values, got {alpha.Length}", nameof(alpha));
        }

        var outW = width + (2 * pad);
        var outH = height + (2 * pad);
        var coverage = new byte[outW * outH];

        if (outW == 0 || outH == 0)
        {
            return coverage;
        }

        // Squared distance to the nearest shape pixel: 0 on the shape, "far" elsewhere.
        var far = (double)(outW + outH) * (outW + outH);
        var distance = new double[outW * outH];
        Array.Fill(distance, far);
        var any = false;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (alpha[(y * width) + x] >= ShapeAlpha)
                {
                    distance[((y + pad) * outW) + x + pad] = 0;
                    any = true;
                }
            }
        }

        if (!any)
        {
            return coverage; // a picture with no opaque pixel has no shape to follow.
        }

        var line = new double[Math.Max(outW, outH)];
        var result = new double[line.Length];
        var hulls = new int[line.Length];
        var bounds = new double[line.Length + 1];

        for (var x = 0; x < outW; x++)
        {
            for (var y = 0; y < outH; y++)
            {
                line[y] = distance[(y * outW) + x];
            }

            Transform(line, outH, result, hulls, bounds);

            for (var y = 0; y < outH; y++)
            {
                distance[(y * outW) + x] = result[y];
            }
        }

        for (var y = 0; y < outH; y++)
        {
            Array.Copy(distance, y * outW, line, 0, outW);
            Transform(line, outW, result, hulls, bounds);
            Array.Copy(result, 0, distance, y * outW, outW);
        }

        for (var i = 0; i < coverage.Length; i++)
        {
            var reach = pad + 0.5 - Math.Sqrt(distance[i]);
            coverage[i] = (byte)Math.Round(Math.Clamp(reach, 0, 1) * 255);
        }

        return coverage;
    }

    /// <summary>
    /// One pass of the exact squared-distance transform (Felzenszwalb and Huttenlocher): the lower
    /// envelope of the parabolas rooted at each sample.
    /// </summary>
    private static void Transform(double[] f, int n, double[] d, int[] v, double[] z)
    {
        var k = 0;
        v[0] = 0;
        z[0] = double.NegativeInfinity;
        z[1] = double.PositiveInfinity;

        for (var q = 1; q < n; q++)
        {
            var s = Intersection(f, q, v[k]);

            while (s <= z[k])
            {
                k--;
                s = Intersection(f, q, v[k]);
            }

            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = double.PositiveInfinity;
        }

        k = 0;

        for (var q = 0; q < n; q++)
        {
            while (z[k + 1] < q)
            {
                k++;
            }

            var dq = q - v[k];
            d[q] = (dq * dq) + f[v[k]];
        }
    }

    private static double Intersection(double[] f, int q, int p)
        => ((f[q] + ((double)q * q)) - (f[p] + ((double)p * p))) / (2.0 * (q - p));
}
