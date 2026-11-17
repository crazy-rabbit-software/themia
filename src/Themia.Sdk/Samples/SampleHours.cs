using Themia.Core.Widgets;

namespace Themia.Sdk.Samples;

/// <summary>
/// [S-11850] [S-12709] The sample day hour by hour at <see cref="SamplePlace"/>, for the weather graphs: from the
/// hour before the sample moment through the next two days, agreeing with <see cref="SampleWeather"/> (each day's low
/// and high, today's 2.6 mm shower from eleven, the 9 °C now, the wind from the south-west, the place's own sunrise
/// and sunset), and four days of air agreeing with the air widget's index of 25. A summer day (Debug renders) shows the
/// heat and the UV bands the autumn sample never reaches.
/// </summary>
public static class SampleHours
{
    /// <summary>The sample day's next two days.</summary>
    public static WeatherHours Weather() => Build(summer: false);

    /// <summary>A hot, clear July-like day at the place: UV to 9, 33 °C, a late thunderstorm.</summary>
    public static WeatherHours Summer() => Build(summer: true);

    /// <summary>[S-12709] The air's next four days hour by hour: a clean morning, an afternoon ozone rise, a hazier third day.</summary>
    public static AirHours Air()
    {
        var first = FirstHour;
        var hours = new List<AirHour>();
        for (var i = 0; i <= WeatherHoursWire.AirHourCount; i++)
        {
            var at = first.AddHours(i);
            var hour = Local(at).Hour;
            var day = i / 24.0;

            // Ozone follows the sun, the particles the evening traffic and a hazier third day.
            var sun = Math.Max(0, Math.Sin(Math.PI * (hour - 7) / 12));
            var haze = 1 + (0.9 * Math.Exp(-Math.Pow(day - 2.4, 2) * 2));
            var evening = 1 + (0.5 * Math.Exp(-Math.Pow(hour - 19, 2) / 6));
            var pm25 = Math.Round(6.5 * haze * evening, 1);
            var ozone = Math.Round(30 + (45 * sun), 1);
            var no2 = Math.Round(12 + (18 * Math.Exp(-Math.Pow(hour - 8, 2) / 4)) + (14 * Math.Exp(-Math.Pow(hour - 18, 2) / 5)), 1);
            var pm10 = Math.Round(pm25 * 1.6, 1);
            hours.Add(new AirHour(at)
            {
                Pm25 = pm25,
                Pm10 = pm10,
                Ozone = ozone,
                NitrogenDioxide = no2,
                EuropeanAqi = Math.Round(Math.Max(pm25 * 25 / 10, Math.Max(ozone * 20 / 50, no2 * 20 / 40))),
                UsAqi = Math.Round(Math.Max(pm25 * 50 / 9, ozone * 50 / 108)),
            });
        }

        return new AirHours(hours);
    }

    /// <summary>The hour under way at the sample moment, less one: what the service sends with <c>past_hours=1</c>.</summary>
    private static DateTimeOffset FirstHour
    {
        get
        {
            var now = SampleDay.Now;
            return DateTimeOffset.FromUnixTimeSeconds((now.ToUnixTimeSeconds() / 3600 * 3600) - 3600).ToOffset(now.Offset);
        }
    }

    private static DateTime Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, TimeZoneInfo.Local).DateTime;

    private static WeatherHours Build(bool summer)
    {
        var days = SampleWeather.Days(4);
        var first = FirstHour;
        var hours = new List<WeatherHour>();
        for (var i = 0; i <= WeatherHoursWire.Hours; i++)
        {
            var at = first.AddHours(i);
            var local = Local(at);
            var d = Math.Clamp(DateOnly.FromDateTime(local).DayNumber - SampleDay.Today.DayNumber, 0, days.Count - 2);
            var temperature = Diurnal((DateOnly.FromDateTime(local).DayNumber - SampleDay.Today.DayNumber) * 24.0 + local.Hour + (local.Minute / 60.0), days, summer);
            var rain = summer ? SummerRain(local, d) : Rain(local, d);
            hours.Add(new WeatherHour(at)
            {
                Temperature = Math.Round(temperature, 1),
                FeelsLike = Math.Round(summer ? temperature + 1.8 : temperature - 2.1, 1),
                Precipitation = rain,
                PrecipitationChance = summer ? (rain > 0 ? 70 : 10) : Chance(local, d),
                Uv = Math.Round(Uv(local.Hour, summer ? 9.1 : days[d].UvMax ?? 2), 1),
                Wind = Math.Round(Wind(i, summer), 1),
                Gusts = Math.Round(Wind(i, summer) * 2.05, 1),
                WindDirection = (240 + (i * 3)) % 360,
                Humidity = Math.Round(Math.Clamp(summer ? 45 + ((33 - temperature) * 2.2) : 84 + ((SampleWeather.Temperature - temperature) * 2.5) + (rain * 4), 20, 100)),
                Pressure = Math.Round(summer ? 1016 - (i * 0.12) : 1012 - (4.5 * Math.Sin(i * Math.PI / 30)), 1),
            });
        }

        var spans = new List<DaylightSpan>();
        for (var d = 0; d < 3; d++)
        {
            var day = SampleDay.Today.AddDays(d);
            var (rise, set) = SamplePlace.Sun(day);
            if (rise is { } r && set is { } s)
            {
                spans.Add(new DaylightSpan(SampleDay.At(day, summer ? new TimeOnly(5, 40) : r, TimeZoneInfo.Local), SampleDay.At(day, summer ? new TimeOnly(21, 15) : s, TimeZoneInfo.Local)));
            }
        }

        return new WeatherHours(hours, spans);
    }

    /// <summary>
    /// Each day's low at six and high at three in the afternoon, joined by a half cosine from one to the next, so the
    /// curve runs on through midnight; <paramref name="hour"/> counts from today's midnight.
    /// </summary>
    private static double Diurnal(double hour, IReadOnlyList<ForecastDay> days, bool summer)
    {
        var points = new List<(double At, double Value)>();
        for (var d = -1; d <= days.Count; d++)
        {
            var day = days[Math.Clamp(d, 0, days.Count - 1)];
            points.Add(((d * 24) + 6, summer ? 18.5 + (d * 0.4) : day.Low!.Value));
            points.Add(((d * 24) + 15, summer ? 33.2 : day.High!.Value));
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            var (a, b) = (points[i], points[i + 1]);
            if (hour >= a.At && hour <= b.At)
            {
                var t = (1 - Math.Cos(Math.PI * (hour - a.At) / (b.At - a.At))) / 2;
                return a.Value + ((b.Value - a.Value) * t);
            }
        }

        return points[^1].Value;
    }

    /// <summary>Today's shower from eleven (2.6 mm, as the forecast says), a little rain tomorrow afternoon.</summary>
    private static double Rain(DateTime local, int day) => (day, local.Hour) switch
    {
        (0, 12) => 1.3,
        (0, 13) => 1.3,
        (1, 16) => 0.2,
        _ => 0,
    };

    private static double SummerRain(DateTime local, int day) => (day, local.Hour) switch
    {
        (0, 19) => 3.4,
        (0, 20) => 8.6,
        (0, 21) => 1.1,
        _ => 0,
    };

    private static double Chance(DateTime local, int day) => day switch
    {
        0 => local.Hour is >= 10 and <= 14 ? 75 - (Math.Abs(local.Hour - 12) * 10) : 15,
        1 => local.Hour is >= 14 and <= 17 ? 25 : 5,
        _ => 5,
    };

    /// <summary>The sun's arc from seven to nineteen, at most <paramref name="peak"/> at one o'clock.</summary>
    private static double Uv(int hour, double peak) => Math.Max(0, peak * Math.Sin(Math.PI * (hour - 7) / 12));

    /// <summary>14 km/h now, freshening to a gusty afternoon, calmer after.</summary>
    private static double Wind(int i, bool summer)
        => summer ? 8 + (6 * Math.Sin(i * Math.PI / 12)) : 14 + (9 * (Math.Exp(-Math.Pow(i - 6, 2) / 18) - Math.Exp(-25.0 / 18))) - ((Math.Min(i, 30) - 1) * 0.15);
}
