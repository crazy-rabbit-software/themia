using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.Sun;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3100] The Sun rows: the place, then the Events switch. How the day is drawn is the style
/// ([S-6045]), picked on the properties panel, what was the sun path mode.
/// </summary>
internal static class SunSettings
{
    public static void Rows(List<FrameworkElement> rows, SunConfig config, Action save)
    {
        SkyPlaceRows.Add(rows, config, save);

        // [S-6074] The times style is the events: no switch for them, nor a heading over nothing.
        if (!config.Offers(SunSetting.Events))
        {
            return;
        }

        rows.Add(FluentLayout.Header(SunStr.WidgetsC_SunMoon_Settings_Sky.Text()));

        rows.Add(FluentLayout.Row(Glyphs.View, SunStr.SunMoon_Events_Label.Text(),
            SunStr.WidgetsC_SunMoon_Settings_Events_Desc.Text(),
            FluentLayout.Toggle(config.ShowEvents, value => { config.ShowEvents = value; save(); })));
    }
}
