using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.RainNextHour;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5120] Rain in the next two hours at a place, on the family's tokens: one sentence ("Rain in
/// 15 min") at the content's top, the intensity, total and chance under it, then a column per quarter
/// hour on a fixed scale (the Chart block's columns) with the window's two ends under them ([S-5126]).
/// A dry window, or one too short to speak for the hour, is the Status row's one plain sentence
/// ([S-6714]). The sentence is re-read from the held nowcast every minute, so "in 30 min" counts down
/// between fetches.
/// </summary>
internal sealed partial class RainNextHourView : Grid, IWidgetView, IWidgetOpens, IWidgetHeaderContent,
    IWidgetSizeAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => RainNextHourModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    /// <summary>[S-7064] The header's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitHeader _header;
    private readonly TextBlock _headline = Family.Text(FamilyType.Title);
    private readonly TextBlock _caption = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly StackPanel _top = new();
    private readonly Grid _chart = new() { RowSpacing = RainLayout.LabelGap };
    private readonly Grid _bars = new() { IsHitTestVisible = false };
    private readonly List<ChartBar> _columns = [];
    private readonly TextBlock _timeStart = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly TextBlock _timeEnd = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);

    /// <summary>[S-6734] No nowcast, a dry window or one too short: the Status block's row at the top.</summary>
    private readonly StatusBlock _state = new();

    private Ticker? _ticker;
    private RainNextHourConfig _config = new();
    private RainNextHourConfig _applied = new();
    private IDisposable? _subscription;
    private readonly IWidgetHost _host;
    private IWeatherAnswers? _web;
    private RainInterest? _want;
    private FetchStatus _status = FetchStatus.Loading;
    private Fetched<RainNowcast>? _nowcast;
    private RainOutlook? _outlook;
    private WidgetArea _area;
    private bool _detached;

    /// <summary>Debug renders pin the clock so the fixture's window stays where it was drawn.</summary>
#if DEBUG
    private DateTimeOffset? _pinnedNow;
#else
    // Release has no renders: the field's Debug-only writer (the .Preview.cs part) is not compiled.
    private DateTimeOffset? _pinnedNow => null;
#endif

    public RainNextHourView(IWidgetHost host)
    {
        _host = host;
        _header = new KitHeader(host);

        _top.Margin = Family.SideInset;
        _top.VerticalAlignment = VerticalAlignment.Top;
        _headline.TextWrapping = TextWrapping.WrapWholeWords;
        _top.Children.Add(_headline);
        _top.Children.Add(_caption);

        // [S-1113] Time runs left to right in every language, as on the stock chart.
        _chart.FlowDirection = FlowDirection.LeftToRight;
        _chart.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _chart.VerticalAlignment = VerticalAlignment.Bottom;
        _chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _chart.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < RainRules.WindowSteps; i++)
        {
            _bars.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var column = new ChartBar(vertical: true);
            SetColumn(column, i);
            _columns.Add(column);
            _bars.Children.Add(column);
        }

        _timeEnd.HorizontalAlignment = HorizontalAlignment.Right;
        SetRow(_timeStart, 1);
        SetRow(_timeEnd, 1);
        _chart.Children.Add(_bars);
        _chart.Children.Add(_timeStart);
        _chart.Children.Add(_timeEnd);

        _state.Visibility = Visibility.Collapsed;
        _top.Visibility = Visibility.Collapsed;
        _chart.Visibility = Visibility.Collapsed;
        Children.Add(_top);
        Children.Add(_chart);
        Children.Add(_state);
    }

    public FrameworkElement? HeaderTrailing => _header;

    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_nowcast), () => Reload(force: true))] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        _web?.Updated -= OnWebUpdated;
        _web = _host.Weather;
        _web.Updated += OnWebUpdated;

        // The sentence counts down with the clock; a minute is the finest it changes.
        _subscription?.Dispose();
        _subscription = _ticker.Subscribe(TimeSpan.FromMinutes(1), () =>
        {
            if (!_detached)
            {
                Render();
            }
        });

        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as RainNextHourConfig ?? new RainNextHourConfig();
        var previous = _applied;
        _config = next;
        _applied = (RainNextHourConfig)next.Clone();
        PublishPlace();

        if (previous.Latitude != next.Latitude || previous.Longitude != next.Longitude)
        {
            _nowcast = null;
        }

        Reload(force: false);
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void OnLanguage(Locale locale) => Reload(force: false);

    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        _want = _config.Interest();
        if (_want is null)
        {
            _status = FetchStatus.NotConfigured;
            _nowcast = null;
            Render();
            return;
        }

        _web?.Show(_want, force);
        Take();
    }

    private void OnWebUpdated(string key)
    {
        if (!_detached && _want is { } want && _web is { } web && web.Key(want) == key)
        {
            Take();
        }
    }

    private void Take()
    {
        if (_want is not { } want)
        {
            return;
        }

        var answer = _web?.Rain(want);
        _nowcast = answer?.Value is not null ? answer : null;
        _status = answer?.Status ?? FetchStatus.Loading;
        Render();
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        if (_nowcast is not { Value: { } cast } held)
        {
            // [S-6734] Not set up is the card's Set up row ([S-1862]); every other state is one row here.
            OnlineParts.ShowState(_state, _status, Glyphs.Rain, () => Reload(force: true));
            _top.Visibility = _chart.Visibility = Visibility.Collapsed;
            _header.Clear();
            _outlook = null;
            return;
        }

        var now = _pinnedNow ?? DateTimeOffset.Now;
        var outlook = RainRules.Outlook(cast, now);
        _outlook = outlook;
        _header.Show(OnlineParts.Problems(held), busy: false);

        // [S-5126] Nothing to warn of (a dry window) or nothing to go on (too few steps): one plain sentence.
        if (!RainLayout.Warns(outlook.Headline))
        {
            _state.Show(StatusKind.Empty, Glyphs.Rain, RainText.Headline(outlook));
            _top.Visibility = _chart.Visibility = Visibility.Collapsed;
            return;
        }

        _state.Hide();
        _top.Visibility = Visibility.Visible;
        _headline.Text = RainText.Headline(outlook);
        _caption.Text = RainText.Caption(outlook);

        if (outlook.Steps.Count > 0)
        {
            var start = outlook.Steps[0].Start.ToLocalTime();
            _timeStart.Text = RainNextHourStr.Online_Rain_Now.Text();
            _timeEnd.Text = LocaleFormat.Time(TimeOnly.FromDateTime(start.AddHours(2).DateTime));
        }

        // [S-5125] A column per quarter hour on a fixed scale; a dry one is its empty track.
        for (var i = 0; i < _columns.Count; i++)
        {
            _columns[i].Show(i < outlook.Steps.Count ? RainRules.BarShare(outlook.Steps[i].Millimetres) : 0);
        }

        Lay();
    }

    private void Lay()
    {
        if (_detached || _outlook is not { } outlook || !RainLayout.Warns(outlook.Headline) || !_area.IsMeasured)
        {
            return;
        }

        var plan = RainLayout.Rain(_area, _headline.Text, _caption.Text.Length > 0);
        Family.Step(_headline, plan.Headline);
        _headline.MaxLines = plan.Wraps ? 2 : 1;
        _caption.Visibility = plan.Caption ? Visibility.Visible : Visibility.Collapsed;
        _chart.Visibility = plan.ChartHeight > 0 ? Visibility.Visible : Visibility.Collapsed;
        _chart.Height = plan.ChartHeight > 0 ? plan.ChartHeight : double.NaN;
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;

        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web = null;
        }

        _ticker = null;
    }

    /// <summary>[S-2260] The place is the header's label beside the widget's name; no place, no label ([S-2848]).</summary>
    private void PublishPlace() => _header.SetAbout(_config.PlaceLabel);
}
