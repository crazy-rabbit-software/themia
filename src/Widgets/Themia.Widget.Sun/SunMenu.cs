using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Sun;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3107] The Sun's options, all on its properties menu ([S-3719]): under Style the Events switch
/// (not in the times style, which is the events: [S-6074]); then Appearance ([S-3723]) and the
/// shared place, a search ([S-3101], [S-3722]). How the day is drawn is the style ([S-6045]). It has
/// no settings page.
/// </summary>
internal static class SunMenu
{
    public static void Lines(WidgetMenuLines menu, SunConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        if (config.Offers(SunSetting.Events))
        {
            menu.StyleLines.Add(menu.Toggle(null, SunStr.SunMoon_Events_Label.Text(), config.ShowEvents, on => config.ShowEvents = on));
        }

        menu.PlaceCard();
        menu.Lines.Add(SkyPlaceRows.LocationLine(menu, config));
    }
}
