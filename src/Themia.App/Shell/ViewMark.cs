using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-903] / [S-3812] How a view is marked wherever it is listed outside the view bar's chips — the
/// edit bar's View and Display menus, the view bar's overflow menu: its icon (the shared view glyph
/// where it has none), in the view's own colour when it has one ([S-3810]), else the theme's.
/// </summary>
internal static class ViewMark
{
    /// <summary>The icon from a view's glyph and colour.</summary>
    public static FontIcon Icon(string? glyph, string? color, double size = Fluent.IconSize)
    {
        var icon = new FontIcon { Glyph = string.IsNullOrEmpty(glyph) ? Glyphs.View : glyph, FontSize = size };
        if (DesignApplier.UserBrush(color) is { } brush)
        {
            icon.Foreground = brush;
        }

        return icon;
    }
}
