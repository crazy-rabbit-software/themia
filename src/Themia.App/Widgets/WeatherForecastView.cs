using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The next few days, from open-meteo's daily block, in one of three layouts ([S-724]): a Windows 11
/// list of rows, the days side by side as columns, or a grid of cards. <c>Auto</c> picks columns on
/// a strip and rows elsewhere; the user's own choice always wins.
/// <para>
/// Each layout has its own ladder (<see cref="WeatherForecastLayout"/>): rows stretch to fill the
/// tile when the days fit ([S-31] of the original) and gain the sky, a range bar, the precipitation
/// and its chance as the width allows; at the largest size a Today panel stands beside them. The
/// user's day count is the cap — days are never invented to fill space.
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
    private readonly Brush _trackBrush = FluentLayout.Themed("ControlStrongFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _barBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly Brush _todayFill = FluentLayout.Themed("SubtleFillColorTertiaryBrush", Colors.Transparent);
    private readonly Brush _cardFill = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Transparent);

    private readonly RefreshSchedule _schedule = new(Scope, "open-meteo");

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
    private CancellationTokenSource? _inFlight;
    private int _generation;

    private Fetched<Forecast>? _forecast;
    private WidgetArea _area;

    /// <summary>What is on screen: the layout, its plan and the day the labels were computed for.</summary>
    private (ForecastLayout Layout, object Plan, DateOnly Today, bool Precipitation)? _built;
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
    /// [S-730] Refresh. [S-1746] The layout is how the widget looks: it is chosen in its settings only.
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), () => { _schedule.Reset(); Reload(force: true); })] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>The language changed: the days are rebuilt from the forecast in hand.</summary>
    private void OnLanguage(Locale locale) => Render();

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

        // Only the fields that change what comes back are worth a re-read. Toggling the
        // precipitation column or the layout redraws the days already in hand.
        var needsFetch = previous.Latitude != next.Latitude
            || previous.Longitude != next.Longitude
            || previous.Unit != next.Unit
            || previous.EffectiveDays != next.EffectiveDays;

        if (needsFetch)
        {
            _forecast = null;
            _schedule.Reset();
        }

        if (Resubscribe())
        {
            return;
        }

        if (needsFetch)
        {
            Reload();
        }
        else
        {
            Render();
        }
    }

    /// <summary>[S-663] The card's word on how much room the widget has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private bool Resubscribe()
    {
        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return false;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, OnTick);
        return _subscription is not null;
    }

    private void OnTick()
    {
        if (_detached)
        {
            return;
        }

        if (_schedule.MayFetch(DateTimeOffset.Now))
        {
            Reload();
            return;
        }

        // Still backed off, but the day labels and the freshness line both move on their own —
        // "Today" has to become "Wed" at midnight whether or not the network is working.
        Render();
    }

    private void Reload(bool force = false)
    {
        if (_detached)
        {
            return;
        }

        if (!WebInput.AreCoordinates(_config.Latitude, _config.Longitude))
        {
            _schedule.Record(FetchStatus.NotConfigured, _config.TickInterval, null, DateTimeOffset.Now);
            Render();
            return;
        }

        var latitude = _config.Latitude!.Value;
        var longitude = _config.Longitude!.Value;
        var unit = _config.Unit;
        var days = _config.EffectiveDays;

        _forecast ??= WeatherService.PeekForecast(latitude, longitude, unit, days);

        _inFlight?.Cancel();
        var cts = new CancellationTokenSource();
        _inFlight = cts;
        var generation = ++_generation;

        Render();
        _ = LoadAsync(latitude, longitude, unit, days, generation, force, cts);
    }

    private async Task LoadAsync(
        double latitude,
        double longitude,
        TemperatureUnit unit,
        int days,
        int generation,
        bool force,
        CancellationTokenSource cts)
    {
        try
        {
            var result = await WeatherService
                .ForecastAsync(latitude, longitude, unit, days, _config.TickInterval, cts.Token, force)
                .ConfigureAwait(true);

            if (_detached || generation != _generation)
            {
                return;
            }

            _schedule.Record(result.Status, _config.TickInterval, result.Detail, DateTimeOffset.Now);

            if (result.Value is not null)
            {
                _forecast = result;
            }
            else if (_forecast is { } held)
            {
                _forecast = held.Stale(result.Status, result.Detail);
            }

            Render();
        }
        catch (OperationCanceledException)
        {
            // ignore: superseded by a newer request, which will draw in this one's place.
        }
        finally
        {
            if (ReferenceEquals(_inFlight, cts))
            {
                _inFlight = null;
            }

            cts.Dispose();
        }
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
            var status = _schedule.Status;

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

    /// <summary>Pick the layout and its plan for the current area, and rebuild only if either changed.</summary>
    private void Lay()
    {
        if (_detached || _forecast?.Value?.Days is not { Count: > 0 } days || !_area.IsMeasured)
        {
            return;
        }

        var layout = WeatherForecastLayout.Resolve(_config.Layout, _area);
        object plan = layout switch
        {
            ForecastLayout.Columns => WeatherForecastLayout.Columns(_area, days.Count),
            ForecastLayout.Grid => WeatherForecastLayout.Grid(_area, days.Count),
            _ => WeatherForecastLayout.Rows(_area, days.Count),
        };

        // One clock reading for the whole build, so a forecast drawn across midnight cannot label
        // two different days "Today".
        var today = DateOnly.FromDateTime(DateTime.Now);
        var key = (layout, plan, today, _config.ShowPrecipitation);

        if (_built is { } built && built.Layout == key.layout && Equals(built.Plan, key.plan)
            && built.Today == key.today && built.Precipitation == key.ShowPrecipitation)
        {
            return;
        }

        ClearBody();
        _built = key;

        var (coldest, warmest) = Extremes(days);
        var range = new Range(coldest, warmest);

        _body.Children.Add(plan switch
        {
            ForecastColumnsPlan columns => BuildColumns(days, today, range, columns),
            ForecastGridPlan grid => BuildGrid(days, today, range, grid),
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

        _inFlight?.Cancel();
        _inFlight = null;

        _ticker = null;
    }

    /// <summary>[S-728] The city as the header title, or the widget's label when the user hid it.</summary>
    private void PublishTitle() => _surface?.SetHeaderTitle(_config.Title);
}
