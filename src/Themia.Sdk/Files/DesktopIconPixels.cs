namespace Themia.Sdk;

/// <summary>[S-5563] Which of the system image lists draws a desktop item's icon.</summary>
public enum DesktopIconList
{
    /// <summary>The extra-large list: 48 × 48 pixels.</summary>
    ExtraLarge,

    /// <summary>The jumbo list: 256 × 256 pixels, drawn smaller.</summary>
    Jumbo,
}

/// <summary>
/// [S-5563] A desktop item's icon at each display's scale, as pure rules: the pixels an icon of some
/// DIPs needs on a display (so a 48 icon is 48 pixels at 100 %, 72 at 150 %, 96 at 200 %), and the
/// image list that gives them crisp: the extra-large list up to its 48 pixels, the jumbo list (drawn
/// down) above, except for an icon that has no large image, which the jumbo list returns as its
/// 48-pixel image in a corner of an empty 256 square: that one is the extra-large image drawn up.
/// </summary>
public static class DesktopIconPixels
{
    /// <summary>The extra-large list's side.</summary>
    public const int ExtraLarge = 48;

    /// <summary>The jumbo list's side.</summary>
    public const int Jumbo = 256;

    /// <summary>The smallest icon asked for.</summary>
    public const int Smallest = 16;

    /// <summary>[S-5563] The physical pixels of an icon <paramref name="dip"/> DIPs wide on a display at <paramref name="scale"/> (1 = 100 %).</summary>
    public static int For(double dip, double scale)
    {
        if (!double.IsFinite(dip) || dip <= 0)
        {
            return Smallest;
        }

        var s = double.IsFinite(scale) && scale > 0 ? scale : 1.0;
        return Math.Clamp((int)Math.Round(dip * s, MidpointRounding.AwayFromZero), Smallest, Jumbo);
    }

    /// <summary>[S-5563] The list for <paramref name="pixels"/>: extra-large up to 48, jumbo above.</summary>
    public static DesktopIconList ListFor(int pixels) => pixels <= ExtraLarge ? DesktopIconList.ExtraLarge : DesktopIconList.Jumbo;

    /// <summary>
    /// [S-5563] Whether a jumbo image (premultiplied BGRA, <paramref name="width"/> × <paramref name="height"/>)
    /// is only a small image in an empty square: every pixel it draws lies within 48 × 48 (the
    /// item has no image larger than the extra-large list's). An empty image counts as small too.
    /// </summary>
    public static bool HoldsOnlySmallImage(ReadOnlySpan<byte> bgra, int width, int height)
    {
        if (width <= ExtraLarge && height <= ExtraLarge)
        {
            return false;
        }

        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
        {
            return false;
        }

        int left = width, top = height, right = -1, bottom = -1;
        for (var y = 0; y < height; y++)
        {
            var row = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                if (bgra[row + (x * 4) + 3] == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0 || (right - left + 1 <= ExtraLarge && bottom - top + 1 <= ExtraLarge);
    }
}
