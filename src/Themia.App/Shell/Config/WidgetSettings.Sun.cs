using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>[S-3100] The Sun rows: the place, then how much of the sun's day to draw.</summary>
internal static partial class WidgetSettings
{
    public static void Sun(List<FrameworkElement> rows, SunConfig config, Action save)
    {
        SkyPlace(rows, config, save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_SunMoon_Settings_Sky.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Sun, Str.SunMoon_SunPath_Label.Text(), Str.SunMoon_SunPath_Desc.Text(),
            FluentLayout.Choice(
                [
                    (SunPathMode.Numbers, Str.SunMoon_PathMode_Numbers.Text()),
                    (SunPathMode.Plain, Str.SunMoon_PathMode_Plain.Text()),
                    (SunPathMode.Hidden, Str.SunMoon_PathMode_Hidden.Text()),
                ],
                config.PathMode,
                value => { config.PathMode = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.View, Str.SunMoon_Events_Label.Text(),
            Str.WidgetsC_SunMoon_Settings_Events_Desc.Text(),
            FluentLayout.Toggle(config.ShowEvents, value => { config.ShowEvents = value; save(); })));
    }
}
