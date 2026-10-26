using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
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

    /// <summary>
    /// [S-5406] A text-only view's colour as a menu row's icon: a dot of <paramref name="diameter"/>
    /// in the middle of the row's icon box. A menu draws its icon scaled to fill that box, so the dot
    /// sits in a frame the box's size (a point at its far corner), never blown up to the whole box.
    /// </summary>
    public static PathIcon Dot(Brush colour, double diameter)
    {
        var box = Fluent.IconSize;
        var r = Math.Min(diameter, box) / 2;
        return new PathIcon
        {
            Foreground = colour,
            Data = new GeometryGroup
            {
                Children =
                {
                    new EllipseGeometry { Center = new Point(box / 2, box / 2), RadiusX = r, RadiusY = r },
                    new LineGeometry { StartPoint = new Point(0, 0), EndPoint = new Point(box, box) },
                },
            },
        };
    }
}
