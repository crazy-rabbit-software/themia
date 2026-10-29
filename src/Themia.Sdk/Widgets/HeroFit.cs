using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-659] [S-6702] The measure a readout is chosen by: how wide a text is in ems of its own size, by
/// what its glyphs are (Segoe UI Variable's advances by glyph class), so a colon or a degree sign is
/// not counted as a digit and a meridiem is measured at the size it is drawn. The family's steps are
/// chosen on it (<c>ReadoutRules</c>, <c>FlipRules</c>); the fill rules that used to scale a readout
/// to its tile are gone (group G5).
/// </summary>
public static class HeroFit
{
    /// <summary>
    /// How wide <paramref name="text"/> is, in ems of its own size: Segoe UI Variable's advances by
    /// glyph class, the widest over its weights and optical sizes, so a fit on them never clips.
    /// A digit is 0.58 (tabular, as every hero sets them), a colon or a stop 0.25, a capital 0.72
    /// ("M" and "W" 0.95), a small letter 0.56; a script the table does not know counts a full em
    /// a character, and a combining mark nothing.
    /// </summary>
    public static double Ems(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var ems = 0.0;

        foreach (var c in text)
        {
            ems += Advance(c);
        }

        return ems;
    }

    private static double Advance(char c)
    {
        if (c is >= '0' and <= '9')
        {
            return 0.58;
        }

        switch (c)
        {
            case ':' or '.' or ',' or ';' or '\'' or '!' or '|':
                return 0.25;
            case ' ' or ' ' or ' ' or ' ':
                return 0.28;
            case '-':
                return 0.41;
            case '−' or '+':
                return 0.7;
            case '°':
                return 0.39;
            case '%':
                return 0.85;
            case 'M' or 'W' or 'm' or 'w':
                return 0.95;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(c);

        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        if (c <= 'ɏ')
        {
            return category == UnicodeCategory.UppercaseLetter ? 0.72 : 0.56;
        }

        return 1.0;
    }
}
