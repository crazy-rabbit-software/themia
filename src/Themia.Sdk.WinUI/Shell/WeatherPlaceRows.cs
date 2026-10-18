using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

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
}
