using Themia.App.Services.Web;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-2663] [S-5101] [S-5121] The open-meteo answers a weather-family widget draws
/// (<see cref="IWidgetHost.Weather"/>): the conditions now, the forecast, the air and the rain at a
/// place. The host's background sync fetches them, the one place they are fetched ([S-2661]); a view
/// tells it what is on screen (<c>Show</c>), draws what it holds and hears <see cref="Updated"/>. It
/// never fetches from its tick.
/// </summary>
public interface IWeatherAnswers
{
    /// <summary>Raised on the UI thread with the key of an answer whose value or state changed.</summary>
    event Action<string>? Updated;

    /// <summary>The key a weather or forecast answer is synced under (what <see cref="Updated"/> names).</summary>
    string Key(WeatherInterest want);

    /// <summary>[S-5101] The key an air answer is synced under.</summary>
    string Key(AirInterest want);

    /// <summary>[S-5121] The key a rain answer is synced under.</summary>
    string Key(RainInterest want);

    /// <summary>
    /// What a widget draws: the newest good answer (a stored one before the first sync of this run),
    /// annotated with the last failure when that could not improve on it. Never asks.
    /// </summary>
    Fetched<CurrentWeather>? Reading(WeatherInterest want);

    /// <summary>The forecast for a want with a day count; null for one without.</summary>
    Fetched<Forecast>? Forecast(WeatherInterest want);

    Fetched<AirReading>? Air(AirInterest want);

    Fetched<RainNowcast>? Rain(RainInterest want);

    /// <summary>
    /// A shown widget's answer. Nothing is asked when one is held and fresh (only a place never
    /// fetched is asked now); <paramref name="refresh"/> is the user's Refresh ([S-730]).
    /// </summary>
    void Show(WeatherInterest want, bool refresh);

    void Show(AirInterest want, bool refresh);

    void Show(RainInterest want, bool refresh);
}

/// <summary>
/// [S-2663] The quotes a Stocks widget draws (<see cref="IWidgetHost.Quotes"/>), kept fresh by the
/// host's background sync as <see cref="IWeatherAnswers"/> are.
/// </summary>
public interface IQuoteAnswers
{
    /// <summary>Raised on the UI thread with the key of an answer whose value or state changed.</summary>
    event Action<string>? Updated;

    /// <summary>The key a quote is synced under (what <see cref="Updated"/> names).</summary>
    string Key(QuoteInterest want);

    /// <summary>What a widget draws for its instrument and chart; never asks.</summary>
    Fetched<TickerQuote>? Quote(QuoteInterest want);

    /// <summary>A shown widget's quote; <paramref name="refresh"/> is the user's Refresh ([S-1311]).</summary>
    void Show(QuoteInterest want, bool refresh);
}

/// <summary>
/// [S-5147] The ECB's reference rates every exchange-rate widget shares (<see cref="IWidgetHost.Rates"/>):
/// one table, one key, kept fresh by the host's background sync.
/// </summary>
public interface IRateAnswers
{
    /// <summary>Raised on the UI thread with the key of an answer whose value or state changed.</summary>
    event Action<string>? Updated;

    /// <summary>The one key the table is synced under.</summary>
    string Key { get; }

    /// <summary>What a widget draws: the newest table; never asks.</summary>
    Fetched<RateTable>? Rates();

    /// <summary>A shown widget's table; <paramref name="refresh"/> is the user's Refresh.</summary>
    void Show(bool refresh);
}
