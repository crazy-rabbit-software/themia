using Microsoft.UI.Xaml.Media;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-566] A brush for a colour the user chose, stored as <c>#RRGGBB</c> (a calendar's colour): the
/// one place a widget part turns the user's own colour into a brush, so the kit's parts name none
/// ([S-7066]). One brush per colour, kept for the process; null for a value that is not a colour.
/// </summary>
public static class HexBrush
{
    private static readonly Dictionary<string, Brush> Brushes = new(StringComparer.OrdinalIgnoreCase);

    public static Brush? Of(string? hex)
    {
        if (hex is null || !HexColor.TryParse(hex, out var r, out var g, out var b))
        {
            return null;
        }

        if (!Brushes.TryGetValue(hex, out var brush))
        {
            brush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
            Brushes[hex] = brush;
        }

        return brush;
    }

    /// <summary>
    /// [S-6705] A colour that comes from data rather than a token, as a colour value: the user's own
    /// (an item colour, [S-6513]) or Windows' (the selection's highlight, [S-6627]), at
    /// <paramref name="alpha"/> (0 to 1). For a part that recolours its brushes in place; the one place,
    /// beside <see cref="Of"/>, a colour is made from numbers.
    /// </summary>
    public static Windows.UI.Color Colour(RgbColor colour, double alpha = 1)
        => Windows.UI.Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), colour.R, colour.G, colour.B);

    /// <summary>
    /// [S-6747] The colour a stored <c>#RRGGBB</c> stands for (a swatch's, a fill the user chose), at
    /// <paramref name="alpha"/> (0 to 1); null for a value that is not a colour.
    /// </summary>
    public static Windows.UI.Color? Colour(string? hex, double alpha = 1)
        => hex is not null && HexColor.TryParse(hex, out var r, out var g, out var b) ? Colour(new RgbColor(r, g, b), alpha) : null;

    /// <summary>
    /// [S-6747] What a mark drawn on a data colour is painted with (a swatch's tick): black on a light
    /// colour, white on a dark one, as Windows draws on an accent (<c>ScreenPick.WantsDarkInk</c>).
    /// </summary>
    public static Brush Ink(RgbColor colour)
        => ScreenPick.WantsDarkInk(colour.R, colour.G, colour.B) ? DarkInk : LightInk;

    private static readonly Brush DarkInk = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));

    private static readonly Brush LightInk = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));

    /// <summary>
    /// [S-6747] <paramref name="role"/>'s colour as a wash (<see cref="FamilyShape.Wash"/>, 16 %): the
    /// faint fill of a rubber band or a placing outline under its stroke in the same role. A new brush,
    /// read from the role as the theme and the accent have it now.
    /// </summary>
    public static Brush Wash(ColourRole role)
    {
        var (colour, _) = Colour(ColourRoles.Key(role));
        return new SolidColorBrush(Colour(new RgbColor(colour.R, colour.G, colour.B), FamilyShape.Wash));
    }

    /// <summary>
    /// [S-6738] A text role as the opaque colour it shows on the card (the role over the card's fill
    /// over its base, as the theme and the user's design have them now): for a control that draws a
    /// text colour without its alpha, the note editor's RichEdit, where the secondary and tertiary
    /// roles would otherwise all come out as the primary. Asked again when the theme changes.
    /// </summary>
    public static Windows.UI.Color OnCard(ColourRole role)
    {
        var (fill, fillAlpha) = Colour(ColourRoles.CardFill);
        var (cardBase, _) = Colour(ColourRoles.CardBase);
        var card = HexColor.Over(fillAlpha, fill.R, fill.G, fill.B, HexColor.From(cardBase.R, cardBase.G, cardBase.B));
        var (text, textAlpha) = Colour(ColourRoles.Key(role));
        HexColor.TryParse(HexColor.Over(textAlpha, text.R, text.G, text.B, card), out var r, out var g, out var b);
        return Windows.UI.Color.FromArgb(255, r, g, b);
    }

    /// <summary>A theme brush's colour and how much of it shows: its colour's alpha times its opacity (the design engine keeps either).</summary>
    private static (Windows.UI.Color Colour, byte Alpha) Colour(string key)
        => ThemeBrush.Of(key, default) is SolidColorBrush brush
            ? (brush.Color, (byte)Math.Round(brush.Color.A * Math.Clamp(brush.Opacity, 0, 1)))
            : (default, 0);
}
