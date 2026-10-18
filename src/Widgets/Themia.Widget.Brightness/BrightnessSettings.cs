using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.Brightness;

namespace Themia.App.Shell.Config;

/// <summary>[S-4446] The brightness widget's settings page: which screens it dims.</summary>
internal static class BrightnessSettings
{
    public static void Rows(List<FrameworkElement> rows, BrightnessConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(BrightnessStr.Panels_Brightness_Label.Text()));

        // [S-4442] Karpo's one choice: every screen together (the default) or the one it is on.
        rows.Add(FluentLayout.Row(Glyphs.Display, BrightnessStr.Panels_Brightness_ScreensLabel.Text(), BrightnessStr.Panels_Brightness_ScreensDesc.Text(),
            FluentLayout.Choice(
                [(BrightnessScreens.All, BrightnessStr.Panels_Brightness_All.Text()), (BrightnessScreens.ThisDisplay, BrightnessStr.Panels_Brightness_Own.Text())],
                config.Screens,
                value => { config.Screens = value; save(); }),
            "monitor", "display", "screen"));
    }
}
