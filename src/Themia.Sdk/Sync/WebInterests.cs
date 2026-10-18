using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.Sync;

/// <summary>[S-2663] A weather widget's place and scale.</summary>
/// <param name="ForecastDays">The forecast's day count, or null for the conditions now (WeatherNow).</param>
public sealed record WeatherInterest(double Latitude, double Longitude, TemperatureUnit Unit, int? ForecastDays, TimeSpan Interval);

/// <summary>[S-2661] An Rss widget's feed at its item cap: what the feed sync keeps fresh for it.</summary>
public sealed record FeedInterest(string Url, int MaxItems, TimeSpan Interval);

/// <summary>[S-2663] A Stocks widget's instrument and chart: everything that changes the quote asked for.</summary>
public sealed record QuoteInterest(string Symbol, QuoteMarket Market, QuoteRange Range, bool PrePost, TimeSpan Interval);

/// <summary>[S-5101] An air quality widget's place. The scale is drawn, not asked: one reply carries both indices.</summary>
public sealed record AirInterest(double Latitude, double Longitude, TimeSpan Interval);

/// <summary>[S-5121] A rain widget's place.</summary>
public sealed record RainInterest(double Latitude, double Longitude, TimeSpan Interval);

/// <summary>
/// [S-5147] The exchange-rate widgets' one need: the ECB's 90-day table, whatever the base and the
/// currencies (every pair is computed from it), so every such widget on every view shares one answer.
/// </summary>
public sealed record RatesInterest(TimeSpan Interval);

/// <summary>[S-5101] [S-5121] [S-5147] How often the three newer online sources are asked.</summary>
public static class OnlineCadence
{
    /// <summary>
    /// [S-5101] Air quality never faster than hourly: CAMS publishes hourly values (Europe) and
    /// three-hourly ones (global), so the shared weather cadence applies only when it is slower.
    /// </summary>
    public static TimeSpan Air(RefreshPreferences refresh)
    {
        var weather = UpdateCadence.Interval(refresh, HostCadences.Weather);
        return weather > TimeSpan.FromHours(1) ? weather : TimeSpan.FromHours(1);
    }

    /// <summary>[S-5121] Rain follows the shared weather cadence: the 15-minute series changes as often as the forecast.</summary>
    public static TimeSpan Rain(RefreshPreferences refresh) => UpdateCadence.Interval(refresh, HostCadences.Weather);

    /// <summary>
    /// [S-5147] The ECB publishes once per working day, around 16:00 CET. Asked every three hours, no
    /// setting: at most eight 70 KB requests a day, and a new day's rates show within three hours.
    /// </summary>
    public static TimeSpan Rates { get; } = TimeSpan.FromHours(3);
}

/// <summary>[S-2663] What a weather widget asks the sync for: the one rule the widgets and the sync share.</summary>
public static class WeatherInterests
{
    /// <summary>
    /// [S-2663] What a weather widget asks for, or null when it can ask nothing (no place: the
    /// widget's own "set up" state, never a request). The widget and the sync use this one rule.
    /// </summary>
    public static WeatherInterest? For(WeatherConfig config)
        => For(config, RegionFormat.Current.Temperature, UpdateCadence.Interval(HostCadences.Weather));

    /// <summary>[S-2663] <see cref="For(WeatherConfig)"/> for a given scale and cadence ([S-3770], [S-3780]).</summary>
    public static WeatherInterest? For(WeatherConfig config, TemperatureUnit unit, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!WebInput.AreCoordinates(config.Latitude, config.Longitude))
        {
            return null;
        }

        // [S-5101] [S-5121] Only a widget that reads the weather feed asks for it; its siblings ask their own.
        if (!config.ReadsWeather)
        {
            return null;
        }

        return new WeatherInterest(config.Latitude!.Value, config.Longitude!.Value, unit, config.ForecastDays, interval);
    }
}
