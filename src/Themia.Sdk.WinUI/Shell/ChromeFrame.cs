using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-3500] The look the edit bar, the placement line, the picker's card and a widget's own toolbar
/// over the desktop (the scratchpad's full screen) share: the flyouts' acrylic, their stroke, the
/// overlay radius and a flyout's shadow, so each reads as one piece of Windows chrome.
/// </summary>
public static class ChromeFrame
{
    /// <summary>The frame's lift: the depth Windows gives a flyout, which casts its shadow.</summary>
    private const float Elevation = 32;

    public static void Apply(Border border)
    {
        ArgumentNullException.ThrowIfNull(border);

        border.CornerRadius = new CornerRadius(Fluent.OverlayRadius);
        border.Background = ThemeBrush.Of("AcrylicBackgroundFillColorDefaultBrush", Colors.Black);
        border.BorderBrush = ThemeBrush.Of("SurfaceStrokeColorFlyoutBrush", Colors.Gray);
        border.BorderThickness = new Thickness(Fluent.Hairline);
        border.Shadow = new ThemeShadow();
        border.Translation = new Vector3(0, 0, Elevation);
    }
}
