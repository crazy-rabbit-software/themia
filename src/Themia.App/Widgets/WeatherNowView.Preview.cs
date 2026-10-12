#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixture reading for Zürich on a cloudy autumn afternoon, so the
/// ladder can be looked at without a place, the network or the sync service. Nothing is fetched.
/// </summary>
internal sealed partial class WeatherNowView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state != "sample")
        {
            return Task.CompletedTask;
        }

        _config = new WeatherNowConfig { Latitude = 47.37, Longitude = 8.54, LocationName = "Zürich, Zurich, Switzerland" };
        _applied = (WeatherNowConfig)_config.Clone();
        _want = null;
        _reading = new Fetched<CurrentWeather>(FetchStatus.Ok, SampleReading(), null, DateTimeOffset.Now.AddMinutes(-4));
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }

    /// <summary>A realistic reading, with today and the next days, shared with the forecast's preview.</summary>
    internal static CurrentWeather SampleReading() => new(14.2, 12.6, 71, 13, 2)
    {
        Precipitation = 0,
        Pressure = 1016,
        WindDirection = 240,
        WindGusts = 27,
        CloudCover = 58,
        UvIndex = 2.4,
        Days = SampleDays(7),
    };

    /// <summary>A week of autumn days, today first.</summary>
    internal static IReadOnlyList<ForecastDay> SampleDays(int count)
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
