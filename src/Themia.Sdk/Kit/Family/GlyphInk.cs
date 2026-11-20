namespace Themia.Sdk.Kit;

/// <summary>
/// [S-14240] Where a glyph inks inside its box, as fractions of it from its top left: <see cref="Left"/> and
/// <see cref="Right"/> across, <see cref="Top"/> and <see cref="Bottom"/> down (a wide glyph may reach past 1).
/// </summary>
public readonly record struct GlyphBox(double Left, double Top, double Right, double Bottom)
{
    /// <summary>A glyph that inks its whole box (a chip with pins, an arrow), or one the table does not know.</summary>
    public static GlyphBox Whole => new(0, 0, 1, 1);
}

/// <summary>
/// [S-14240] Where each Segoe Fluent Icons glyph inks in its box (a FontIcon's box is its em: the font's ascent is its
/// em and its descent 0), read off the font's outlines by scripts/glyph-ink.py. Most glyphs ink only part of it (the
/// memory chip leaves an eighth over and under, the drive is a flat bar through the middle), so a readout led by one
/// stands the glyph's ink on the card's insets, not its box (owner, 2026-11-19, WR-1: "there should not be a space at
/// the top"; WR-3, WR-4).
/// </summary>
public static partial class GlyphInk
{
    /// <summary>The ink of <paramref name="glyph"/> (one private-use character) in its box; the whole box for any other text.</summary>
    public static GlyphBox Of(string? glyph)
    {
        if (glyph is not { Length: 1 })
        {
            return GlyphBox.Whole;
        }

        int code = glyph[0];
        int low = 0, high = (Data.Length / 5) - 1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var at = Data[mid * 5];
            if (at == code)
            {
                return new GlyphBox(Data[(mid * 5) + 1] / Em, Data[(mid * 5) + 2] / Em, Data[(mid * 5) + 3] / Em, Data[(mid * 5) + 4] / Em);
            }

            if (at < code)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return GlyphBox.Whole;
    }
}
