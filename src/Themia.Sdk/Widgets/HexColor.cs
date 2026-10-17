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

    private static bool IsHex(char c)
        => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static int Digit(char c)
        => c <= '9' ? c - '0' : (char.ToLowerInvariant(c) - 'a' + 10);
}
