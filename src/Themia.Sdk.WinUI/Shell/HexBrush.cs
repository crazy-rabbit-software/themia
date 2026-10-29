using Microsoft.UI.Xaml.Media;
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
