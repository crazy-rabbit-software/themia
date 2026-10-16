using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The next few days, from open-meteo's daily block, in one of four styles ([S-6163]): a Windows 11
/// list of rows, the days side by side as columns, the highs and lows as a temperature curve, or the
/// days as cells in a grid shaped to the tile ([S-6171]).
/// <para>
/// Each style has its own ladder (<see cref="WeatherForecastLayout"/>, <see cref="ForecastCurve"/>):
/// rows fill the tile with as many whole days as have their floor, and gain the sky, a range bar,
/// the precipitation and its chance as the width allows; the room left grows the day's parts. The
/// user's day count is the cap: days are never invented to fill space, and none is a sliver.
/// </para>
/// <para>
/// The bar on each day is scaled against the whole forecast rather than that day's own range: a
/// week that runs 12° to 14° every day should draw seven short marks in the same place.
/// </para>
/// </summary>
internal sealed partial class WeatherForecastView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions
{
    private const string Scope = "forecast";

    private readonly TextBlock _freshness = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly Grid _body = new();
    private readonly StackPanel _emptyState;
    private readonly TextBlock _emptyMessage = new();

    private readonly List<IDisposable> _hovers = [];

    private readonly Brush _freshBrush = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _staleBrush = FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Goldenrod);
    // [S-837] The range bar's track is a slider's (SliderTrackFill), not the disabled fill: the item
    // layer's track ([S-6513]). Today's column or row is a selected chip of it.
    private readonly Brush _barBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);

    /// <summary>[S-6172] The now dot's fill: the colour text takes on the accent, so it stands on the bar's fill.</summary>
    private readonly Brush _nowFill = FluentLayout.Themed("TextOnAccentFillColorPrimaryBrush", Colors.White);

    /// <summary>The now dot's accent ring.</summary>
    private const double NowRing = 2;


    private Ticker? _ticker;
    private WeatherForecastConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the settings page edits the live config object in place.
    /// </summary>
    private WeatherForecastConfig _applied = new();

    /// <summary>The card this view draws on, which owns the header it names.</summary>
    private IWidgetSurface? _surface;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private WebSync? _web;

    /// <summary>What the widget asks for now, or null without a place ([S-2663]).</summary>
    private WeatherInterest? _want;

    /// <summary>The state to say when there is no forecast: not set up, or how the last sync went.</summary>
    private FetchStatus _status = FetchStatus.Loading;

    private Fetched<Forecast>? _forecast;

    /// <summary>
    /// [S-6172] The temperature now, from the current-conditions reading already held for the same
    /// place and unit (asked for by a current-conditions widget, or restored); never asked for here.
    /// </summary>
    private double? _nowTemperature;
    private WidgetArea _area;

    /// <summary>What is on screen: the style, its plan and the day the labels were computed for.</summary>
    private (ForecastStyle Style, object Plan, DateOnly Today, bool Precipitation, double? Now)? _built;
    private bool _detached;

    public WeatherForecastView()
    {
        _emptyState = Fluent.EmptyState(Glyphs.Forecast, _emptyMessage, out _);
        _emptyState.Visibility = Visibility.Collapsed;

        Children.Add(_body);
        Children.Add(_emptyState);
    }

    /// <summary>
    /// The age of the forecast, in the card's header. Built in the constructor because the card
    /// reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _freshness;

    /// <summary>
    /// [S-730] Refresh. The style is chosen on the properties strip and the settings page ([S-6007]).
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: true))] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        // [S-2663] The forecast comes from the background sync; the widget only hears it.
        _web = AppServices.WebSync;
        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web.Updated += OnWebUpdated;
        }

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

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as WeatherForecastConfig ?? new WeatherForecastConfig();
        var previous = _applied;
        _config = next;
        _applied = (WeatherForecastConfig)next.Clone();
        PublishTitle();

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

        _want = SyncInterests.For(_config);
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
        if (!_detached && _want is { } want && (WebSync.Key(want) == key || WebSync.Key(want with { ForecastDays = null }) == key))
        {
            Take();
        }
    }

    /// <summary>What the sync holds: the newest forecast (kept through a failure, annotated with it), or the state to say.</summary>
    private void Take()
    {
        if (_want is not { ForecastDays: { } days } want)
        {
            return;
        }

        var answer = _web is { } web ? web.Forecast(want) : WeatherService.PeekForecast(want.Latitude, want.Longitude, want.Unit, days);
        _forecast = answer?.Value is not null ? answer : null;
        _status = answer?.Status ?? FetchStatus.Loading;

        // [S-6172] Read, never asked: Reading() only returns what the sync or the shelf holds.
        var reading = _web?.Reading(want with { ForecastDays = null });
        _nowTemperature = reading is { Value: { } current } ? ForecastNow.Temperature(current.Temperature, reading.At, DateTimeOffset.Now) : null;
        Render();
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var held = _forecast;
        var days = held?.Value?.Days;

        if (days is null || days.Count == 0)
        {
            var status = _status;

            // No place yet: the card's Set up button covers this ([S-1862]).
            _emptyMessage.Text = status == FetchStatus.NotConfigured
                ? string.Empty
                : FetchState.Message(status);

            _emptyState.Visibility = Visibility.Visible;
            _body.Visibility = Visibility.Collapsed;
            _freshness.Text = string.Empty;
            ClearBody();
            return;
        }

        _emptyState.Visibility = Visibility.Collapsed;
        _body.Visibility = Visibility.Visible;
        UpdateFreshness(held!, now);

        // New data always rebuilds; a tick with the same data rebuilds only when the day changed.
        _built = null;
        Lay();
    }

    private void UpdateFreshness(Fetched<Forecast> held, DateTimeOffset now)
    {
        var failed = FetchState.IsFailure(held.Status);
        var stale = failed || held.Restored || FetchState.IsStale(held.At, now, _config.TickInterval);
        var age = FetchState.Freshness(held.At, now);

        _freshness.Text = age;
        _freshness.Foreground = stale ? _staleBrush : _freshBrush;

        var inline = WeatherNowView.InlineAge(age);
        ToolTipService.SetToolTip(
            _freshness,
            failed ? Str.WidgetsC_Weather_Tooltip_ForecastFailed.Format(("problem", FetchState.Message(held.Status)), ("age", inline))
            : held.Restored ? Str.WidgetsC_Weather_Tooltip_ForecastSaved.Format(("age", inline))
            : Str.WidgetsC_Weather_Tooltip_Updated.Format(("age", inline)));
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
            ForecastStyle.Columns => WeatherForecastLayout.Columns(_area, days.Count, _config.ShowPrecipitation),
            ForecastStyle.Curve => ForecastCurve.Plan(_area, days.Count, _config.ShowPrecipitation),
            ForecastStyle.Grid => ForecastGrid.Plan(_area, days.Count),
            _ => WeatherForecastLayout.Rows(_area, days.Count, _config.ShowPrecipitation),
        };

        // One clock reading for the whole build, so a forecast drawn across midnight cannot label
        // two different days "Today".
        var today = DateOnly.FromDateTime(DateTime.Now);
        var key = (style, plan, today, _config.ShowPrecipitation, _nowTemperature);

        if (_built is { } built && built.Style == key.style && Equals(built.Plan, key.plan)
            && built.Today == key.today && built.Precipitation == key.ShowPrecipitation && built.Now == key._nowTemperature)
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
    private static string LookGlyph(WeatherLook look) => WeatherNowView.LookGlyph(look);

    /// <summary>
    /// Drop the layout and, with it, its hover handlers: a handler left hooked keeps the row it
    /// lights alive and re-lights it after the list has been redrawn.
    /// </summary>
    private void ClearBody()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
        _body.Children.Clear();
        _built = null;
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

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
    private void PublishTitle() => _surface?.SetHeaderTitle(_config.Title);
}
