using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.Metrics;

/// <summary>
/// [S-16307] Where the face's data comes from: the host's answers, only the one its metric reads, followed while the card is
/// attached. The hub's series is watched (a point a period; the watch hands the history so far at once); a place's hours and
/// days, its air, a quote and the rates are the background sync's answers (<see cref="SkyFeed{TWant, T}"/>: told what is shown,
/// heard when they change, never fetched here). The view redraws on each, and every five minutes so "now" moves on.
/// </summary>
internal sealed partial class MetricsView
{
    /// <summary>How often the graph is drawn again with nothing new: the hour under way moves on, never faster than it does.</summary>
    private static readonly TimeSpan Redraw = TimeSpan.FromMinutes(5);

    private Ticker? _ticker;
    private ISeriesWatch? _watch;
    private SeriesInterest? _watched;
    private SkyFeed<HoursInterest, WeatherHours> _hours = null!;
    private SkyFeed<WeatherInterest, Forecast> _days = null!;
    private SkyFeed<AirHoursInterest, AirHours> _air = null!;
    private SkyFeed<QuoteInterest, TickerQuote> _quote = null!;
    private SkyFeed<RatesInterest, RateTable> _rates = null!;

    /// <summary>The source of the feed being followed, or null for none (detached).</summary>
    private MetricSource? _following;

    private void Feeds(IWidgetHost host)
    {
        var weather = host.Weather;
        _hours = new(() => new(h => weather.Updated += h, h => weather.Updated -= h, weather.Key, weather.Hours, weather.Show),
            () => _config.HoursInterest(_config.TickInterval), Render);

        // [S-2663] The days come with the reading: heard under its key too.
        _days = new(() => new(h => weather.Updated += h, h => weather.Updated -= h, weather.Key, weather.Forecast, weather.Show)
            {
                Hears = (want, key) => key == weather.Key(want) || key == weather.Key(want with { ForecastDays = null }),
            },
            () => _config.DaysInterest(RegionFormat.Current.Temperature, _config.TickInterval), Render);
        _air = new(() => new(h => weather.Updated += h, h => weather.Updated -= h, weather.Key, weather.AirHours, weather.Show),
            () => _config.AirInterest(OnlineCadence.Air(UpdateCadence.Current)), Render);
        _quote = new(() => new(h => host.Quotes.Updated += h, h => host.Quotes.Updated -= h, host.Quotes.Key, host.Quotes.Quote, host.Quotes.Show),
            () => _config.QuoteInterest(UpdateCadence.Interval(HostCadences.Markets)), Render);
        _rates = new(() => SkyFeeds.Rates(host.Rates), () => new RatesInterest(OnlineCadence.Rates), Render);
    }

    /// <summary>[S-16307] Follow what the config's metric reads, and let go of anything else.</summary>
    private void Follow()
    {
        if (_detached || _ticker is not { } ticker)
        {
            return;
        }

        var source = _config.Feed.Source;
        if (_following != source)
        {
            Unfollow();
            _following = source;
            if (Sky(source) is { } feed)
            {
                Attach(feed, ticker);
            }
        }

        switch (Sky(source))
        {
            case { } feed:
                Reload(feed);
                break;
            default:
                Rewatch();
                break;
        }
    }

    /// <summary>Let go of every source.</summary>
    private void Unfollow()
    {
        _watch?.Dispose();
        _watch = null;
        _watched = null;
        _hours.Detach();
        _days.Detach();
        _air.Detach();
        _quote.Detach();
        _rates.Detach();
        _following = null;
    }

    /// <summary>The answer feed of a source, null for the hub's.</summary>
    private object? Sky(MetricSource source) => source switch
    {
        MetricSource.Hours => _hours,
        MetricSource.Days => _days,
        MetricSource.AirHours => _air,
        MetricSource.Quote => _quote,
        MetricSource.Rates => _rates,
        _ => null,
    };

    private static void Attach(object feed, Ticker ticker)
    {
        switch (feed)
        {
            case SkyFeed<HoursInterest, WeatherHours> f: f.Attach(ticker, Redraw); break;
            case SkyFeed<WeatherInterest, Forecast> f: f.Attach(ticker, Redraw); break;
            case SkyFeed<AirHoursInterest, AirHours> f: f.Attach(ticker, Redraw); break;
            case SkyFeed<QuoteInterest, TickerQuote> f: f.Attach(ticker, Redraw); break;
            case SkyFeed<RatesInterest, RateTable> f: f.Attach(ticker, Redraw); break;
        }
    }

    private static void Reload(object feed, bool force = false)
    {
        switch (feed)
        {
            case SkyFeed<HoursInterest, WeatherHours> f: f.Reload(force); break;
            case SkyFeed<WeatherInterest, Forecast> f: f.Reload(force); break;
            case SkyFeed<AirHoursInterest, AirHours> f: f.Reload(force); break;
            case SkyFeed<QuoteInterest, TickerQuote> f: f.Reload(force); break;
            case SkyFeed<RatesInterest, RateTable> f: f.Reload(force); break;
        }
    }

    /// <summary>[S-2504] Watch the series the config names, only when it names another (a new drive or host is another history).</summary>
    private void Rewatch()
    {
        var wanted = _config.Series(SystemDrive.Root);
        if (_watch is not null && wanted == _watched)
        {
            return;
        }

        _watch?.Dispose();
        _watch = null;
        _watched = wanted;
        if (wanted is { } series && _host.System is { } hub)
        {
            _watch = hub.WatchSeries(series, _ => Render());
        }
    }

    /// <summary>[S-730] The Refresh the metric's source has: the hub's read now, a sync's answer asked again.</summary>
    private (string Label, Action Run)? Refresher() => _following switch
    {
        MetricSource.Hub when _watch is { } watch => (Str.Common_Refresh.Text(), watch.Refresh),
        MetricSource.Hours when _config.HasLocation => (OnlineParts.RefreshLabel(_hours.Held), _hours.Refresh),
        MetricSource.Days when _config.HasLocation => (OnlineParts.RefreshLabel(_days.Held), _days.Refresh),
        MetricSource.AirHours when _config.HasLocation => (OnlineParts.RefreshLabel(_air.Held), _air.Refresh),
        MetricSource.Quote when _quote.Want is not null => (OnlineParts.RefreshLabel(_quote.Held), _quote.Refresh),
        MetricSource.Rates => (OnlineParts.RefreshLabel(_rates.Held), _rates.Refresh),
        _ => null,
    };

    /// <summary>
    /// [S-11850] [S-16309] What a sample or a preview state hands the face in place of its source's answer (the hub's series
    /// is the live machine's: a picture of the empty, stale or another metric's face needs its own), with the problems its
    /// status says; null: the source's own.
    /// </summary>
    private (MetricSeries Series, IReadOnlyList<string> Problems, string Currency)? _shown;

    /// <summary>
    /// [S-16311] [S-16327] The metric's series now, through its source's adapter: the data held (none yet: the span's empty
    /// slots, so the graph still stands, WR-10), and why in the status's compact mark: a refresh failed over an answer kept,
    /// no answer and why (offline, refused; still loading: its busy ring), the hub's reason for no reading.
    /// </summary>
    private MetricSeries Read(DateTimeOffset now)
    {
        if (_shown is { } shown)
        {
            _currency = shown.Currency;
            Say(shown.Problems, busy: false);
            return shown.Series;
        }

        var def = _config.Def;
        var feed = _config.Feed;
        var span = _config.SpanInForce;
        var unit = RegionFormat.Current.Temperature;
        _currency = string.Empty;
        IReadOnlyList<string> problems = [];
        MetricSeries? series = null;

        switch (feed.Source)
        {
            case MetricSource.Hub:
                var period = SystemCadence.PeriodSeconds(feed.Hub ?? SystemSource.Cpu);
                var history = _watch is { } watch ? watch.History(MetricAdapters.HubSlots(span, period)) : [];
                series = MetricAdapters.Hub(def, history, Reading(def, _watch?.Latest), span, period, now);
                problems = _watch?.Latest is { Available: false, Reason: { Length: > 0 } reason } ? [reason] : [];
                break;
            case MetricSource.Hours when _hours.Held?.Value is { } hours:
                series = MetricAdapters.Hours(def, feed, hours.Hours, h => h.At, feed.Hour!, feed.HourSecond, span, now, unit);
                problems = OnlineParts.Problems(_hours.Held);
                break;
            case MetricSource.Days when _days.Held?.Value is { Days.Count: > 0 } forecast:
                series = MetricAdapters.Days(feed, forecast.Days, span, forecast.Days[0].Day, now.Offset);
                problems = OnlineParts.Problems(_days.Held);
                break;
            case MetricSource.AirHours when _air.Held?.Value is { } air:
                series = MetricAdapters.Hours(def, feed, air.Hours, h => h.At, feed.Air!, null, span, now, unit);
                problems = OnlineParts.Problems(_air.Held);
                break;
            case MetricSource.Quote when _quote.Held?.Value is { } quote:
                series = MetricAdapters.Quote(quote, now.Offset);
                _currency = quote.Currency;
                problems = OnlineParts.Problems(_quote.Held);
                break;
            case MetricSource.Rates when _rates.Held?.Value is { } table:
                var pair = _config.Pair();
                series = MetricAdapters.Rates(table, pair.EffectiveBase, pair.EffectiveQuote, span, now.Offset);
                problems = OnlineParts.Problems(_rates.Held);
                break;
        }

        var busy = false;
        if (series is null && Status(feed.Source) is { } status)
        {
            // No answer to draw: the face stays (its default graph, "--") and the mark says why.
            busy = status == FetchStatus.Loading;
            problems = busy || status == FetchStatus.NotConfigured ? [] : OnlineParts.Failure(status);
            _whyStatus = status;
        }

        Say(problems, busy);

        return series ?? Empty(feed, span, now);
    }

    /// <summary>The state of a sync's answer while it has none to draw; null for the hub's.</summary>
    private FetchStatus? Status(MetricSource source) => source switch
    {
        MetricSource.Hours => _hours.Status,
        MetricSource.Days => _days.Status,
        MetricSource.AirHours => _air.Status,
        MetricSource.Quote => _quote.Status,
        MetricSource.Rates => _rates.Status,
        _ => null,
    };

    /// <summary>[S-16327] What the status's compact line says where the face has no reading: a short word (the tooltip says all).</summary>
    private string? _why;

    /// <summary>[S-16327] The problems the status says (the value face's own mark says them in its tooltip).</summary>
    private IReadOnlyList<string> _problems = [];

    /// <summary>[S-16327] The state the status's compact line names, set with its problems.</summary>
    private FetchStatus? _whyStatus;

    /// <summary>[S-16327] The status's mark: the problems' glyph, the busy ring, or nothing.</summary>
    private void Say(IReadOnlyList<string> problems, bool busy)
    {
        _why = problems.Count > 0 ? MetricFormat.Why(_whyStatus) : null;
        _problems = problems;
        _whyStatus = null;
        if (problems.Count > 0 || busy)
        {
            _status.Show(problems, busy);
        }
        else
        {
            _status.Clear();
        }
    }

    /// <summary>[S-16311] The span's slots with nothing in them yet.</summary>
    internal static MetricSeries Empty(MetricFeed feed, MetricSpan span, DateTimeOffset now)
    {
        var step = span.Step switch
        {
            MetricStep.Hour => TimeSpan.FromHours(1),
            MetricStep.Day => TimeSpan.FromDays(1),
            _ => TimeSpan.FromSeconds(SystemCadence.PeriodSeconds(feed.Hub ?? SystemSource.Cpu)),
        };
        var count = span.Step == MetricStep.Live ? MetricAdapters.HubSlots(span, (int)step.TotalSeconds) : span.Count;
        return MetricSeries.Empty(feed.Shape, count, MetricAdapters.WindowStart(span, MetricAdapters.HourOf(now), step), step);
    }

    /// <summary>[S-16311] The hub's reading as a value: a share's fraction, else its value (a pair's second too); none while it is unavailable.</summary>
    internal static MetricValue? Reading(MetricDef def, MeterReading? reading)
    {
        if (reading is not { Available: true } r)
        {
            return null;
        }

        var value = def.Unit == MetricUnit.Share ? r.Fraction : r.Value ?? r.Fraction;
        return value is { } v && double.IsFinite(v)
            ? new MetricValue(v, def.Feed(MetricStep.Live).Shape == MetricShape.Pair && r.Second is { } s && double.IsFinite(s) ? s : null)
            : null;
    }
}
