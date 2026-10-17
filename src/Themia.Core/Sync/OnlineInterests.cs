using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.Sync;

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

    /// <summary>[S-5101] What an air widget asks for, or null without a place.</summary>
    public static AirInterest? For(AirQualityConfig config, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(config);
        return WebInput.AreCoordinates(config.Latitude, config.Longitude)
            ? new AirInterest(config.Latitude!.Value, config.Longitude!.Value, interval)
            : null;
    }

    /// <summary>[S-5121] What a rain widget asks for, or null without a place.</summary>
    public static RainInterest? For(RainNextHourConfig config, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(config);
        return WebInput.AreCoordinates(config.Latitude, config.Longitude)
            ? new RainInterest(config.Latitude!.Value, config.Longitude!.Value, interval)
            : null;
    }

    /// <summary>The running app's air widget interest ([S-3780]: the cadence in force).</summary>
    public static AirInterest? For(AirQualityConfig config) => For(config, Air(UpdateCadence.Current));

    public static RainInterest? For(RainNextHourConfig config) => For(config, Rain(UpdateCadence.Current));
}
