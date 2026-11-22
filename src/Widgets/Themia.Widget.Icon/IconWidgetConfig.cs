using Themia.Core.Model;

namespace Themia.Widget.Icon;

/// <summary>[S-15011] What one Icon widget remembers: the glyph picked (a Segoe Fluent glyph from the icon picker), or none.</summary>
public sealed class IconWidgetConfig : WidgetConfig
{
    /// <summary>[S-15011] The glyph picked, one character of the icon font; empty: the default, the star.</summary>
    public string Glyph { get; set; } = string.Empty;

    /// <summary>[S-15011] A stored glyph that is not one character of the icon font (a hand-edited file) is cleared, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (Glyph.Length == 0 || IconChoice.IsGlyph(Glyph))
        {
            return [];
        }

        Glyph = string.Empty;
        return ["IconWidgetConfig had a glyph that is not one icon; it shows the default"];
    }

    public override WidgetConfig Clone() => new IconWidgetConfig { Glyph = Glyph };
}

/// <summary>[S-15011] Which glyph an Icon widget draws, pure.</summary>
public static class IconChoice
{
    /// <summary>[S-15011] Whether <paramref name="glyph"/> is one character of the icon font: one code point of a private use area.</summary>
    public static bool IsGlyph(string? glyph)
    {
        if (string.IsNullOrEmpty(glyph) || glyph.Length > 2)
        {
            return false;
        }

        int code;
        if (glyph.Length == 2)
        {
            if (!char.IsSurrogatePair(glyph, 0))
            {
                return false;
            }

            code = char.ConvertToUtf32(glyph, 0);
        }
        else
        {
            code = glyph[0];
        }

        return code is (>= 0xE000 and <= 0xF8FF) or (>= 0xF0000 and <= 0x10FFFD);
    }

    /// <summary>[S-15011] What the widget draws for its pick: the glyph picked, or <paramref name="fallback"/> (the star) with none.</summary>
    public static string Drawn(string? glyph, string fallback) => IsGlyph(glyph) ? glyph! : fallback;
}
