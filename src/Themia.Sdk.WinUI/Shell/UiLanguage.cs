using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;

namespace Themia.App.Shell;

/// <summary>
/// [S-1110] / [S-1113] / [S-1114] The language XAML lays text out in, and the direction it flows.
/// <para>
/// The tag picks the fallback font per script (<see cref="TextDirection.LayoutTag"/>) and the
/// hyphenation; left alone it follows Windows' display language, not the one chosen in Themia, and
/// Japanese would be drawn with Chinese glyph shapes on an English Windows.
/// </para>
/// <para>
/// The direction is set on the roots whose whole content is text-like UI — the settings window,
/// every card, the edit bar, the view bar, the notices, the placement hint, a menu's anchor — and
/// never on a desktop surface itself: its canvases are physical screen geometry (where a widget
/// sits, the hit region, the rubber band), the same in every language. Flyouts, menus and tooltips
/// take the direction of the element they open from.
/// </para>
/// </summary>
public static class UiLanguage
{
    public static string Tag(Locale locale) => TextDirection.LayoutTag(locale);

    /// <summary>The flow of the current language.</summary>
    public static FlowDirection Flow
        => TextDirection.IsRightToLeft(Strings.Current) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Set the current language and its direction on a root; its children inherit both.</summary>
    public static void Apply(FrameworkElement? root)
    {
        if (root is not null)
        {
            root.Language = Tag(Strings.Current);
            root.FlowDirection = Flow;
        }
    }

    /// <summary>The language alone, for a desktop surface whose layers are screen geometry and must not mirror.</summary>
    public static void ApplyLanguageOnly(FrameworkElement? root)
    {
        if (root is not null)
        {
            root.Language = Tag(Strings.Current);
            root.FlowDirection = FlowDirection.LeftToRight;
        }
    }

    /// <summary>The direction alone, for a root inside a surface that already carries the language.</summary>
    public static void ApplyFlow(FrameworkElement? element)
    {
        if (element is not null)
        {
            element.FlowDirection = Flow;
        }
    }

    /// <summary>
    /// [S-1113] Left to right in every language: media transport, charts over time, clock faces,
    /// and edit chrome whose parts are physical corners.
    /// </summary>
    public static T KeepLeftToRight<T>(T element)
        where T : FrameworkElement
    {
        element.FlowDirection = FlowDirection.LeftToRight;
        return element;
    }

    /// <summary>[S-1113] Turn a directional icon (back, chevron, send …) round when the UI runs right to left.</summary>
    public static FontIcon Mirrored(FontIcon icon)
    {
        icon.MirroredWhenRightToLeft = TextDirection.MirrorsGlyph(icon.Glyph);
        return icon;
    }
}
