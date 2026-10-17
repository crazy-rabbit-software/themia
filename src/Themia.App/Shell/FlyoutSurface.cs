using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-3715] A Windows 11 flyout's surface for chrome that stands on the desktop surface beside the
/// widgets (the properties panel): the flyout's solid fill, its stroke, the overlay corners and a
/// flyout's drop shadow, so it reads as a raised sheet over any wallpaper and any card.
/// <para>
/// The fill is the flyout presenter's solid colour (<c>SolidBackgroundFillColorQuarternary</c>,
/// #2C2C2C dark / #FFFFFF light: the fallback of <c>AcrylicInAppFillColorDefault</c>), not acrylic.
/// In-app acrylic blurs only what the window itself draws, and the surface window draws almost
/// nothing behind the panel (it is transparent over the wallpaper), so the acrylic came out as a
/// faint tint the colour of the cards beside it: the panel did not stand apart from them.
/// </para>
/// <para>
/// The shadow is a composition drop shadow on a plate under the frame rather than a
/// <see cref="ThemeShadow"/>: outside a popup a theme shadow needs receivers to fall on, and there
/// is nothing under the panel to receive it but the transparent window. A layer visual's shadow
/// follows the plate's rounded clip, so it has the panel's corners. It falls inside the edit-mode
/// hit region (the whole work area, [S-851]); the panel is never shown outside edit mode.
/// </para>
/// </summary>
internal static class FlyoutSurface
{
    private const string Scope = "flyout-surface";

    /// <summary>Fluent's flyout shadow (elevation 32): a key light 8 DIPs down, blurred over 32.</summary>
    private const float ShadowOffsetY = 8;

    private const float ShadowBlur = 32;

    /// <summary>How dark the shadow is: Windows draws it deeper on the dark theme, where a lighter one would not show.</summary>
    private const float ShadowOpacityLight = 0.18f;

    private const float ShadowOpacityDark = 0.42f;

    /// <summary>
    /// Give <paramref name="frame"/> the flyout's surface, and cast its shadow from a plate inserted
    /// behind it in <paramref name="host"/>, which the frame must fill.
    /// </summary>
    public static void Apply(Grid host, Border frame)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(frame);

        frame.CornerRadius = new CornerRadius(Fluent.OverlayRadius);
        frame.Background = ThemeBrush.Of("SolidBackgroundFillColorQuarternaryBrush", Colors.Black);
        frame.BorderBrush = ThemeBrush.Of("SurfaceStrokeColorFlyoutBrush", Colors.Gray);
        frame.BorderThickness = new Thickness(Fluent.Hairline);

        var caster = new Border { IsHitTestVisible = false };
        host.Children.Insert(0, caster);

        try
        {
            var compositor = ElementCompositionPreview.GetElementVisual(caster).Compositor;
            var geometry = compositor.CreateRoundedRectangleGeometry();
            geometry.CornerRadius = new Vector2((float)Fluent.OverlayRadius);

            // The plate only shapes the shadow: it lies under the opaque frame and is never seen, so
            // its colour is not a design colour. Inset a hair so no anti-aliased edge of it shows.
            var plate = compositor.CreateSpriteVisual();
            plate.Brush = compositor.CreateColorBrush(Colors.Black);
            plate.Clip = compositor.CreateGeometricClip(geometry);
            plate.Offset = new Vector3((float)Fluent.Hairline, (float)Fluent.Hairline, 0);

            var shadow = compositor.CreateDropShadow();
            shadow.Color = Colors.Black;
            shadow.BlurRadius = ShadowBlur;
            shadow.Offset = new Vector3(0, ShadowOffsetY, 0);

            var layer = compositor.CreateLayerVisual();
            layer.Children.InsertAtTop(plate);
            layer.Shadow = shadow;
            ElementCompositionPreview.SetElementChildVisual(caster, layer);

            void Size(Windows.Foundation.Size size)
            {
                var inner = new Vector2(
                    (float)Math.Max(0, size.Width - (2 * Fluent.Hairline)),
                    (float)Math.Max(0, size.Height - (2 * Fluent.Hairline)));
                layer.Size = new Vector2((float)size.Width, (float)size.Height);
                plate.Size = inner;
                geometry.Size = inner;
            }

            void Theme() => shadow.Opacity = frame.ActualTheme == ElementTheme.Light ? ShadowOpacityLight : ShadowOpacityDark;

            frame.SizeChanged += (_, e) => Size(e.NewSize);
            frame.ActualThemeChanged += (_, _) => Theme();
            Theme();
        }
        catch (Exception ex)
        {
            // The panel still works and still has its fill and stroke; only its lift is missing.
            Log.Warn(Scope, "could not cast the flyout shadow", ex);
        }
    }
}
