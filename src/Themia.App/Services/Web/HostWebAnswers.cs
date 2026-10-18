using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Web;

/// <summary>
/// [S-2663] [S-6913] The web answers a widget asks for (<see cref="IWidgetHost.Weather"/>,
/// <see cref="IWidgetHost.Quotes"/>, <see cref="IWidgetHost.Rates"/>): the app's one
/// <see cref="WebSync"/>, or, where it is not running (a debug render), what the shared caches hold.
/// </summary>
internal sealed class HostWeatherAnswers : IWeatherAnswers
{
    public static HostWeatherAnswers Instance { get; } = new();

    private HostWeatherAnswers()
    {
    }

    public event Action<string>? Updated
    {
        add
        {
            if (AppServices.WebSync is { } web)
            {
                web.Updated += value;
            }
        }

        remove
        {
            if (AppServices.WebSync is { } web)
            {
                web.Updated -= value;
            }
        }
    }

    public string Key(WeatherInterest want) => WebSync.Key(want);

    public string Key(AirInterest want) => WebSync.Key(want);

    public string Key(RainInterest want) => WebSync.Key(want);

    public Fetched<CurrentWeather>? Reading(WeatherInterest want)
        => AppServices.WebSync is { } web ? web.Reading(want) : WeatherService.PeekCurrent(want.Latitude, want.Longitude, want.Unit);

    public Fetched<Forecast>? Forecast(WeatherInterest want)
        => AppServices.WebSync is { } web ? web.Forecast(want)
            : want.ForecastDays is { } days ? WeatherService.PeekForecast(want.Latitude, want.Longitude, want.Unit, days) : null;

    public Fetched<AirReading>? Air(AirInterest want)
        => AppServices.WebSync is { } web ? web.Air(want) : OnlineService.PeekAir(want.Latitude, want.Longitude);

    public Fetched<RainNowcast>? Rain(RainInterest want)
        => AppServices.WebSync is { } web ? web.Rain(want) : OnlineService.PeekRain(want.Latitude, want.Longitude);

    public void Show(WeatherInterest want, bool refresh) => AppServices.WebSync?.Show(want, refresh);

    public void Show(AirInterest want, bool refresh) => AppServices.WebSync?.Show(want, refresh);

    public void Show(RainInterest want, bool refresh) => AppServices.WebSync?.Show(want, refresh);
}

/// <summary>[S-2663] The quotes a Stocks widget asks for: the one <see cref="WebSync"/>, else the shared cache.</summary>
internal sealed class HostQuoteAnswers : IQuoteAnswers
{
    public static HostQuoteAnswers Instance { get; } = new();

    private HostQuoteAnswers()
    {
    }

    public event Action<string>? Updated
    {
        add
        {
            if (AppServices.WebSync is { } web)
            {
                web.Updated += value;
            }
        }

        remove
        {
            if (AppServices.WebSync is { } web)
            {
                web.Updated -= value;
            }
        }
    }

    public string Key(QuoteInterest want) => WebSync.Key(want);

    public Fetched<TickerQuote>? Quote(QuoteInterest want)
        => AppServices.WebSync is { } web ? web.Quote(want) : MarketService.Peek(want.Symbol, want.Market, want.Range, want.PrePost);

    public void Show(QuoteInterest want, bool refresh) => AppServices.WebSync?.Show(want, refresh);
}

/// <summary>[S-5147] The ECB table every exchange-rate widget shares: the one <see cref="WebSync"/>, else the shared cache.</summary>
internal sealed class HostRateAnswers : IRateAnswers
{
    public static HostRateAnswers Instance { get; } = new();

    private HostRateAnswers()
    {
    }

    public event Action<string>? Updated
    {
        add
        {
            if (AppServices.WebSync is { } web)
            {
                web.Updated += value;
            }
        }

        remove
        {
            if (AppServices.WebSync is { } web)
            {
                web.Updated -= value;
            }
        }
    }

    public string Key => WebSync.RatesKey;

    public Fetched<RateTable>? Rates() => AppServices.WebSync is { } web ? web.Rates() : OnlineService.PeekRates();

    public void Show(bool refresh) => AppServices.WebSync?.ShowRates(refresh);
}
