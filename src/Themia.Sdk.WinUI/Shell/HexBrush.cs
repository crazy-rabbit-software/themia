using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;

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
}
