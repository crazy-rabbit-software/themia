using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>How a colour is written down. A closed set, because it is persisted.</summary>
public enum ColorNotation
{
    /// <summary><c>#RRGGBB</c>.</summary>
    Hex = 0,

    /// <summary><c>rgb(0, 120, 212)</c>.</summary>
    Rgb = 1,

    /// <summary><c>hsl(206, 100%, 42%)</c>.</summary>
    Hsl = 2,
}

/// <summary>An opaque colour, as three 8-bit channels. No alpha: a palette swatch is a colour, not a wash.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>Hue in degrees (0–360), saturation and lightness as fractions (0–1).</summary>
public readonly record struct HslColor(double H, double S, double L);

/// <summary>
/// Reading and writing the three notations a palette copies in.
/// <para>
/// Hex parsing is <b>not</b> reimplemented here: <see cref="HexColor"/> already owns it, including
/// the <c>#abc</c> shorthand and the rule that empty is a real answer rather than a failure. This
/// adds the two notations it does not cover and the conversion between them, and everything is a
/// pure function of its input so the whole thing is testable without a window.
/// </para>
/// <para>
/// Every entry point treats its input as untrusted. A palette comes off disk, and a user typing a
/// colour into settings is the same trust boundary as a file someone hand-edited: malformed input
/// returns false, never a half-built colour.
/// </para>
/// </summary>
public static class ColorFormats
{
    /// <summary>
    /// Parse a colour in any of the supported notations. Returns false for empty and for anything
    /// malformed, so the caller can tell "no colour" from "a colour" without a sentinel.
    /// </summary>
    public static bool TryParse(string? value, out RgbColor color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        // Hex first, and through the parser that already exists rather than a second copy of it.
        if (HexColor.TryParse(text, out var r, out var g, out var b))
        {
            color = new RgbColor(r, g, b);
            return true;
        }

        if (TryFunction(text, "rgba", out var rgbaArgs) && rgbaArgs.Length == 4)
        {
            // The alpha is read so that a pasted rgba() is accepted, and then dropped: a swatch is
            // opaque, and silently keeping a 0.2 alpha would make the copied value lie.
            return TryRgbChannels(rgbaArgs, out color);
        }

        if (TryFunction(text, "rgb", out var rgbArgs) && rgbArgs.Length == 3)
        {
            return TryRgbChannels(rgbArgs, out color);
        }

        if (TryFunction(text, "hsla", out var hslaArgs) && hslaArgs.Length == 4)
        {
            return TryHslChannels(hslaArgs, out color);
        }

        if (TryFunction(text, "hsl", out var hslArgs) && hslArgs.Length == 3)
        {
            return TryHslChannels(hslArgs, out color);
        }

        return false;
    }

    /// <summary>Write a colour in the notation the user picked.</summary>
    public static string Format(RgbColor color, ColorNotation notation) => notation switch
    {
        ColorNotation.Rgb => string.Create(
            CultureInfo.InvariantCulture,
            $"rgb({color.R}, {color.G}, {color.B})"),
        ColorNotation.Hsl => FormatHsl(ToHsl(color)),
        _ => HexColor.From(color.R, color.G, color.B),
    };

    /// <summary>The label a notation carries in settings.</summary>
    public static string Describe(ColorNotation notation) => notation switch
    {
        ColorNotation.Rgb => "rgb(0, 120, 212)",
        ColorNotation.Hsl => "hsl(206, 100%, 42%)",
        _ => "#0078D4",
    };

    /// <summary>
    /// The standard sRGB conversion. Hue is undefined for a grey and reported as 0, which is the
    /// conventional answer and keeps the round trip through <see cref="FromHsl"/> exact.
    /// </summary>
    public static HslColor ToHsl(RgbColor color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        var span = max - min;

        if (span <= 0)
        {
            return new HslColor(0, 0, lightness);
        }

        var saturation = lightness > 0.5
            ? span / (2 - max - min)
            : span / (max + min);

        double hue;

        if (max == r)
        {
            hue = ((g - b) / span) + (g < b ? 6 : 0);
        }
        else if (max == g)
        {
            hue = ((b - r) / span) + 2;
        }
        else
        {
            hue = ((r - g) / span) + 4;
        }

        return new HslColor(hue * 60, saturation, lightness);
    }

    /// <summary>The inverse of <see cref="ToHsl"/>. Out-of-range input is wrapped or clamped, never rejected.</summary>
    public static RgbColor FromHsl(HslColor color)
    {
        var hue = Wrap(color.H) / 360.0;
        var saturation = Math.Clamp(Finite(color.S), 0, 1);
        var lightness = Math.Clamp(Finite(color.L), 0, 1);

        if (saturation <= 0)
        {
            var grey = Channel(lightness);
            return new RgbColor(grey, grey, grey);
        }

        var q = lightness < 0.5
            ? lightness * (1 + saturation)
            : lightness + saturation - (lightness * saturation);

        var p = (2 * lightness) - q;

        return new RgbColor(
            Channel(HueToChannel(p, q, hue + (1.0 / 3.0))),
            Channel(HueToChannel(p, q, hue)),
            Channel(HueToChannel(p, q, hue - (1.0 / 3.0))));
    }

    /// <summary>
    /// WCAG relative luminance, used to decide whether a label drawn on a swatch should be dark or
    /// light. The swatch's colour is the user's; the legibility of the text on it is ours.
    /// </summary>
    public static double RelativeLuminance(RgbColor color)
        => (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    /// <summary>Whether text on this colour should be dark. The 0.5 threshold is WCAG's own crossover.</summary>
    public static bool WantsDarkText(RgbColor color) => RelativeLuminance(color) > 0.5;

    // ---- internals -----------------------------------------------------------------------------

    private static string FormatHsl(HslColor hsl)
    {
        var hue = (int)Math.Round(Wrap(hsl.H), MidpointRounding.AwayFromZero) % 360;
        var saturation = (int)Math.Round(Math.Clamp(hsl.S, 0, 1) * 100, MidpointRounding.AwayFromZero);
        var lightness = (int)Math.Round(Math.Clamp(hsl.L, 0, 1) * 100, MidpointRounding.AwayFromZero);

        return string.Create(CultureInfo.InvariantCulture, $"hsl({hue}, {saturation}%, {lightness}%)");
    }

    /// <summary>Split <c>name(a, b, c)</c> into its arguments, or false if this is not that shape.</summary>
    private static bool TryFunction(string text, string name, out string[] arguments)
    {
        arguments = [];

        if (!text.StartsWith(name, StringComparison.OrdinalIgnoreCase) || !text.EndsWith(')'))
        {
            return false;
        }

        var open = text.IndexOf('(');

        // "rgb" must be the whole head: "rgba(...)" must not be read as "rgb" with a stray "a".
        if (open != name.Length)
        {
            return false;
        }

        var inner = text[(open + 1)..^1];

        // CSS accepts commas, spaces, or a slash before the alpha. All three land here as separators.
        arguments = inner.Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return arguments.Length > 0;
    }

    private static bool TryRgbChannels(string[] arguments, out RgbColor color)
    {
        color = default;

        if (!TryChannel(arguments[0], out var r)
            || !TryChannel(arguments[1], out var g)
            || !TryChannel(arguments[2], out var b))
        {
            return false;
        }

        color = new RgbColor(r, g, b);
        return true;
    }

    private static bool TryHslChannels(string[] arguments, out RgbColor color)
    {
        color = default;

        if (!TryNumber(arguments[0], out var hue)
            || !TryPercent(arguments[1], out var saturation)
            || !TryPercent(arguments[2], out var lightness))
        {
            return false;
        }

        color = FromHsl(new HslColor(hue, saturation, lightness));
        return true;
    }

    /// <summary>An 0–255 channel, or the same as a percentage. Out of range is a malformed colour, not a clamp.</summary>
    private static bool TryChannel(string text, out byte value)
    {
        value = 0;

        if (text.EndsWith('%'))
        {
            if (!TryPercent(text, out var fraction) || fraction < 0 || fraction > 1)
            {
                return false;
            }

            value = Channel(fraction);
            return true;
        }

        if (!TryNumber(text, out var number) || number < 0 || number > 255)
        {
            return false;
        }

        value = (byte)Math.Round(number, MidpointRounding.AwayFromZero);
        return true;
    }

    private static bool TryPercent(string text, out double fraction)
    {
        fraction = 0;

        var body = text.EndsWith('%') ? text[..^1] : text;

        if (!TryNumber(body, out var number))
        {
            return false;
        }

        // A bare number where a percentage belongs is read as a fraction, which is what the modern
        // CSS syntax means by it.
        fraction = text.EndsWith('%') ? number / 100.0 : number;
        return fraction is >= 0 and <= 1;
    }

    private static bool TryNumber(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);

    private static double HueToChannel(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        if (t < 1.0 / 6.0)
        {
            return p + ((q - p) * 6 * t);
        }

        if (t < 1.0 / 2.0)
        {
            return q;
        }

        if (t < 2.0 / 3.0)
        {
            return p + ((q - p) * ((2.0 / 3.0) - t) * 6);
        }

        return p;
    }

    private static byte Channel(double fraction)
        => (byte)Math.Clamp(Math.Round(fraction * 255, MidpointRounding.AwayFromZero), 0, 255);

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double Wrap(double degrees)
    {
        if (!double.IsFinite(degrees))
        {
            return 0;
        }

        var wrapped = degrees % 360;
        return wrapped < 0 ? wrapped + 360 : wrapped;
    }

    private static double Finite(double value) => double.IsFinite(value) ? value : 0;
}
