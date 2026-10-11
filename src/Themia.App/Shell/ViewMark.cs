using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-903] / [S-3812] How a view is marked wherever it is listed outside the view bar — the Views
/// page, the Shows and New displays drop-downs, "Views on the bar", the edit bar's View menu: its
/// icon (or initials), in the view's own colour when it has one ([S-3810]), else the theme's.
/// </summary>
internal static class ViewMark
{
    /// <summary>The view's colour as a brush, or null for the theme's.</summary>
    public static Brush? Brush(ViewState view) => DesignApplier.UserBrush(view.Color);

    /// <summary>The icon a header or a menu item shows: the view's glyph, else the shared view glyph, coloured.</summary>
    public static FontIcon Icon(ViewState view, double size = Fluent.IconSize)
    {
        ArgumentNullException.ThrowIfNull(view);
        return Icon(view.Icon, view.Color, size);
    }

    /// <summary>The same icon from a view's glyph and colour alone (the view bar's overflow menu, [S-918]).</summary>
    public static FontIcon Icon(string? glyph, string? color, double size = Fluent.IconSize)
    {
        var icon = new FontIcon { Glyph = string.IsNullOrEmpty(glyph) ? Glyphs.View : glyph, FontSize = size };
        if (DesignApplier.UserBrush(color) is { } brush)
        {
            icon.Foreground = brush;
        }

        return icon;
    }

    /// <summary>The view's glyph, or its initials where it has none (as the view bar draws it), coloured.</summary>
    public static UIElement Mark(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (!string.IsNullOrEmpty(view.Icon))
        {
            return Icon(view);
        }

        var initials = Fluent.Text(FluentType.BodyStrong);
        initials.Text = ViewListRules.Initials(view.Name);
        initials.TextTrimming = TextTrimming.None;
        initials.VerticalAlignment = VerticalAlignment.Center;
        if (Brush(view) is { } brush)
        {
            initials.Foreground = brush;
        }

        return initials;
    }

    /// <summary>A drop-down entry: the mark, then the name.</summary>
    public static FrameworkElement Labelled(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var name = Fluent.Text(FluentType.Body);
        name.Text = view.Name;
        name.VerticalAlignment = VerticalAlignment.Center;

        var mark = Mark(view);
        if (mark is FrameworkElement element)
        {
            element.MinWidth = Fluent.IconSize;
            element.VerticalAlignment = VerticalAlignment.Center;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Children = { mark, name },
        };
    }
}
