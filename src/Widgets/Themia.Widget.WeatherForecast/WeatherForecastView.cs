using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.WeatherForecast;

namespace Themia.App.Widgets;

/// <summary>
/// The next few days, from open-meteo's daily block, in one of four styles ([S-6163]), drawn from the
/// family's blocks: the days as rows (the Row block with its range bar), side by side as columns, the
/// highs and lows as a temperature curve (the Chart block), or as cells in a grid shaped to the tile
/// ([S-6171]).
/// <para>
/// [S-11145] The days are the config's in every style and at every size; each style's plan
/// (<see cref="WeatherForecastLayout"/>, <see cref="ForecastCurve"/>, <see cref="ForecastGrid"/>) only
/// shares the box among them: a bar or a chart longer, a column wider.
/// </para>
/// <para>
/// The bar on each day is scaled against the whole forecast rather than that day's own range: a
/// week that runs 12° to 14° every day should draw seven short marks in the same place.
/// </para>
/// </summary>
internal sealed partial class WeatherForecastView : Grid, IWidgetView, IWidgetOpens, IWidgetHeaderContent,
    IWidgetSizeAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => WeatherForecastModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    private const string Scope = "forecast";

    /// <summary>[S-7064] The header's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitHeader _header;
    private readonly Grid _body = new();

    /// <summary>[S-6734] Why there is no forecast to draw: the Status block's row at the top.</summary>
    private readonly StatusBlock _state = new();


    private Ticker? _ticker;
    private WeatherForecastConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the properties menu edits the live config object in place.
    /// </summary>
    private WeatherForecastConfig _applied = new();

    /// <summary>The card this view draws on, which owns the header it names.</summary>
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private readonly IWidgetHost _host;
    private IWeatherAnswers? _web;

    /// <summary>What the widget asks for now, or null without a place ([S-2663]).</summary>
    private WeatherInterest? _want;

    /// <summary>The state to say when there is no forecast: not set up, or how the last sync went.</summary>
    private FetchStatus _status = FetchStatus.Loading;

    private Fetched<Forecast>? _forecast;
    private WidgetArea _area;

    /// <summary>What is on screen: the style, its plan and the day the labels were computed for.</summary>
    private (ForecastStyle Style, object Plan, DateOnly Today, bool Precipitation)? _built;
    private bool _detached;

    public WeatherForecastView(IWidgetHost host)
    {
        _host = host;
        _header = new KitHeader(host);
        _state.Visibility = Visibility.Collapsed;
        _days = new LinkLights<DateLink>(_links, LinkTopics.HoveredDate, LinkTopics.SelectedDate);

        Children.Add(_body);
        Children.Add(_state);
    }

    /// <summary>
    /// The age of the forecast, in the card's header. Built in the constructor because the card
    /// reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _header;

    /// <summary>
    /// [S-730] Refresh. The style is chosen on the properties strip and its menu ([S-6007]).
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_forecast), () => Reload(force: true))] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        // [S-2663] The forecast comes from the background sync; the widget only hears it.
        _web?.Updated -= OnWebUpdated;
        _web = _host.Weather;
        _web.Updated += OnWebUpdated;
        _links.Join(_surface?.Links);

        ApplyConfig(config);
    }

    /// <summary>The language changed: the days are rebuilt from the forecast in hand.</summary>
    /// <para>
    /// [S-3770] [S-3780] It is also how a change to the shared temperature scale or cadence arrives:
    /// the reading is asked for again under the new scale (the sync holds both, keyed apart).
    /// </para>
    private void OnLanguage(Locale locale)
    {
        Resubscribe();
        Reload(force: false);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as WeatherForecastConfig ?? new WeatherForecastConfig();
        var previous = _applied;
        _config = next;
        _applied = (WeatherForecastConfig)next.Clone();
        PublishPlace();

        // Only the fields that change what comes back make another forecast. Toggling the
        // precipitation column or the style redraws the days already in hand.
        var placeChanged = previous.Latitude != next.Latitude
            || previous.Longitude != next.Longitude
            || previous.EffectiveDays != next.EffectiveDays;

        if (placeChanged)
        {
            _forecast = null;
        }

        // [S-2663] Always through the sync: it asks nothing for a forecast already held and fresh.
        Resubscribe();
        Reload(force: false);
    }

    /// <summary>[S-663] The card's word on how much room the widget has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>Rebuild the ticker subscription only when the cadence actually changed.</summary>
    private void Resubscribe()
    {
        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, OnTick);
    }

    /// <summary>
    /// A tick only redraws: the day labels and the freshness line move on their own ("Today" has to
    /// become "Wed" at midnight). [S-2663] The forecast is kept fresh by the sync service.
    /// </summary>
    private void OnTick()
    {
        if (!_detached)
        {
            Render();
        }
    }

    /// <summary>
    /// [S-2663] Tell the sync what is on screen and draw what it holds. Nothing is fetched for a
    /// forecast already held and fresh; <paramref name="force"/> (the user's Refresh) asks now.
    /// </summary>
    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        _want = WeatherInterests.For(_config);
        if (_want is null)
        {
            _status = FetchStatus.NotConfigured;
            _forecast = null;
            Render();
            return;
        }

        _web?.Show(_want, force);
        Take();
    }

    /// <summary>[S-2663] A forecast's answer or state changed in the sync service.</summary>
    private void OnWebUpdated(string key)
    {
        if (!_detached && _want is { } want && _web is { } web && (web.Key(want) == key || web.Key(want with { ForecastDays = null }) == key))
        {
            Take();
        }
    }

    /// <summary>What the sync holds: the newest forecast (kept through a failure, annotated with it), or the state to say.</summary>
    private void Take()
    {
        if (_want is not { ForecastDays: not null } want)
        {
            return;
        }

        var answer = _web?.Forecast(want);
        _forecast = answer?.Value is not null ? answer : null;
        _status = answer?.Status ?? FetchStatus.Loading;
        Render();
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var held = _forecast;
        var days = held?.Value?.Days;

        if (days is null || days.Count == 0)
        {
            // [S-6734] Not set up is the card's Set up row ([S-1862]); every other state is one row here.
            OnlineParts.ShowState(_state, _status, WeatherForecastModule.Glyph, () => Reload(force: true));
            _body.Visibility = Visibility.Collapsed;
            _header.Clear();
            ClearBody();
            return;
        }

        _state.Hide();
        _body.Visibility = Visibility.Visible;
        _header.Show(OnlineParts.Problems(held), busy: false);

        // New data always rebuilds; a tick with the same data rebuilds only when the day changed.
        _built = null;
        Lay();
    }

    /// <summary>Pick the style's plan for the current area, and rebuild only if it changed.</summary>
    private void Lay()
    {
        if (_detached || _forecast?.Value?.Days is not { Count: > 0 } days || !_area.IsMeasured)
        {
            return;
        }

        var style = _config.Style;
        object plan = style switch
        {
            ForecastStyle.Columns => WeatherForecastLayout.Columns(_area, _config),
            ForecastStyle.Curve => ForecastCurve.Plan(_area, _config),
            ForecastStyle.Grid => ForecastGrid.Plan(_area, _config),
            _ => WeatherForecastLayout.Rows(_area, _config),
        };

        // One clock reading for the whole build, so a forecast drawn across midnight cannot label
        // two different days "Today".
        var today = DateOnly.FromDateTime(DateTime.Now);
        var key = (style, plan, today, _config.ShowPrecipitation);

        if (_built is { } built && built.Style == key.style && Equals(built.Plan, key.plan)
            && built.Today == key.today && built.Precipitation == key.ShowPrecipitation)
        {
            return;
        }

        ClearBody();
        _built = key;

        // The bars scale against the days shown, not the ones left out for room.
        var shown = plan switch
        {
            ForecastRowsPlan r => r.Days,
            ForecastColumnsPlan c => c.Days,
            _ => days.Count,
        };
        var (coldest, warmest) = Extremes([.. days.Take(shown)]);
        var range = new Range(coldest, warmest);

        _body.Children.Add(plan switch
        {
            ForecastColumnsPlan columns => BuildColumns(days, today, range, columns),
            ForecastCurvePlan curve => BuildCurve(days, today, curve),
            ForecastGridPlan cells => BuildGrid(days, today, cells),
            ForecastRowsPlan rows => BuildRows(days, today, range, rows),
            _ => new Grid(),
        });
    }

    /// <summary>The coldest low and warmest high across the forecast, which every bar scales against.</summary>
    private readonly record struct Range(double Coldest, double Warmest);

    private static (double Coldest, double Warmest) Extremes(IReadOnlyList<ForecastDay> days)
    {
        var coldest = double.MaxValue;
        var warmest = double.MinValue;

        foreach (var day in days)
        {
            foreach (var value in new[] { day.Low, day.High })
            {
                if (value is { } v && double.IsFinite(v))
                {
                    coldest = Math.Min(coldest, v);
                    warmest = Math.Max(warmest, v);
                }
            }
        }

        // Every day missing both numbers: the bar spans the row, which RangeBar does for no span.
        return coldest <= warmest ? (coldest, warmest) : (0, 1);
    }

    /// <summary>The same glyph the current-conditions widget draws for a condition. [S-652]</summary>
    private static string LookGlyph(WeatherLook look) => WeatherGlyph.Of(look);

    /// <summary>Drop the layout: the next one is built from the plan.</summary>
    private void ClearBody()
    {
        _body.Children.Clear();
        _days.Clear();
        _built = null;
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        _links.Leave();

        ClearBody();

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web = null;
        }

        _ticker = null;
    }

    /// <summary>[S-728] The city as the header title, or the widget's label when the user hid it.</summary>
    /// <summary>[S-2260] The place is the header's label beside the widget's name; no place, no label ([S-2848]).</summary>
    private void PublishPlace() => _header.SetAbout(_config.PlaceLabel);
}
