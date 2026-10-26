using Themia.Core.Localization;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// The location line the weather family's properties menus share (current conditions, forecast,
/// air quality, rain).
/// </summary>
public static class WeatherPlaceRows
{
    /// <summary>
    /// [S-2840] [S-3722] The place's lines on the properties menu, shared by the weather family: Use my
    /// location ([S-2850], with what Windows says while it gives no position), then, while it is off,
    /// the location as a search, the chosen place at the end of its line, in its submenu the search box,
    /// the places it finds (a click picks one: replaced at once, resolved once) and Clear location
    /// ([S-2848]: the widget waits for setup again). The place's name is the header's title unless one
    /// is typed: the Header row's Title is the one way to change it ([S-728], [S-3590]).
    /// </summary>
    public static IReadOnlyList<ItemMenuEntry> Lines(WidgetMenuLines menu, WeatherConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        List<ItemMenuEntry> lines = [.. DevicePlaceRows.Lines(menu, config)];

        // [S-3718] One source of the place at a time: the search is not there while the device's is used.
        if (!config.UseDeviceLocation)
        {
            lines.Add(LocationLine(menu, config));
        }

        return lines;
    }

    /// <summary>[S-2840] The location's search: what is found, and Clear location under it.</summary>
    private static ItemMenuEntry LocationLine(WidgetMenuLines menu, WeatherConfig config)
    {
        List<ItemMenuEntry> under = [menu.Command(Glyphs.Dismiss, Str.Shell_Place_Clear.Text(), config.ClearLocation, disabled: !config.HasLocation)];

        var search = new MenuSearch(
            Str.Shell_Place_SearchPlaceholder.Text(),
            (typed, cancel) => PlaceSearch.MenuAnswerAsync(typed, place =>
            {
                config.Latitude = place.Latitude;
                config.Longitude = place.Longitude;
                config.LocationName = place.Label;
                menu.Save();
            }, cancel),
            under,
            PlaceQuery.Debounce);

        // [S-1862] A widget of the family waits for its place: Set up opens this search.
        return WidgetMenuLines.SetsUp(WidgetMenuLines.Search(Glyphs.Place, Str.Shell_Place_Location.Text(),
            config.HasLocation ? config.LocationName.Split(',', 2)[0].Trim() : null, search));
    }
}
