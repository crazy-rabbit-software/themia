using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Themia.App.Shell;

/// <summary>
/// [S-903] A glyph setting: a button showing the current icon that opens the icon picker
/// (<see cref="IconPicker"/>: every Segoe Fluent glyph, searchable, [S-1803]). Stores the glyph
/// itself (one character), or null for the default. Used for a view's icon.
/// </summary>
internal static class IconChoice
{
    /// <param name="value">The current glyph, or null for the default.</param>
    /// <param name="defaultGlyph">What the default looks like.</param>
    /// <param name="onChanged">Gets the chosen glyph, or null for the default.</param>
    /// <param name="defaultText">
    /// [S-903] Text the default shows instead of <paramref name="defaultGlyph"/> — a view's initials,
    /// which is what a view without an icon really looks like in the view bar and switchers.
    /// </param>
    /// <param name="defaultName">The default cell's tooltip and accessible name.</param>
    public static DropDownButton Create(string? value, string defaultGlyph, Action<string?> onChanged, string? defaultText = null, string? defaultName = null)
    {
        ArgumentNullException.ThrowIfNull(onChanged);

        var look = new IconLook(defaultGlyph, defaultText);
        var current = string.IsNullOrEmpty(value) ? null : value;
        var button = new DropDownButton { Content = look.For(current) };
        var flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };

        // [S-1803] Built when it opens, not with the page: a settings page holding a closed picker
        // should not pay for a 1,500-glyph grid, and the selection is the value as it is now.
        flyout.Opening += (_, _) => flyout.Content = new IconPicker(current, look, defaultName, glyph =>
        {
            current = glyph;
            button.Content = look.For(glyph);
            flyout.Hide();
            onChanged(glyph);
        });

        // Typing searches at once: the caret is in the box as soon as the picker is open.
        flyout.Opened += (_, _) => (flyout.Content as IconPicker)?.FocusSearch();
        flyout.Closed += (_, _) => flyout.Content = null;

        button.Flyout = flyout;
        return button;
    }
}

/// <summary>How an icon choice draws a value: the glyph, or for the default the default glyph or text.</summary>
internal sealed class IconLook(string defaultGlyph, string? defaultText)
{
    public UIElement For(string? glyph) => glyph is null && defaultText is not null
        ? new TextBlock { Text = defaultText, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }
        : new FontIcon { Glyph = glyph ?? defaultGlyph, FontSize = 16 };
}
