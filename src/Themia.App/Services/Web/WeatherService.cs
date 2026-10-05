using System.Globalization;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>A place the geocoder matched, with the coordinates to store for it.</summary>
internal sealed record GeocodedPlace(string Name, double Latitude, double Longitude);

/// <summary>
/// open-meteo, which is the reason the two weather widgets could be ported at all: it is free,
/// unauthenticated, and asks for nothing but a coordinate pair.
/// <para>
/// <b>No API key, no account, no token.</b> That is a property of the endpoint and a constraint on
/// this file: if it ever stops being true, the widget stops working and says so, and nothing here
/// quietly substitutes a service that wants a credential.
/// </para>
/// </summary>
internal static class WeatherService
{
    private const string Scope = "weather";

    /// <summary>Conditions now. Documented at <c>open-meteo.com/en/docs</c>.</summary>
    private const string CurrentEndpoint = "https://api.open-meteo.com/v1/forecast";

    /// <summary>Place-name lookup. The same service, a different host.</summary>
    private const string GeocodeEndpoint = "https://geocoding-api.open-meteo.com/v1/search";

    private static readonly WebCache<CurrentWeather> CurrentCache = new(Scope);
    private static readonly WebCache<Forecast> ForecastCache = new(Scope);

    /// <summary>
    /// Conditions at a point. Widgets on the same point, in the same unit, share one request — the
    /// cache key is exactly the set of things that change the answer.
    /// </summary>
    /// <param name="force">[S-730] The user's "Refresh": ask the network even if the answer is young.</param>
    public static async Task<Fetched<CurrentWeather>> CurrentAsync(
        double latitude,
        double longitude,
        TemperatureUnit unit,
        TimeSpan freshFor,
        CancellationToken token,
        bool force = false)
    {
        var key = CurrentKey(latitude, longitude, unit);
        var loaded = false;

        var result = await CurrentCache.GetAsync(
            key,
            freshFor,
            ct =>
            {
                loaded = true;
                return LoadCurrentAsync(latitude, longitude, unit, ct);
            },
            token,
            force).ConfigureAwait(false);

        if (loaded && result.Status == FetchStatus.Ok)
        {
            SaveSoon();
        }

        return result;
    }

    private static string CurrentKey(double latitude, double longitude, TemperatureUnit unit)
        => $"now|{Coordinate(latitude)},{Coordinate(longitude)}|{unit}";

    /// <summary>The cached conditions for a point regardless of age, for drawing before a fetch returns.</summary>
    public static Fetched<CurrentWeather>? PeekCurrent(double latitude, double longitude, TemperatureUnit unit)
        => CurrentCache.Peek(CurrentKey(latitude, longitude, unit));

    public static async Task<Fetched<Forecast>> ForecastAsync(
        double latitude,
        double longitude,
        TemperatureUnit unit,
        int days,
        TimeSpan freshFor,
        CancellationToken token,
        bool force = false)
    {
        var key = ForecastKey(latitude, longitude, unit, days);
        var loaded = false;

        var result = await ForecastCache.GetAsync(
            key,
            freshFor,
            ct =>
            {
                loaded = true;
                return LoadForecastAsync(latitude, longitude, unit, days, ct);
            },
            token,
            force).ConfigureAwait(false);

        if (loaded && result.Status == FetchStatus.Ok)
        {
            SaveSoon();
        }

        return result;
    }

    public static Fetched<Forecast>? PeekForecast(double latitude, double longitude, TemperatureUnit unit, int days)
        => ForecastCache.Peek(ForecastKey(latitude, longitude, unit, days));

    private static string ForecastKey(double latitude, double longitude, TemperatureUnit unit, int days)
        => $"days|{Coordinate(latitude)},{Coordinate(longitude)}|{unit}|{days}";

    private static async Task<Fetched<CurrentWeather>> LoadCurrentAsync(
        double latitude,
        double longitude,
        TemperatureUnit unit,
        CancellationToken token)
    {
        var uri = new Uri(
            $"{CurrentEndpoint}?latitude={Coordinate(latitude)}&longitude={Coordinate(longitude)}"
            + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code"
            + ",precipitation,pressure_msl,wind_direction_10m,wind_gusts_10m,cloud_cover,uv_index"
            // [S-720] Today and the next five days, and the next 24 hours, in the same request: the
            // larger tiles draw them, and asking once costs the service less than asking twice.
            + "&daily=" + DailyFields
            + "&hourly=temperature_2m,precipitation_probability&forecast_hours=24"
            + "&forecast_days=6"
            + "&timezone=auto"
            + "&wind_speed_unit=kmh"
            + $"&temperature_unit={UnitParameter(unit)}");

        var (status, reply, detail) = await WebFetch
            .GetJsonAsync(uri, WebJsonContext.Default.OpenMeteoReply, token)
            .ConfigureAwait(false);

        var now = DateTimeOffset.Now;

        if (status != FetchStatus.Ok || reply is null)
        {
            return Fetched<CurrentWeather>.Failed(status, detail, now);
        }

        // open-meteo answers a bad request with 400 and a reason, but also carries one on some 200s.
        if (!string.IsNullOrEmpty(reply.Reason))
        {
            return Fetched<CurrentWeather>.Failed(FetchStatus.BadResponse, reply.Reason, now);
        }

        var current = reply.Current;

        if (current is null)
        {
            return Fetched<CurrentWeather>.Failed(FetchStatus.Empty, "no current block", now);
        }

        var reading = new CurrentWeather(
            Sane(current.Temperature, -120, 100),
            Sane(current.ApparentTemperature, -150, 130),
            Sane(current.Humidity, 0, 100),
            Sane(current.WindSpeed, 0, 500),
            Code(current.WeatherCode))
        {
            Precipitation = Sane(current.Precipitation, 0, 500),
            Pressure = Sane(current.Pressure, 850, 1100),
            WindDirection = Sane(current.WindDirection, 0, 360),
            WindGusts = Sane(current.WindGusts, 0, 500),
            CloudCover = Sane(current.CloudCover, 0, 100),
            UvIndex = Sane(current.UvIndex, 0, 30),

            // The extras are extras: a reply whose daily or hourly block is missing or malformed
            // still yields a reading, just without them.
            Days = reply.Daily is { } daily ? ParseDays(daily, 6) : [],
            Hours = ParseHours(reply.Hourly, reply.UtcOffsetSeconds),
        };

        // A "current" block with no temperature in it is not a reading, whatever else it carries.
        return reading.Temperature is null
            ? Fetched<CurrentWeather>.Failed(FetchStatus.Empty, "no temperature in the reply", now)
            : new Fetched<CurrentWeather>(FetchStatus.Ok, reading, null, now);
    }

    private static async Task<Fetched<Forecast>> LoadForecastAsync(
        double latitude,
        double longitude,
        TemperatureUnit unit,
        int days,
        CancellationToken token)
    {
        var wanted = Math.Clamp(days, 1, 14);

        var uri = new Uri(
            $"{CurrentEndpoint}?latitude={Coordinate(latitude)}&longitude={Coordinate(longitude)}"
            + "&daily=" + DailyFields
            + "&wind_speed_unit=kmh"
            + $"&temperature_unit={UnitParameter(unit)}"
            // The days come back in the location's own calendar, which is what makes "Today" mean
            // today there rather than today here.
            + "&timezone=auto"
            + $"&forecast_days={wanted.ToString(CultureInfo.InvariantCulture)}");

        var (status, reply, detail) = await WebFetch
            .GetJsonAsync(uri, WebJsonContext.Default.OpenMeteoReply, token)
            .ConfigureAwait(false);

        var now = DateTimeOffset.Now;

        if (status != FetchStatus.Ok || reply is null)
        {
            return Fetched<Forecast>.Failed(status, detail, now);
        }

        if (!string.IsNullOrEmpty(reply.Reason))
        {
            return Fetched<Forecast>.Failed(FetchStatus.BadResponse, reply.Reason, now);
        }

        var daily = reply.Daily;
        var dates = daily?.Time;

        if (daily is null || dates is null || dates.Count == 0)
        {
            return Fetched<Forecast>.Failed(FetchStatus.Empty, "no daily block", now);
        }

        var parsed = ParseDays(daily, wanted);

        return parsed.Count == 0
            ? Fetched<Forecast>.Failed(FetchStatus.Empty, "no usable days in the reply", now)
            : new Fetched<Forecast>(FetchStatus.Ok, new Forecast(parsed), null, now);
    }

    /// <summary>The daily fields both requests ask for.</summary>
    private const string DailyFields =
        "weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum"
        + ",precipitation_probability_max,wind_speed_10m_max,uv_index_max,sunrise,sunset";

    private static List<ForecastDay> ParseDays(OpenMeteoDaily daily, int wanted)
    {
        var dates = daily.Time ?? [];
        var parsed = new List<ForecastDay>(Math.Min(dates.Count, wanted));

        for (var i = 0; i < dates.Count && parsed.Count < wanted; i++)
        {
            // The arrays are supposed to be the same length. Indexed defensively anyway, because
            // "supposed to be" is not a guarantee and an off-by-one here is an exception on the way
            // to a render.
            if (!DateOnly.TryParseExact(dates[i] ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                continue;
            }

            parsed.Add(new ForecastDay(
                day,
                Code(At(daily.WeatherCode, i)),
                Sane(At(daily.Low, i), -120, 100),
                Sane(At(daily.High, i), -120, 100),
                Sane(At(daily.Precipitation, i), 0, 2000))
            {
                PrecipitationChance = Sane(At(daily.PrecipitationChance, i), 0, 100),
                WindMax = Sane(At(daily.WindMax, i), 0, 500),
                UvMax = Sane(At(daily.UvMax, i), 0, 30),
                Sunrise = WallClock(daily.Sunrise, i),
                Sunset = WallClock(daily.Sunset, i),
            });
        }

        return parsed;
    }

    /// <summary>
    /// The hourly curve, as instants. Dropped whole when the reply has no UTC offset: an hour that
    /// cannot be placed on the timeline would draw the chart against the wrong "now".
    /// </summary>
    private static List<HourlyPoint> ParseHours(OpenMeteoHourly? hourly, int? offsetSeconds)
    {
        var hours = new List<HourlyPoint>(24);

        if (hourly?.Time is not { } times || offsetSeconds is not { } seconds || Math.Abs(seconds) > 14 * 3600)
        {
            return hours;
        }

        var offset = TimeSpan.FromSeconds(seconds);

        for (var i = 0; i < times.Count && hours.Count < 48; i++)
        {
            if (!DateTime.TryParseExact(times[i] ?? string.Empty, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                continue;
            }

            hours.Add(new HourlyPoint(
                new DateTimeOffset(local, offset),
                Sane(At(hourly.Temperature, i), -120, 100),
                Sane(At(hourly.PrecipitationChance, i), 0, 100)));
        }

        return hours;
    }

    /// <summary>The time part of "2026-10-04T07:31", or null for a polar day with no sunrise.</summary>
    private static TimeOnly? WallClock(List<string?>? list, int index)
    {
        var text = list is not null && index < list.Count ? list[index] : null;

        return text is not null
            && DateTime.TryParseExact(text, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
                ? TimeOnly.FromDateTime(at)
                : null;
    }

    // ---- [S-729] persistence -----------------------------------------------------------------

    private static readonly Lock PersistGate = new();
    private static string? _persistPath;
    private static Task _writes = Task.CompletedTask;

    /// <summary>
    /// [S-729] Keep the last good answers in <paramref name="path"/> across restarts: read them back
    /// now (marked restored, so they draw at once as stale and are fetched again), and save after
    /// every fetch that brings a new one. Called once, by the app; a debug render never calls it, so a
    /// render neither reads nor writes the user's file.
    /// </summary>
    public static void Persist(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var (snapshot, outcome) = WeatherCacheStore.Load(path, DateTimeOffset.Now);

        foreach (var entry in snapshot.Current)
        {
            CurrentCache.Restore(entry.Key, entry.Value, entry.At);
        }

        foreach (var entry in snapshot.Forecasts)
        {
            ForecastCache.Restore(entry.Key, entry.Value, entry.At);
        }

        if (outcome == WeatherCacheLoadOutcome.Loaded)
        {
            Log.Info(Scope, $"restored {snapshot.Current.Count} readings and {snapshot.Forecasts.Count} forecasts");
        }

        lock (PersistGate)
        {
            _persistPath = path;
        }
    }

    /// <summary>
    /// Queue a save of both caches. One writer: each save runs after the one before it, off the UI
    /// thread, with the snapshot taken now.
    /// </summary>
    private static void SaveSoon()
    {
        lock (PersistGate)
        {
            if (_persistPath is not { } path)
            {
                return;
            }

            var snapshot = new WeatherCacheSnapshot(
                [.. CurrentCache.Snapshot().Select(pair => new CachedWeather<CurrentWeather>(pair.Key, pair.Entry.At, pair.Entry.Value!))],
                [.. ForecastCache.Snapshot().Select(pair => new CachedWeather<Forecast>(pair.Key, pair.Entry.At, pair.Entry.Value!))]);

            _writes = _writes.ContinueWith(
                _ =>
                {
                    try
                    {
                        WeatherCacheStore.Save(path, snapshot);
                    }
                    catch (Exception ex)
                    {
                        // Save handles the I/O failures it expects; anything here is a bug, logged
                        // rather than left to fault a task nobody observes.
                        Log.Error(Scope, "saving the weather cache threw", ex);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Look a place up by name. Used only from the settings page, on a keystroke the user made, so
    /// it is not cached and not on a timer.
    /// </summary>
    public static async Task<(FetchStatus Status, IReadOnlyList<GeocodedPlace> Places, string? Detail)> FindAsync(
        string query,
        CancellationToken token)
    {
        if (!WebInput.IsPlausiblePlace(query))
        {
            return (FetchStatus.Refused, [], "not a place name");
        }

        var uri = new Uri(
            $"{GeocodeEndpoint}?name={Uri.EscapeDataString(query.Trim())}&count=6&language=en&format=json");

        var (status, reply, detail) = await WebFetch
            .GetJsonAsync(uri, WebJsonContext.Default.GeocodeReply, token)
            .ConfigureAwait(false);

        if (status != FetchStatus.Ok || reply is null)
        {
            return (status, [], detail);
        }

        var places = new List<GeocodedPlace>(6);

        foreach (var hit in reply.Results ?? [])
        {
            // A result with no name or no finite coordinates is dropped rather than drawn: it
            // cannot be shown and it cannot be stored, so offering it would only produce a widget
            // pointed at nowhere.
            if (string.IsNullOrWhiteSpace(hit.Name) || !WebInput.AreCoordinates(hit.Latitude, hit.Longitude))
            {
                continue;
            }

            places.Add(new GeocodedPlace(Describe(hit), hit.Latitude!.Value, hit.Longitude!.Value));
        }

        // The geocoder matching nothing is an answer, not a failure, and the two read very
        // differently to somebody who has just mistyped a city.
        return places.Count == 0
            ? (FetchStatus.Empty, places, null)
            : (FetchStatus.Ok, places, null);
    }

    /// <summary>"Zurich, Canton of Zurich, Switzerland", skipping the parts the service left out.</summary>
    private static string Describe(GeocodeHit hit)
    {
        var parts = new List<string>(3) { hit.Name!.Trim() };

        if (!string.IsNullOrWhiteSpace(hit.Admin1) && !hit.Admin1.Equals(hit.Name, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(hit.Admin1.Trim());
        }

        if (!string.IsNullOrWhiteSpace(hit.Country))
        {
            parts.Add(hit.Country.Trim());
        }

        return string.Join(", ", parts);
    }

    private static string UnitParameter(TemperatureUnit unit)
        => unit == TemperatureUnit.Fahrenheit ? "fahrenheit" : "celsius";

    /// <summary>
    /// A coordinate in the URL, to four decimal places — about eleven metres, which is far finer
    /// than any weather model. Invariant, so a machine that writes decimal commas cannot produce a
    /// URL with a stray separator in it.
    /// </summary>
    private static string Coordinate(double value)
        => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>A reading, if it is a number and inside a range the physical quantity can occupy.</summary>
    private static double? Sane(double? value, double min, double max)
        => value is { } number && double.IsFinite(number) && number >= min && number <= max ? number : null;

    /// <summary>A WMO code, if it is one. Codes run 0 to 99; anything else is not this vocabulary.</summary>
    private static int? Code(int? value) => value is >= 0 and <= 99 ? value : null;

    private static T? At<T>(List<T?>? list, int index) where T : struct
        => list is not null && index < list.Count ? list[index] : null;
}
