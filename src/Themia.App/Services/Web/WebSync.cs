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
    private bool _disposed;

    public WebSync(SyncHost sync, Ticker ticker)
    {
        _sync = sync;
        _readings = new AnswerSync<CurrentWeather>("weather", ticker);
        _forecasts = new AnswerSync<Forecast>("forecast", ticker);
        _quotes = new AnswerSync<TickerQuote>("stocks", ticker);
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
        }

        remove
        {
            _readings.Updated -= value;
            _forecasts.Updated -= value;
            _quotes.Updated -= value;
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
    }

    private void Wake()
    {
        _readings.Wake();
        _forecasts.Wake();
        _quotes.Wake();
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
    }
}
