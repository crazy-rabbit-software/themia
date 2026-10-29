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
        var fill = Colour(ColourRoles.CardFill);
        var card = HexColor.Over(fill.A, fill.R, fill.G, fill.B, Hex(Colour(ColourRoles.CardBase)));
        var text = Colour(ColourRoles.Key(role));
        HexColor.TryParse(HexColor.Over(text.A, text.R, text.G, text.B, card), out var r, out var g, out var b);
        return Windows.UI.Color.FromArgb(255, r, g, b);
    }

    private static Windows.UI.Color Colour(string key) => (ThemeBrush.Of(key, default) as SolidColorBrush)?.Color ?? default;

    private static string Hex(Windows.UI.Color colour) => HexColor.From(colour.R, colour.G, colour.B);
}
