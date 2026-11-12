using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Sun;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3107] The Sun's options, all on its properties menu ([S-3719]): Style (how the day is drawn,
/// [S-6045]) and Appearance ([S-3723]), then the shared place, a search ([S-3101], [S-3722]). It has
/// no settings page.
/// </summary>
internal static class SunMenu
{
    public static void Lines(WidgetMenuLines menu, SunConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // The look (Style, Appearance), a divider, then the place.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(SkyPlaceRows.Lines(menu, config));
    }
}
