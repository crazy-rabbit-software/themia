using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3100] The Sun rows: the place, then the Events switch. How the day is drawn is the shared
/// Style row ([S-6045], [S-6010]), what was the sun path mode.
/// </summary>
internal static partial class WidgetSettings
{
    public static void Sun(List<FrameworkElement> rows, SunConfig config, Action save)
    {
        SkyPlace(rows, config, save);

        // [S-6074] The times style is the events: no switch for them, nor a heading over nothing.
        if (!config.Offers(SunSetting.Events))
        {
            return;
        }

        rows.Add(FluentLayout.Header(Str.WidgetsC_SunMoon_Settings_Sky.Text()));

        rows.Add(FluentLayout.Row(Glyphs.View, Str.SunMoon_Events_Label.Text(),
            Str.WidgetsC_SunMoon_Settings_Events_Desc.Text(),
            FluentLayout.Toggle(config.ShowEvents, value => { config.ShowEvents = value; save(); })));
    }
}
