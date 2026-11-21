using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// The location line the weather family's pages share (current conditions, forecast,
/// air quality, rain).
/// </summary>
public static class WeatherPlaceRows
{
    /// <summary>
    /// [S-2840] [S-3722] The place's lines on the selection's page, shared by the weather family: Use my
    /// location ([S-2850], with what Windows says while it gives no position), then, while it is off,
    /// the location as a search, the chosen place at the end of its line, in its submenu the search box,
    /// the places it finds (a click picks one: replaced at once, resolved once) and Clear location
    /// ([S-2848]: the widget waits for setup again). Any widget about a place takes them, the Header widget too
    /// ([S-9604]), over the shared <see cref="IPlaced"/>.
    /// </summary>
    public static IReadOnlyList<SettingsLine> Lines(WidgetMenuLines menu, IPlaced config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        List<SettingsLine> lines = [.. DevicePlaceRows.Lines(menu, config)];

        // [S-3718] One source of the place at a time: the search is not there while the device's is used.
        if (!config.UseDeviceLocation)
        {
            lines.Add(LocationLine(menu, config));
        }

        // [S-11753] The place is one setting every widget about a place holds: a group sets it once.
        return WidgetMenuLines.Shared(SharedSettings.Place, lines);
    }

    /// <summary>
    /// [S-9604] The place's one line where it is only named, never measured (the Header widget's): the location's
    /// search alone, no Use my location (owner, 2026-11-19: "show the place should not be a caption setting, use my
    /// location neither"); a place found there replaces the device's. Marked as the shared place's line.
    /// </summary>
    public static SettingsLine Named(WidgetMenuLines menu, IPlaced config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        return WidgetMenuLines.Shared(SharedSettings.Place, LocationLine(menu, config, named: true));
    }

    /// <summary>[S-2840] The location's search: what is found, and Clear location under it.</summary>
    private static SettingsLine LocationLine(WidgetMenuLines menu, IPlaced config, bool named = false)
    {
        List<SettingsLine> under = [menu.Command(Glyphs.Dismiss, Str.Shell_Place_Clear.Text(), config.ClearLocation, disabled: !config.HasLocation)];

        var line = WidgetMenuLines.Search(Glyphs.Place, Str.Shell_Place_Location.Text(),
            config.HasLocation ? config.PlaceName.Split(',', 2)[0].Trim() : null,
            Str.Shell_Place_SearchPlaceholder.Text(),
            (typed, cancel) => PlaceSearch.MenuAnswerAsync(typed, place => config.HasLocation && config.PlaceName == place.Label, place =>
            {
                config.Latitude = place.Latitude;
                config.Longitude = place.Longitude;
                config.PlaceName = place.Label;
                config.UseDeviceLocation = false;
                menu.Save();
            }, cancel),
            under,
            PlaceQuery.Debounce);

        // [S-1862] A widget of the family waits for its place: Set up opens this search. One that only names it waits for none.
        return named ? line : WidgetMenuLines.SetsUp(line);
    }
}
