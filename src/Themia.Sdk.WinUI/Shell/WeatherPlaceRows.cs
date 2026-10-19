using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// The place and display rows the weather family's settings share (current conditions, forecast,
/// air quality, rain).
/// </summary>
public static class WeatherPlaceRows
{
    /// <summary>
    /// [S-2840] The location row, shared by the weather family (current conditions, forecast, air
    /// quality, rain): the reusable <see cref="PlaceSearch"/>, bound to a weather config. A pick
    /// replaces the place; [S-2848] clearing it leaves the widget to its Set up state.
    /// </summary>
    public static void AddPlacePicker(List<FrameworkElement> rows, WeatherConfig config, Action save)
        => PlaceSearch.Add(rows, new PlaceSearchBinding(
            config.HasLocation ? config.LocationName : null,
            place =>
            {
                config.Latitude = place.Latitude;
                config.Longitude = place.Longitude;
                config.LocationName = place.Label;
                save();
            },
            () =>
            {
                config.ClearLocation();
                save();
            }));

    /// <summary>
    /// The location-name switch, shared by both weather widgets. The scale (Settings › Language &amp;
    /// region, [S-3770]) and the cadence (Settings › Widgets, [S-3780]) are every widget's.
    /// </summary>
    public static void AddWeatherDisplay(List<FrameworkElement> rows, WeatherConfig config, Action save)
    {
        // [S-728] Off: the header reads as the widget's label instead of the city.
        rows.Add(FluentLayout.Row(Glyphs.Place, Str.Weather_LocationNameLabel.Text(), Str.Weather_LocationNameDesc.Text(),
            FluentLayout.Toggle(!config.HideLocation, value => { config.HideLocation = !value; save(); })));
    }

    /// <summary>
    /// [S-2840] [S-3722] The location as a search on the properties menu, shared by the weather family:
    /// the chosen place at the end of its line; in its submenu the search box, the places it finds (a
    /// click picks one: replaced at once, resolved once), then Clear location ([S-2848]: the widget
    /// waits for setup again) and the location-name switch ([S-728]).
    /// </summary>
    public static ItemMenuEntry LocationLine(WidgetMenuLines menu, WeatherConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        var search = new MenuSearch(
            Str.Shell_Place_SearchPlaceholder.Text(),
            (typed, cancel) => PlaceSearch.MenuAnswerAsync(typed, place =>
            {
                config.Latitude = place.Latitude;
                config.Longitude = place.Longitude;
                config.LocationName = place.Label;
                menu.Save();
            }, cancel),
            [
                menu.Command(Glyphs.Dismiss, Str.Shell_Place_Clear.Text(), config.ClearLocation, disabled: !config.HasLocation),
                menu.Toggle(null, Str.Weather_LocationNameLabel.Text(), !config.HideLocation, on => config.HideLocation = !on),
            ],
            PlaceQuery.Debounce);

        return WidgetMenuLines.Search(Glyphs.Place, Str.Shell_Place_Location.Text(),
            config.HasLocation ? config.LocationName.Split(',', 2)[0].Trim() : null, search);
    }
}
