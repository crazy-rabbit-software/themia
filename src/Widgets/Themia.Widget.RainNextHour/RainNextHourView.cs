using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.RainNextHour;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5120] Rain in the next two hours at a place: one sentence ("Rain in 15 min", "No rain in the
/// next 2 hours") at every size, then the intensity and chance, then a bar per quarter hour on a fixed
/// scale with the times under it ([S-5126]). The sentence is re-read from the held nowcast every
/// minute, so "in 30 min" counts down between fetches.
/// </summary>
internal sealed partial class RainNextHourView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions
{
    private readonly TextBlock _freshness = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly FontIcon _icon = Fluent.Icon(Glyphs.Rain, "TextFillColorSecondaryBrush", Fluent.Space6);
    private readonly TextBlock _headline = Fluent.Text(FluentType.Subtitle);
    private readonly TextBlock _caption = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly Grid _top = new();
    private readonly Grid _chart = new();
    private readonly Canvas _bars = new() { IsHitTestVisible = false };
    private readonly Grid _times = new();
    private readonly TextBlock _timeStart = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly TextBlock _timeMid = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly TextBlock _timeEnd = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly StackPanel _emptyState;
    private readonly TextBlock _emptyMessage = new();

    private Ticker? _ticker;
    private RainNextHourConfig _config = new();
    private RainNextHourConfig _applied = new();
    private IWidgetSurface? _surface;
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
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _top.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _top.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _top.Margin = new Thickness(OnlineLayouts.Inset, OnlineLayouts.VerticalInset, OnlineLayouts.Inset, 0);
        _top.VerticalAlignment = VerticalAlignment.Center;
        _icon.Margin = new Thickness(0, 0, Fluent.Space3, 0);
        _icon.VerticalAlignment = VerticalAlignment.Center;
        _headline.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(_headline, 1);
        SetColumn(_caption, 1);
        SetRow(_caption, 1);
        _top.Children.Add(_icon);
        _top.Children.Add(_headline);
        _top.Children.Add(_caption);

        // [S-1113] Time runs left to right in every language, as on the stock chart.
        _chart.FlowDirection = FlowDirection.LeftToRight;
        _chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _chart.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _chart.Margin = new Thickness(OnlineLayouts.Inset, OnlineLayouts.Gap, OnlineLayouts.Inset, OnlineLayouts.VerticalInset);
        _timeMid.HorizontalAlignment = HorizontalAlignment.Center;
        _timeEnd.HorizontalAlignment = HorizontalAlignment.Right;
        _times.Children.Add(_timeStart);
        _times.Children.Add(_timeMid);
        _times.Children.Add(_timeEnd);
        SetRow(_times, 1);
        _chart.Children.Add(_bars);
        _chart.Children.Add(_times);
        _chart.SizeChanged += OnChartSize;
        SetRow(_chart, 1);

        _emptyState = Fluent.EmptyState(Glyphs.Rain, _emptyMessage, out _);
        SetRowSpan(_emptyState, 2);

        Children.Add(_top);
        Children.Add(_chart);
        Children.Add(_emptyState);
    }

    public FrameworkElement? HeaderTrailing => _freshness;

    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: true))] : [];

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

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as RainNextHourConfig ?? new RainNextHourConfig();
        var previous = _applied;
        _config = next;
        _applied = (RainNextHourConfig)next.Clone();
        PublishTitle();

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
            _emptyMessage.Text = _status == FetchStatus.NotConfigured ? string.Empty : FetchState.Message(_status);
            _emptyState.Visibility = Visibility.Visible;
            _top.Visibility = _chart.Visibility = Visibility.Collapsed;
            _freshness.Text = string.Empty;
            _outlook = null;
            return;
        }

        var now = _pinnedNow ?? DateTimeOffset.Now;
        var outlook = RainRules.Outlook(cast, now);
        _outlook = outlook;

        _emptyState.Visibility = Visibility.Collapsed;
        _top.Visibility = Visibility.Visible;
        _headline.Text = RainText.Headline(outlook);
        _caption.Text = RainText.Caption(outlook);

        var wet = outlook.Headline is RainHeadline.StopsIn or RainHeadline.Continues or RainHeadline.StartsIn;
        _icon.Glyph = outlook.Headline == RainHeadline.Dry ? Glyphs.Cloudy : Glyphs.Rain;
        _icon.Foreground = ThemeBrush.Of(wet ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush", Colors.Gray);

        if (outlook.Steps.Count > 0)
        {
            var start = outlook.Steps[0].Start.ToLocalTime();
            _timeStart.Text = RainNextHourStr.Online_Rain_Now.Text();
            _timeMid.Text = LocaleFormat.Time(TimeOnly.FromDateTime(start.AddHours(1).DateTime));
            _timeEnd.Text = LocaleFormat.Time(TimeOnly.FromDateTime(start.AddHours(2).DateTime));
        }

        OnlineParts.Stamp(_freshness, held, OnlineCadence.Rain(UpdateCadence.Current), note: Str.Online_Source.Format(("name", "Open-Meteo")));
        Lay();
    }

    private void Lay()
    {
        if (_detached || _outlook is not { } outlook || !_area.IsMeasured)
        {
            return;
        }

        var width = Math.Max(0, _area.Width - (2 * OnlineLayouts.Inset));
        var hasCaption = _caption.Text.Length > 0;

        // The sentence's step by its width first (it is the widget); two lines where even the
        // smallest step cannot hold it on one. The ladder then spends the height under it.
        var iconRoom = width >= 200 ? Fluent.Space6 + Fluent.Space3 : 0;
        var characters = Math.Max(8, _headline.Text.Length);
        var wanted = FluentType.Fit(width - iconRoom, FluentType.Title.LineHeight, characters, FluentType.BodyStrong, FluentType.Title);
        var wraps = FluentType.EstimateWidth(FluentType.BodyStrong, characters) > width - iconRoom;
        _headline.TextWrapping = wraps ? TextWrapping.WrapWholeWords : TextWrapping.NoWrap;
        _headline.MaxLines = wraps ? 2 : 1;
        var floor = wanted.LineHeight * (wraps ? 2 : 1);
        var plan = RainLayout.Rain(_area, floor, hasCaption);

        _caption.Visibility = plan.Caption ? Visibility.Visible : Visibility.Collapsed;
        var showChart = plan.ChartHeight > 0 && outlook.Headline != RainHeadline.Outdated;
        _chart.Visibility = showChart ? Visibility.Visible : Visibility.Collapsed;
        _chart.Height = showChart ? plan.ChartHeight : double.NaN;
        RowDefinitions[0].Height = showChart ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        RowDefinitions[1].Height = showChart ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        _chart.VerticalAlignment = VerticalAlignment.Bottom;

        // The sentence at the largest step its room allows, measured on its own length.
        _icon.Visibility = iconRoom > 0 ? Visibility.Visible : Visibility.Collapsed;
        var room = plan.HeadlineRoom / (wraps ? 2 : 1);
        var step = wraps ? FluentType.BodyStrong : FluentType.Fit(width - iconRoom, room, characters, FluentType.BodyStrong, FluentType.Title);
        _headline.Apply(step);
        _icon.FontSize = Math.Clamp(step.LineHeight, Fluent.IconSize, 40);
        SetRowSpan(_icon, plan.Caption ? 2 : 1);

        DrawBars();
    }

    private void OnChartSize(object sender, SizeChangedEventArgs args) => DrawBars();

    /// <summary>[S-5125] One bar per quarter hour on a fixed scale; a dry quarter is a hairline on the baseline.</summary>
    private void DrawBars()
    {
        _bars.Children.Clear();

        if (_chart.Visibility != Visibility.Visible || _outlook is not { Steps.Count: > 0 } outlook)
        {
            return;
        }

        var width = _chart.ActualWidth;
        var height = _chart.ActualHeight - FluentType.Caption.LineHeight - Fluent.Space1;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var slots = RainRules.WindowSteps;
        var gap = Math.Min(Fluent.Space1, width / (slots * 4));
        var barWidth = (width - ((slots - 1) * gap)) / slots;
        var accent = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        var rule = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray);

        for (var i = 0; i < outlook.Steps.Count && i < slots; i++)
        {
            var share = RainRules.BarShare(outlook.Steps[i].Millimetres);
            var barHeight = share > 0 ? Math.Max(Fluent.Space1, share * height) : Fluent.Hairline;
            var bar = new Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                RadiusX = share > 0 ? Fluent.ControlRadius / 2 : 0,
                RadiusY = share > 0 ? Fluent.ControlRadius / 2 : 0,
                Fill = share > 0 ? accent : rule,
            };

            Canvas.SetLeft(bar, i * (barWidth + gap));
            Canvas.SetTop(bar, height - barHeight);
            _bars.Children.Add(bar);
        }
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

        _bars.Children.Clear();
        _ticker = null;
    }

    private void PublishTitle() => _surface?.SetHeaderTitle(_config.Title);
}
