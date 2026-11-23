using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Widget.WeatherForecast;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2860] [S-16974] The forecast widget's options, all on its page ([S-3719]): Appearance ([S-3723]), the location's search
/// ([S-3722]) and the precipitation switch; on the item base's styles what a day shows (its parts in order: the table's
/// columns) and the gap or the table's column titles. How many days is the card's ([S-14410]): no line sets it.
/// </summary>
internal static class WeatherForecastMenu
{
    public static void Lines(WidgetMenuLines menu, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // The look (Style, Appearance), then behind a divider each: the place; what each day shows. [S-14410] How many
        // days is the card's: what its box holds, never stored ([S-14502]).
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.Add(menu.Toggle(Glyphs.Droplet, WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text(), config.ShowPrecipitation, on => config.ShowPrecipitation = on));
        if (ForecastItems.Arrangement(config.Style) is not { } style)
        {
            return;
        }

        var locale = Strings.Current;
        menu.Lines.Add(ItemMenuLines.Parts(menu, config, ForecastItems.Parts, name: part => ForecastItems.PartName(part, locale)));
        menu.Lines.Add(ItemLayout.TakesGap(style)
            ? ItemMenuLines.Gap(menu, config.Gap, value => config.Gap = value)
            : menu.Toggle(Glyphs.View, Str.Items_ColumnTitles.Text(), config.ColumnTitles, on => config.ColumnTitles = on));
    }
}
