using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13901] [S-14601] One weather reading at the card's place on the single metric's face (<see cref="MetricView"/>).
/// The reading comes from the background sync through <see cref="SkyFeed{TWant, T}"/>, the same answer the weather summary's sky and condition
/// (<c>weatherSky</c>, <c>weatherCondition</c>) draw; a trend over the service's hours reads the place's hours (the weather graphs' answer),
/// this hour against the last for the arrow and the hours from the last on for the line ([S-14603]). A reading kept
/// through a failed refresh stays and the status's problem glyph says so ([S-7042]); with none, the row stays with
/// "--" and its tooltip says why ([S-13902]).
/// </summary>
internal sealed partial class WeatherFactView : MetricView
{
    private readonly WeatherFactKind _fact;
    private readonly SkyFeed<WeatherInterest, CurrentWeather> _feed;
    private readonly SkyFeed<HoursInterest, WeatherHours> _hours;
    private WeatherFactConfig _config;

    /// <summary>The moment the hour under way is found at: now, or the sample moment in a picture.</summary>
    private Func<DateTimeOffset> _clock = () => DateTimeOffset.Now;

    public WeatherFactView(IWidgetHost host, WeatherFactKind fact)
        : base(host, WeatherFactKinds.Of(fact))
    {
        _fact = fact;
        _config = (WeatherFactConfig)WeatherFactKinds.Of(fact).Create();
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Reading, host.Weather.Show),
            () => WeatherInterests.For(_config),
            Render);
        _hours = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Hours, host.Weather.Show),
            () => _config.HoursInterest(_config.TickInterval),
            Render);
    }

    protected override string MetricName => WeatherFactRules.Name(_fact, Strings.Current);

    /// <summary>[S-730] "Refresh": the network now.</summary>
    protected override IReadOnlyList<WidgetAction> OwnActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), Refresh)] : [];

    protected override void OnAttach(Ticker ticker, WidgetConfig config)
    {
        _config = Own(config);
        _feed.Attach(ticker, _config.TickInterval);
        _hours.Attach(ticker, _config.TickInterval);
        OnApply(config);
    }

    protected override void OnApply(WidgetConfig config)
    {
        var own = Own(config);
        if (!SamePlace(own, _config))
        {
            Restart();
        }

        _config = own;
        _feed.Cadence(_config.TickInterval);
        _hours.Cadence(_config.TickInterval);
        _feed.Reload();
        _hours.Reload();
    }

    protected override void OnDetach()
    {
        _feed.Detach();
        _hours.Detach();
    }

    private void Refresh()
    {
        _feed.Refresh();
        if (_config.ReadsHours)
        {
            _hours.Refresh();
        }
    }

    /// <summary>The card's config, or its kind's default for one that is not (never another fact's).</summary>
    private WeatherFactConfig Own(WidgetConfig config)
        => config is WeatherFactConfig own && own.Fact == _fact ? own : (WeatherFactConfig)WeatherFactKinds.Of(_fact).Create();

    private static bool SamePlace(WeatherConfig a, WeatherConfig b) => a.Latitude == b.Latitude && a.Longitude == b.Longitude;

    /// <summary>The reading on the row, or its default without one; the trend over the hours when it draws one.</summary>
    private void Render()
    {
        if (_feed.Held is not { Value: { } reading } held)
        {
            // [S-13902] The row stays, "--", the reason its tooltip and the readout's line ([S-15710]); [S-15711] a failure
            // the status glyph's, as a stale reading's.
            Present(new MetricShown(MetricValue.Unread) { Why = OnlineParts.Caption(_feed.Status), Problems = OnlineParts.Failure(_feed.Status) });
            return;
        }

        // [S-15513] A day from today: that day's reading, its name and its sky; no trend over the hours under way.
        var day = _config.TakesDay ? _config.Day : 0;
        var locale = Strings.Current;
        var value = WeatherFactRules.Metric(locale, _fact, reading, _feed.Want?.Unit ?? RegionFormat.Current.Temperature, day);
        var shown = new MetricShown(value)
        {
            Reading = held.At.UtcTicks,
            Problems = OnlineParts.Problems(held),
            Name = WeatherFactRules.DayRowName(locale, _fact, reading, day),
        };
        if (day == 0 && History == MetricHistory.Series && _hours.Held is { Value: { } hours })
        {
            // [S-14711] The graph's window: the hours its span holds.
            var span = MetricGraphs.EffectiveSpan(_config);
            var (direction, series) = MetricRules.Hours(hours.Hours, h => h.At, h => WeatherFactRules.Hourly(_fact, h), WeatherFactRules.Digits(_fact), _clock(), span);
            shown = shown with { Direction = direction, Series = series, Slots = span, Figure = WeatherFactRules.Figure(Strings.Current, _fact, _feed.Want?.Unit ?? RegionFormat.Current.Temperature) };
        }

        Present(shown);
    }
}
