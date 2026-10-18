namespace Themia.Core.Motion;

/// <summary>
/// [S-6327] Colours mixed in OKLab (Björn Ottosson's perceptual space), so a tile moving from the
/// accent to the caution colour passes through colours of an even lightness instead of the muddy,
/// darker middle a straight sRGB mix gives. Colours are <c>0xAARRGGBB</c>; alpha mixes linearly.
/// </summary>
public static class OkLab
{
    /// <summary>The colour <paramref name="t"/> (clamped to 0..1) of the way from one colour to the other; the ends exactly.</summary>
    public static uint Mix(uint from, uint to, double t)
    {
        var p = double.IsFinite(t) ? Math.Clamp(t, 0, 1) : 1;

        if (p <= 0)
        {
            return from;
        }

        if (p >= 1 || from == to)
        {
            return to;
        }

        var (l1, a1, b1) = ToLab(from);
        var (l2, a2, b2) = ToLab(to);
        var rgb = FromLab(l1 + ((l2 - l1) * p), a1 + ((a2 - a1) * p), b1 + ((b2 - b1) * p));
        var alpha = Channel(((from >> 24) & 0xFF) + ((((double)((to >> 24) & 0xFF)) - ((from >> 24) & 0xFF)) * p));
        return (alpha << 24) | rgb;
    }

    /// <summary>
    /// [S-6327] The colours a key-framed glide passes through: <paramref name="segments"/> colours at
    /// 1/n, 2/n .. 1 of the way, the last the target, so a straight per-segment animation between them
    /// follows the OKLab path.
    /// </summary>
    public static uint[] Ramp(uint from, uint to, int segments)
    {
        var count = Math.Max(1, segments);
        var ramp = new uint[count];

        for (var i = 1; i <= count; i++)
        {
            ramp[i - 1] = Mix(from, to, (double)i / count);
        }

        return ramp;
    }

    private static (double L, double A, double B) ToLab(uint argb)
    {
        var r = ToLinear((argb >> 16) & 0xFF);
        var g = ToLinear((argb >> 8) & 0xFF);
        var b = ToLinear(argb & 0xFF);

        var l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));

        return (
            (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s),
            (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s),
            (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s));
    }

    private static uint FromLab(double lightness, double a, double b)
    {
        var l = lightness + (0.3963377774 * a) + (0.2158037573 * b);
        var m = lightness - (0.1055613458 * a) - (0.0638541728 * b);
        var s = lightness - (0.0894841775 * a) - (1.2914855480 * b);
        l *= l * l;
        m *= m * m;
        s *= s * s;

        var r = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        var g = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        var bl = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);

        return (FromLinear(r) << 16) | (FromLinear(g) << 8) | FromLinear(bl);
    }

    private static double ToLinear(uint channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static uint FromLinear(double linear)
    {
        var c = Math.Clamp(linear, 0, 1);
        var srgb = c <= 0.0031308 ? c * 12.92 : (1.055 * Math.Pow(c, 1 / 2.4)) - 0.055;
        return Channel(srgb * 255);
    }

    private static uint Channel(double value) => (uint)Math.Clamp(Math.Round(value), 0, 255);
}
