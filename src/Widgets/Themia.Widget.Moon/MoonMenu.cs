using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3108] The Moon's options, all on its properties menu ([S-3719]): Appearance ([S-3723]), then the
/// shared place, a search ([S-3101], [S-3722]). The phase is the same everywhere; the rise and set,
/// and which way round the lit side is drawn, are the place's, so the month's phases ([S-6050]) offer
/// no longitude ([S-6074]). It has no settings page.
/// </summary>
internal static class MoonMenu
{
    public static void Lines(WidgetMenuLines menu, MoonConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // The look (Style, Appearance), a divider, then the place.
        menu.PlaceCard();
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(SkyPlaceRows.Lines(menu, config, longitude: config.Offers(MoonSetting.Longitude)));
    }
}
