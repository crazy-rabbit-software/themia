using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Widget.WeatherForecast;
using Themia.Core.Sync;
using Themia.Sdk;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2860] The forecast widget's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the location's search ([S-3722]) with the location-name switch after it and the precipitation
/// switch. How many days is the card's ([S-14410]): no line sets it. It has no settings page.
/// </summary>
internal static class WeatherForecastMenu
{
    public static void Lines(WidgetMenuLines menu, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // [S-11147] Under Style: the rows' lines per row (the rows style only).
        if (config.Style == ForecastStyle.Rows)
        {
            menu.StyleLines.Add(menu.Choice(
                null,
                Str.Kit_Lines.Text(),
                [(1, Str.Kit_LinesOne.Text()), (2, Str.Kit_LinesTwo.Text())],
                config.EffectiveLines,
                v => config.Lines = v));
        }

        // The look (Style, Appearance), then behind a divider each: the place; what each day shows. [S-14410] How many
        // days is the card's: a resize stores the days its box holds.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Toggle(Glyphs.Droplet, WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text(), config.ShowPrecipitation, on => config.ShowPrecipitation = on));
    }
}
