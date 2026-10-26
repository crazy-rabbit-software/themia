using System.Globalization;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3101] What the Sun and Moon widgets share on their properties menus ([S-3719]): where the
/// observer is.
/// <para>
/// [S-1494] The place is picked by city name, as in the original [S-01], through the same search the
/// weather widgets use (<see cref="PlaceSearch.MenuAnswerAsync"/>). It resolves to coordinates once,
/// at the pick; the astronomy itself stays offline. Under the answers the exact coordinates stay
/// editable, each asked for as text and validated, for a place no geocoder knows (a cabin, a ship, a pole).
/// </para>
/// </summary>
public static class SkyPlaceRows
{
    /// <summary>
    /// [S-3101] [S-2850] The place's lines: Use my location (with what Windows says while it gives no
    /// position), then, while it is off, the location's search (<see cref="LocationLine"/>).
    /// </summary>
    public static IReadOnlyList<ItemMenuEntry> Lines(WidgetMenuLines menu, SkyPlaceConfig config, bool longitude = true)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        List<ItemMenuEntry> lines = [.. DevicePlaceRows.Lines(menu, config)];
        if (!config.UseDeviceLocation)
        {
            lines.Add(LocationLine(menu, config, longitude));
        }

        return lines;
    }

    /// <summary>
    /// [S-3101] [S-3722] The location as a search: the chosen place (or its coordinates) at the end of
    /// its line; in its submenu the search box and the places it finds (a pick replaces the place and
    /// its coordinates), then Clear location ([S-2848]), the latitude and, where the style reads it
    /// (<paramref name="longitude"/>, [S-6074]), the longitude. The place's name is what the search
    /// found; the header's title is the Header row's to change ([S-3590]), never a second line here.
    /// </summary>
    private static ItemMenuEntry LocationLine(WidgetMenuLines menu, SkyPlaceConfig config, bool longitude)
    {
        List<ItemMenuEntry> under =
        [
            menu.Command(Glyphs.Dismiss, Str.Shell_Place_Clear.Text(), config.ClearLocation, disabled: !config.HasLocation),
            Coordinate(menu, Str.SunMoon_Latitude_Label.Text(), config.Latitude, -90, 90, value => config.Latitude = value),
        ];

        if (longitude)
        {
            under.Add(Coordinate(menu, Str.SunMoon_Longitude_Label.Text(), config.Longitude, -180, 180, value => config.Longitude = value));
        }

        var search = new MenuSearch(
            Str.Shell_Place_SearchPlaceholder.Text(),
            (typed, cancel) => PlaceSearch.MenuAnswerAsync(typed, place =>
            {
                config.Latitude = place.Latitude;
                config.Longitude = place.Longitude;
                config.Place = Cut(place.Label);
                menu.Save();
            }, cancel),
            under,
            PlaceQuery.Debounce);

        return WidgetMenuLines.Search(Glyphs.Place, Str.Shell_Place_Location.Text(), Chosen(config), search);
    }

    /// <summary>
    /// [S-2848] What the location's line names: the place's first part, or its coordinates when it has
    /// no name; null when there is no place.
    /// </summary>
    public static string? Chosen(SkyPlaceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.HasLocation)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(config.Place)
            ? $"{FormatCoordinate(config.Latitude!.Value)}, {FormatCoordinate(config.Longitude!.Value)}"
            : config.Place.Split(',', 2)[0].Trim();
    }

    /// <summary>
    /// [S-171] A typed coordinate's refusal: the reason it cannot be used, or null. Deliberately no
    /// fallback: coercing unreadable input to 0 would quietly move the user to the Gulf of Guinea and
    /// report sunrise there as though nothing had happened.
    /// </summary>
    public static string? RefuseCoordinate(string typed, double minimum, double maximum)
        => ParseCoordinate(typed, minimum, maximum) is null
            ? Str.WidgetsC_SunMoon_Settings_CoordinateRange.Format(("min", FormatCoordinate(minimum)), ("max", FormatCoordinate(maximum)))
            : null;

    /// <summary>[S-171] A typed coordinate: invariant digits, finite, inside the range; null for anything else.</summary>
    public static double? ParseCoordinate(string typed, double minimum, double maximum)
        => double.TryParse((typed ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value)
            && value >= minimum
            && value <= maximum
                ? value
                : null;

    /// <summary>
    /// [S-171] [S-3721] A coordinate asked for as text, refused (with the range as the reason) unless it
    /// is a number in range; [S-2848] the field of a cleared place starts empty.
    /// </summary>
    private static ItemMenuEntry Coordinate(WidgetMenuLines menu, string title, double? current, double minimum, double maximum, Action<double> set)
        => new(Glyphs.Grid, title, () => menu.Ask(new WidgetTextAsk(
            title,
            FormatCoordinate(0),
            Str.Common_Save.Text(),
            typed => RefuseCoordinate(typed, minimum, maximum),
            typed =>
            {
                if (ParseCoordinate(typed, minimum, maximum) is { } value && value != current)
                {
                    set(value);
                    menu.Save();
                }
            },
            Initial: current is { } known ? FormatCoordinate(known) : null)), Value: current is { } shown ? FormatCoordinate(shown) : null, Asks: true);

    /// <summary>A place name is a header title, not a document: cut as load repair cuts it ([S-3101]).</summary>
    private static string Cut(string name) => name.Length > SkyPlaceConfig.MaxPlaceLength ? name[..SkyPlaceConfig.MaxPlaceLength] : name;

    private static string FormatCoordinate(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
