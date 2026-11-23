using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Header;

/// <summary>
/// [S-9601] The Header widget's sizes and text rules: a thin strip, as a card's header was: the card's inset
/// over it, one line of 20 (the family's header line), one Caption line more with a caption, and the
/// header's gap of 4 under it to the content of the mate it heads. Wide as the group
/// it heads: any width from <see cref="Narrowest"/> to <see cref="Widest"/>, the height its lines state
/// (the relaxed range, [S-12411], lets it be stretched).
/// </summary>
public static class HeaderLayout
{
    /// <summary>The least width: the glyph and a short word.</summary>
    public const double Narrowest = 96;

    /// <summary>A new one's width: a group's.</summary>
    public const double NaturalWidth = 240;

    /// <summary>The widest: across a row of large widgets.</summary>
    public const double Widest = 960;

    /// <summary>The longest title or caption kept: a header is a line, never a paragraph.</summary>
    public const int MostText = 80;

    /// <summary>The strip's height: the inset, the line, a caption's line if any, the header's gap.</summary>
    public static double Height(bool caption)
        => FamilySpace.Inset + FamilySpace.HeaderLine + (caption ? FamilyType.Caption.LineHeight : 0) + FamilySpace.HeaderGap;

    /// <summary>[S-9601] The sizing: any width in range, the height its lines take; [S-17100] a taller card at most its line's shape at its width.</summary>
    public static WidgetSizing Sizing(HeaderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var height = Height(config.HasCaption);
        return WidgetSizing.Range(new(Narrowest, height), new(NaturalWidth, height), new(Widest, height)).ScalesAsOne();
    }

    /// <summary>Text as stored: one line (breaks become spaces), trimmed, at most <see cref="MostText"/> characters.</summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var line = string.Join(' ', text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();
        return line.Length <= MostText ? line : line[..MostText].TrimEnd();
    }

    /// <summary>[S-9602] Whether <paramref name="glyph"/> is none or one Segoe Fluent glyph (one private-use character).</summary>
    public static bool IsGlyph(string? glyph)
        => string.IsNullOrEmpty(glyph) || (glyph.Length == 1 && glyph[0] is >= '' and <= '');
}
