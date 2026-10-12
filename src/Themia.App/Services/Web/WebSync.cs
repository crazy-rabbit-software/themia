using Themia.App.Services.Sync;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// [S-2663] The readings, forecasts and quotes every WeatherNow, WeatherForecast and Stocks widget in
/// every view reads, kept fresh in the background: the one place they are fetched, as
/// <see cref="FeedSync"/> is for feeds. The answers live in the shared caches, restored from and saved
/// to the sealed web shelves ([S-2660]); a widget draws <see cref="Reading"/> / <see cref="Forecast"/> /
/// <see cref="Quote"/>, hears <see cref="Updated"/> and never fetches from its tick.
/// <para>
/// UI thread: the ticker and the widgets are there, and each fetch resumes there.
/// </para>
/// </summary>
internal sealed class WebSync : IDisposable
{
    private readonly SyncHost _sync;
    private readonly AnswerSync<CurrentWeather> _readings;
    private readonly AnswerSync<Forecast> _forecasts;
    private readonly AnswerSync<TickerQuote> _quotes;
    private readonly AnswerSync<AirReading> _air;
    private readonly AnswerSync<RainNowcast> _rain;
    private readonly AnswerSync<RateTable> _rates;
    private bool _disposed;

    public WebSync(SyncHost sync, Ticker ticker)
    {
        _sync = sync;
        _readings = new AnswerSync<CurrentWeather>("weather", ticker);
        _forecasts = new AnswerSync<Forecast>("forecast", ticker);
        _quotes = new AnswerSync<TickerQuote>("stocks", ticker);
        _air = new AnswerSync<AirReading>("air", ticker);
        _rain = new AnswerSync<RainNowcast>("rain", ticker);
        _rates = new AnswerSync<RateTable>("rates", ticker);
        _sync.InterestsChanged += Reconcile;
        _sync.NetworkAvailable += Wake;
        Reconcile();
    }

    /// <summary>
    /// Raised on the UI thread with the <see cref="Key(WeatherInterest)"/> or <see cref="Key(QuoteInterest)"/>
    /// of an answer whose value or state changed.
    /// </summary>
    public event Action<string>? Updated
    {
        add
        {
            _readings.Updated += value;
            _forecasts.Updated += value;
            _quotes.Updated += value;
            _air.Updated += value;
            _rain.Updated += value;
            _rates.Updated += value;
        }

        remove
        {
            _readings.Updated -= value;
            _forecasts.Updated -= value;
            _quotes.Updated -= value;
            _air.Updated -= value;
            _rain.Updated -= value;
            _rates.Updated -= value;
        }
    }

    /// <summary>The cache key a weather widget's answer is synced under.</summary>
    public static string Key(WeatherInterest want)
    {
        ArgumentNullException.ThrowIfNull(want);
        return want.ForecastDays is { } days
            ? WeatherService.ForecastKey(want.Latitude, want.Longitude, want.Unit, days)
            : WeatherService.CurrentKey(want.Latitude, want.Longitude, want.Unit);
    }

    public static string Key(QuoteInterest want)
    {
        ArgumentNullException.ThrowIfNull(want);
        return MarketService.Key(want.Symbol, want.Market, want.Range, want.PrePost);
    }

    /// <summary>[S-5101] The key an air widget's answer is synced under.</summary>
    public static string Key(AirInterest want)
    {
        ArgumentNullException.ThrowIfNull(want);
        return OnlineService.AirKey(want.Latitude, want.Longitude);
    }

    /// <summary>[S-5121] The key a rain widget's answer is synced under.</summary>
    public static string Key(RainInterest want)
    {
        ArgumentNullException.ThrowIfNull(want);
        return OnlineService.RainKey(want.Latitude, want.Longitude);
    }

    /// <summary>[S-5147] The one key every exchange-rates widget shares.</summary>
    public static string RatesKey => OnlineService.RatesKey;

    public Fetched<AirReading>? Air(AirInterest want)
        => _air.Answer(Key(want)) ?? OnlineService.PeekAir(want.Latitude, want.Longitude);

    public Fetched<RainNowcast>? Rain(RainInterest want)
        => _rain.Answer(Key(want)) ?? OnlineService.PeekRain(want.Latitude, want.Longitude);

    public Fetched<RateTable>? Rates() => _rates.Answer(RatesKey) ?? OnlineService.PeekRates();

    public void Show(AirInterest want, bool refresh)
    {
        ArgumentNullException.ThrowIfNull(want);
        Go(_air, AirWant(want), refresh);
    }

    public void Show(RainInterest want, bool refresh)
    {
        ArgumentNullException.ThrowIfNull(want);
        Go(_rain, RainWant(want), refresh);
    }

    public void ShowRates(bool refresh) => Go(_rates, RatesWant(new RatesInterest(OnlineCadence.Rates)), refresh);

    private static AnswerWant<AirReading> AirWant(AirInterest a)
        => new(
            Key(a),
            a.Interval,
            (freshFor, force) => OnlineService.AirAsync(a.Latitude, a.Longitude, freshFor, force),
            () => OnlineService.PeekAir(a.Latitude, a.Longitude));

    private static AnswerWant<RainNowcast> RainWant(RainInterest r)
        => new(
            Key(r),
            r.Interval,
            (freshFor, force) => OnlineService.RainAsync(r.Latitude, r.Longitude, freshFor, force),
            () => OnlineService.PeekRain(r.Latitude, r.Longitude));

    private static AnswerWant<RateTable> RatesWant(RatesInterest r)
        => new(RatesKey, r.Interval, (freshFor, force) => OnlineService.RatesAsync(freshFor, force), OnlineService.PeekRates);

    /// <summary>
    /// What a widget draws: the newest good answer (a stored one before the first sync of this run),
    /// annotated with the last failure when that could not improve on it. Never asks.
    /// </summary>
    public Fetched<CurrentWeather>? Reading(WeatherInterest want)
        => _readings.Answer(Key(want)) ?? WeatherService.PeekCurrent(want.Latitude, want.Longitude, want.Unit);

    public Fetched<Forecast>? Forecast(WeatherInterest want)
        => want.ForecastDays is { } days
            ? _forecasts.Answer(Key(want)) ?? WeatherService.PeekForecast(want.Latitude, want.Longitude, want.Unit, days)
            : null;

    public Fetched<TickerQuote>? Quote(QuoteInterest want)
        => _quotes.Answer(Key(want)) ?? MarketService.Peek(want.Symbol, want.Market, want.Range, want.PrePost);

    public bool Busy(WeatherInterest want)
        => want.ForecastDays is null ? _readings.Busy(Key(want)) : _forecasts.Busy(Key(want));

    public bool Busy(QuoteInterest want) => _quotes.Busy(Key(want));

    /// <summary>
    /// A shown widget's answer. Nothing is asked when one is held and fresh (only a place or symbol
    /// never fetched is asked now); <paramref name="refresh"/> is the user's Refresh ([S-730], [S-1311]).
    /// </summary>
    public void Show(WeatherInterest want, bool refresh)
    {
        ArgumentNullException.ThrowIfNull(want);

        if (want.ForecastDays is null)
        {
            Go(_readings, ReadingWant(want), refresh);
        }
        else
        {
            Go(_forecasts, ForecastWant(want), refresh);
        }
    }

    public void Show(QuoteInterest want, bool refresh)
    {
        ArgumentNullException.ThrowIfNull(want);
        Go(_quotes, QuoteWant(want), refresh);
    }

    private static void Go<T>(AnswerSync<T> answers, AnswerWant<T> want, bool refresh)
        where T : class
    {
        if (refresh)
        {
            answers.Refresh([want]);
        }
        else
        {
            answers.Show([want]);
        }
    }

    private static AnswerWant<CurrentWeather> ReadingWant(WeatherInterest w)
        => new(
            Key(w),
            w.Interval,
            (freshFor, force) => WeatherService.CurrentAsync(w.Latitude, w.Longitude, w.Unit, freshFor, CancellationToken.None, force),
            () => WeatherService.PeekCurrent(w.Latitude, w.Longitude, w.Unit));

    private static AnswerWant<Forecast> ForecastWant(WeatherInterest w)
    {
        var days = w.ForecastDays ?? 1;
        return new(
            Key(w),
            w.Interval,
            (freshFor, force) => WeatherService.ForecastAsync(w.Latitude, w.Longitude, w.Unit, days, freshFor, CancellationToken.None, force),
            () => WeatherService.PeekForecast(w.Latitude, w.Longitude, w.Unit, days));
    }

    private static AnswerWant<TickerQuote> QuoteWant(QuoteInterest q)
        => new(
            Key(q),
            q.Interval,
            (freshFor, force) => MarketService.QuoteAsync(q.Symbol, q.Market, q.Range, q.PrePost, freshFor, CancellationToken.None, force),
            () => MarketService.Peek(q.Symbol, q.Market, q.Range, q.PrePost));

    private void Reconcile()
    {
        if (_disposed)
        {
            return;
        }

        var interests = _sync.Interests;
        _readings.Reconcile(interests.Weather.Where(w => w.ForecastDays is null).Select(ReadingWant));
        _forecasts.Reconcile(interests.Weather.Where(w => w.ForecastDays is not null).Select(ForecastWant));
        _quotes.Reconcile(interests.Quotes.Select(QuoteWant));
        _air.Reconcile(interests.Air.Select(AirWant));
        _rain.Reconcile(interests.Rain.Select(RainWant));
        _rates.Reconcile(interests.Rates is { } rates ? [RatesWant(rates)] : []);
    }

    private void Wake()
    {
        _readings.Wake();
        _forecasts.Wake();
        _quotes.Wake();
        _air.Wake();
        _rain.Wake();
        _rates.Wake();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sync.InterestsChanged -= Reconcile;
        _sync.NetworkAvailable -= Wake;
        _readings.Dispose();
        _forecasts.Dispose();
        _quotes.Dispose();
        _air.Dispose();
        _rain.Dispose();
        _rates.Dispose();
    }
}
