using Themia.App.Shell;

namespace Themia.Core.Widgets;

/// <summary>
/// The Segoe glyph for a condition, the one both weather widgets draw, so a day reads the same in
/// the current conditions and in the forecast. [S-652] One glyph per kind of weather.
/// </summary>
public static class WeatherGlyph
{
    public static string Of(WeatherLook look) => look switch
    {
        WeatherLook.Clear => Glyphs.Sun,
        WeatherLook.PartlyCloudy or WeatherLook.Overcast => Glyphs.Cloudy,
        WeatherLook.Fog => Glyphs.Fog,
        WeatherLook.Drizzle or WeatherLook.Rain => Glyphs.Rain,
        WeatherLook.Snow => Glyphs.Snow,
        WeatherLook.Thunderstorm => Glyphs.Thunderstorm,
        _ => Glyphs.Weather,
    };
}
