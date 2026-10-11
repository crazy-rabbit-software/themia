using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>
/// The current-conditions widget's settings rows, and the place and display rows both weather
/// widgets share.
/// </summary>
internal static partial class WidgetSettings
{
    public static void WeatherNow(List<FrameworkElement> rows, WeatherNowConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_Weather_Settings_Place.Text()));
        AddPlacePicker(rows, config, save);

        rows.Add(FluentLayout.Header(Str.Weather_DefaultLabel.Text()));
        AddWeatherDisplay(rows, config, save);

        rows.Add(FluentLayout.Row(Glyphs.Info, Str.WidgetsC_Weather_Settings_ShowDetail_Label.Text(),
            Str.WidgetsC_Weather_Settings_ShowDetail_Desc.Text(),
            FluentLayout.Toggle(config.ShowDetails, value => { config.ShowDetails = value; save(); })));
    }

    /// <summary>
    /// [S-2840] The location row, shared by both weather widgets: the reusable
    /// <see cref="PlaceSearch"/>, bound to a weather config. A pick replaces the place; there is no
    /// clearing it, because a weather widget without a place has nothing to show.
    /// </summary>
    private static void AddPlacePicker(List<FrameworkElement> rows, WeatherConfig config, Action save)
        => PlaceSearch.Add(rows, new PlaceSearchBinding(
            config.HasLocation ? config.LocationName : null,
            place =>
            {
                config.Latitude = place.Latitude;
                config.Longitude = place.Longitude;
                config.LocationName = place.Label;
                save();
            }));

    /// <summary>
    /// The location-name switch, shared by both weather widgets. The scale and the cadence are every
    /// widget's, in Settings › Widgets ([S-3770], [S-3780]).
    /// </summary>
    private static void AddWeatherDisplay(List<FrameworkElement> rows, WeatherConfig config, Action save)
    {
        // [S-728] Off: the header reads as the widget's label instead of the city.
        rows.Add(FluentLayout.Row(Glyphs.Place, Str.Weather_LocationNameLabel.Text(), Str.Weather_LocationNameDesc.Text(),
            FluentLayout.Toggle(!config.HideLocation, value => { config.HideLocation = !value; save(); })));
    }
}
