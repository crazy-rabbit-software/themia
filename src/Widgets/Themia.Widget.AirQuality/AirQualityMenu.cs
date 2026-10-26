using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Widget.AirQuality;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-5108] The air quality widget's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the location's search ([S-3722]) with the location-name switch after it, and the
/// index, whose submenu also credits the data's source (CAMS and Open-Meteo ask to be credited). It
/// has no settings page.
/// </summary>
internal static class AirQualityMenu
{
    public static void Lines(WidgetMenuLines menu, AirQualityConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // The look (Style, Appearance), then behind a divider each: the place; the index it is read on.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
        menu.Lines.Add(ItemMenuEntry.Separator);

        (AirQualityScale, string)[] scales =
        [
            (AirQualityScale.Auto, AirQualityStr.Online_Air_Settings_Index_Auto.Text()),
            (AirQualityScale.European, AirQualityStr.Online_Air_Settings_Index_European.Text()),
            (AirQualityScale.Us, AirQualityStr.Online_Air_Settings_Index_Us.Text()),
        ];

        var index = menu.Choice(AirQualityModule.Glyph, AirQualityStr.Online_Air_Settings_Index_Label.Text(), scales, config.Scale, value => config.Scale = value);
        menu.Lines.Add(index with
        {
            Children =
            [
                .. index.Children!,
                ItemMenuEntry.Separator,
                new(null, Str.Online_Source.Format(("name", "Copernicus Atmosphere Monitoring Service (CAMS) · Open-Meteo")), () => { }, Disabled: true),
            ],
        });
    }
}
