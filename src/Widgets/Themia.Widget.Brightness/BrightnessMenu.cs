using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Widget.Brightness;

namespace Themia.App.Shell.Config;

/// <summary>[S-4446] The brightness widget's options, all on its properties menu ([S-3719]): Appearance, then which screens it dims. It has no settings page.</summary>
internal static class BrightnessMenu
{
    public static void Lines(WidgetMenuLines menu, BrightnessConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();

        // [S-4442] Karpo's one choice: every screen together (the default) or the one it is on.
        menu.Lines.Add(menu.Choice(Glyphs.Display, BrightnessStr.Panels_Brightness_ScreensLabel.Text(),
            [(BrightnessScreens.All, BrightnessStr.Panels_Brightness_All.Text()), (BrightnessScreens.ThisDisplay, BrightnessStr.Panels_Brightness_Own.Text())],
            config.Screens, value => config.Screens = value));
    }
}
