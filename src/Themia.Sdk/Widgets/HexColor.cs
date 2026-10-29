using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>
/// The colour overrides in <c>DesignState</c>, as stored.
/// <para>
/// Held as hex strings rather than a colour struct so that <b>empty means "use the theme's own"</b>.
/// That distinction is the whole point: a user overriding the card fill must not thereby pin the
/// border and the accent to whatever they happened to be, or the widgets stop following light/dark.
/// </para>
/// </summary>
public static class HexColor
{
    /// <summary>Whether this string names a colour at all. Empty is valid and means "not overridden".</summary>
    public static bool IsOverridden(string? value) => TryParse(value, out _, out _, out _);

    /// <summary>
    /// Parse <c>#RRGGBB</c> or <c>#RGB</c>, with or without the hash. Returns false for empty, which
    /// is the "use the theme" case, and for anything malformed — a stored colour is user input by
    /// the time it comes back off disk.
    /// </summary>
    public static bool TryParse(string? value, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim().TrimStart('#');

        if (text.Length == 3)
        {
            // #abc is #aabbcc, the same shorthand CSS uses.
            if (!IsHex(text[0]) || !IsHex(text[1]) || !IsHex(text[2]))
            {
                return false;
            }

            r = (byte)(Digit(text[0]) * 17);
            g = (byte)(Digit(text[1]) * 17);
            b = (byte)(Digit(text[2]) * 17);
            return true;
        }

        if (text.Length != 6)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!IsHex(c))
            {
                return false;
            }
        }

        r = byte.Parse(text.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        g = byte.Parse(text.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        b = byte.Parse(text.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Normalise to <c>#RRGGBB</c>, or empty when the value does not name a colour.</summary>
    public static string Normalise(string? value)
        => TryParse(value, out var r, out var g, out var b)
            ? $"#{r:X2}{g:X2}{b:X2}"
            : string.Empty;

    public static string From(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    /// <summary>
    /// [S-6738] A translucent colour laid over an opaque one (<paramref name="under"/>, <c>#RRGGBB</c>),
    /// as the opaque <c>#RRGGBB</c> it shows as: what a control that draws a colour without its alpha
    /// (the note editor's RichEdit) is given in its place. An <paramref name="under"/> that is not a
    /// colour leaves the colour as it is, opaque.
    /// </summary>
    public static string Over(byte alpha, byte r, byte g, byte b, string? under)
    {
        if (!TryParse(under, out var ur, out var ug, out var ub))
        {
            return From(r, g, b);
        }

        var a = alpha / 255.0;
        return From(Mix(r, ur, a), Mix(g, ug, a), Mix(b, ub, a));
    }

    private static byte Mix(byte over, byte under, double alpha) => (byte)Math.Round((over * alpha) + (under * (1 - alpha)));

    private static bool IsHex(char c)
        => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static int Digit(char c)
        => c <= '9' ? c - '0' : (char.ToLowerInvariant(c) - 'a' + 10);
}
