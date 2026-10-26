using Themia.App.Services.Brightness;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Brightness;

namespace Themia.App.Shell.Config;

/// <summary>[S-4446] The brightness widget's options, all on its properties menu ([S-3719]): Appearance, then which screens it dims. It has no settings page.</summary>
internal static class BrightnessMenu
{
    /// <param name="displays">The displays with a brightness (<c>BrightnessDisplays.Now()</c>).</param>
    public static void Lines(WidgetMenuLines menu, BrightnessConfig config, BrightnessSnapshot displays)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);


        // [S-4447] Only where the choice changes what is dimmed: one display with a brightness is dimmed either way.
        if (!BrightnessRules.OffersScreens(displays, config.Screens))
        {
            return;
        }

        // [S-4442] Karpo's one choice: every screen together (the default) or the one it is on.
        menu.Lines.Add(menu.Choice(Glyphs.Display, BrightnessStr.Panels_Brightness_ScreensLabel.Text(),
            [(BrightnessScreens.All, BrightnessStr.Panels_Brightness_All.Text()), (BrightnessScreens.ThisDisplay, BrightnessStr.Panels_Brightness_Own.Text())],
            config.Screens, value => config.Screens = value));
    }
}
