#if DEBUG
namespace Themia.Core.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixture reading for Zürich on a cloudy autumn afternoon and its
/// week, the one sample both weather widgets' previews draw (the forecast's "now" is its temperature).
/// </summary>
public static class WeatherSample
{
    /// <summary>A realistic reading, with today and the next days, shared with the forecast's preview.</summary>
    public static CurrentWeather Reading() => new(14.2, 12.6, 71, 13, 2)
    {
        Precipitation = 0,
        Pressure = 1016,
        WindDirection = 240,
        WindGusts = 27,
        CloudCover = 58,
        UvIndex = 2.4,
        Days = Days(7),
    };

    /// <summary>A week of autumn days, today first.</summary>
    public static IReadOnlyList<ForecastDay> Days(int count)
    {
        (int Code, double Low, double High, double Rain, double Chance, double Wind)[] week =
        [
            (2, 8.4, 16.1, 0, 10, 14),
            (61, 9.2, 13.4, 4.2, 80, 22),
            (3, 7.1, 12.0, 0.4, 35, 18),
            (0, 4.6, 15.3, 0, 0, 9),
            (1, 5.8, 17.2, 0, 5, 11),
            (80, 8.9, 14.6, 6.8, 90, 31),
            (45, 6.3, 11.8, 0.1, 20, 7),
        ];

        var today = DateOnly.FromDateTime(DateTime.Now);
        var days = new List<ForecastDay>();

        for (var i = 0; i < count; i++)
        {
            var d = week[i % week.Length];
            days.Add(new ForecastDay(today.AddDays(i), d.Code, d.Low, d.High, d.Rain)
            {
                PrecipitationChance = d.Chance,
                WindMax = d.Wind,
                UvMax = 3,
                Sunrise = new TimeOnly(7, 41 + (i % 3)),
                Sunset = new TimeOnly(18, 32 - (i % 3)),
            });
        }

        return days;
    }
}
#endif
