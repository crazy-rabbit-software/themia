using Themia.Core.Widgets;

namespace Themia.Sdk.Samples;

/// <summary>
/// The sample day ([S-11850], [S-2052]): the sample day's weather at <see cref="SamplePlace"/>, the one
/// reading every weather widget draws: an overcast autumn morning, light rain from about
/// eleven. Weather now, the forecast, air quality and the rain nowcast all read their numbers here,
/// so one temperature, one high and low, one UV index and one sunrise show on every card.
/// </summary>
public static class SampleWeather
{
    /// <summary>The temperature now, °C.</summary>
    public const double Temperature = 9.4;

    /// <summary>The UV index now, under the clouds.</summary>
    public const double UvIndex = 1.1;

    /// <summary>The reading now, with today and the next days.</summary>
    public static CurrentWeather Reading() => new(Temperature, 7.6, 84, 14, 3)
    {
        Precipitation = 0,
        Pressure = 1012,
        WindDirection = 240,
        WindGusts = 29,
        CloudCover = 92,
        UvIndex = UvIndex,
        Days = Days(7),
    };

    /// <summary>
    /// <paramref name="count"/> days from today: today the rain that is on its way (the rain widget's
    /// next hour), then a showery, clearing week. Sunrise and sunset are the place's own.
    /// </summary>
    public static IReadOnlyList<ForecastDay> Days(int count)
    {
        (int Code, double Low, double High, double Rain, double Chance, double Wind, double Uv)[] week =
        [
            (61, 6.2, 12.8, 2.6, 75, 21, 2),
            (3, 5.4, 11.6, 0.2, 25, 15, 2),
            (1, 3.8, 13.1, 0, 5, 9, 3),
            (0, 3.1, 14.4, 0, 0, 7, 3),
            (2, 5.0, 13.7, 0, 10, 11, 2),
            (80, 7.3, 11.9, 5.8, 85, 27, 1),
            (45, 4.6, 10.2, 0.1, 20, 6, 1),
        ];

        var today = SampleDay.Today;
        var days = new List<ForecastDay>();

        for (var i = 0; i < count; i++)
        {
            var d = week[i % week.Length];
            var day = today.AddDays(i);
            var (sunrise, sunset) = SamplePlace.Sun(day);
            days.Add(new ForecastDay(day, d.Code, d.Low, d.High, d.Rain)
            {
                PrecipitationChance = d.Chance,
                WindMax = d.Wind,
                UvMax = d.Uv,
                Sunrise = sunrise,
                Sunset = sunset,
            });
        }

        return days;
    }

    /// <summary>
    /// Rain over the quarter hours from the one under way at the sample moment (10:30), in mm: dry
    /// until about eleven, then the shower that today's forecast names (the same 2.6 mm).
    /// </summary>
    public static IReadOnlyList<double> NextQuarters { get; } = [0, 0, 0, 0.2, 0.5, 0.6, 0.6, 0.5, 0.2, 0];
}
