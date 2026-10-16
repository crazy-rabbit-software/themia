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
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5140] The ECB's reference rate of one pair, in one of two styles ([S-6167]). Number: the rate
/// as the hero readout with its day change (the shared <see cref="HeroReadout"/>), as large as the
/// tile allows ([S-5146]). Chart: the same readout over (or on a strip beside) its 90 days
/// ([S-6168]). The header names the pair and says which day's rate this is.
/// </summary>
internal sealed partial class ExchangeRatesView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions
{
    private readonly TextBlock _freshness = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly HeroReadout _hero = new();
    private readonly TextBlock _perBase;
    private readonly Canvas _chart = new() { IsHitTestVisible = false };
    private readonly StackPanel _emptyState;
    private readonly TextBlock _emptyMessage = new();

    private Ticker? _ticker;
    private IWidgetSurface? _surface;
    private ExchangeRatesConfig _config = new();
    private IDisposable? _subscription;
    private WebSync? _web;
    private FetchStatus _status = FetchStatus.Loading;
    private Fetched<RateTable>? _table;
    private WidgetArea _area;
    private bool _detached;

    public ExchangeRatesView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _perBase = _hero.AddLine(FluentType.Caption);
        _hero.Margin = new Thickness(OnlineLayouts.Inset, OnlineLayouts.VerticalInset, OnlineLayouts.Inset, 0);
        _hero.VerticalAlignment = VerticalAlignment.Center;

        // [S-1113] Time runs left to right in every language.
        _chart.FlowDirection = FlowDirection.LeftToRight;
        _chart.Margin = new Thickness(0, OnlineLayouts.Gap, 0, 0);
        _chart.SizeChanged += (_, _) => DrawChart();
        SetRow(_chart, 1);

        _emptyState = Fluent.EmptyState(Glyphs.ExchangeRates, _emptyMessage, out _);
        SetRowSpan(_emptyState, 2);
        SetColumnSpan(_emptyState, 2);

        Children.Add(_hero);
        Children.Add(_chart);
        Children.Add(_emptyState);
    }

    public FrameworkElement? HeaderTrailing => _freshness;

    public IReadOnlyList<WidgetAction> ContextActions =>
        [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: true))];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        _web = AppServices.WebSync;
        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web.Updated += OnWebUpdated;
        }

        // Only the age in the header moves between fetches; a minute is fine enough for it.
        _subscription?.Dispose();
        _subscription = _ticker.Subscribe(TimeSpan.FromMinutes(1), () =>
        {
            if (!_detached)
            {
                Stamp();
            }
        });

        ApplyConfig(config);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    /// <summary>[S-5146] The header names the pair: "Exchange rates · CHF/EUR" (a typed title wins, as everywhere).</summary>
    private void PublishTitle() => _surface?.SetHeaderTitle($"{Str.Online_Rates_Label.Text()} · {_config.EffectiveBase}/{_config.EffectiveQuote}");

    /// <summary>The base, the quote or the style: all drawn from the one table in hand.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ExchangeRatesConfig ?? new ExchangeRatesConfig();
        PublishTitle();
        Reload(force: false);
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    /// <summary>[S-3770] A language change, or a Windows region change that moves the default base.</summary>
    private void OnLanguage(Locale locale)
    {
        PublishTitle();
        Reload(force: false);
    }

    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        _web?.ShowRates(force);
        Take();
    }

    private void OnWebUpdated(string key)
    {
        if (!_detached && key == WebSync.RatesKey)
        {
            Take();
        }
    }

    private void Take()
    {
        var answer = _web is { } web ? web.Rates() : OnlineService.PeekRates();
        _table = answer?.Value is not null ? answer : null;
        _status = answer?.Status ?? FetchStatus.Loading;
        Render();
    }

    private void Stamp()
    {
        if (_table is not { Value: { } table } held)
        {
            _freshness.Text = string.Empty;
            return;
        }

        var date = LocaleFormat.Date(table.Latest.Date, DateStyle.MonthDayShort);
        OnlineParts.Stamp(_freshness, held, OnlineCadence.Rates,
            text: Str.Online_Rates_Fix.Format(("date", date)),
            note: Str.Online_Rates_FixTooltip.Format(("date", LocaleFormat.Date(table.Latest.Date, DateStyle.Long))));
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var quote = _config.EffectiveQuote;
        var rate = _table?.Value is { } table ? ExchangeRules.Cross(table, _config.EffectiveBase, quote) : null;

        // [S-5145] No table yet, or the latest day lacks one of the two: said as a state.
        if (rate is null)
        {
            _emptyMessage.Text = _table is null ? FetchState.Message(_status) : Str.Online_Rates_Missing.Text();
            _emptyState.Visibility = Visibility.Visible;
            _hero.Visibility = _chart.Visibility = Visibility.Collapsed;
            Stamp();
            return;
        }

        _emptyState.Visibility = Visibility.Collapsed;
        Stamp();

        if (!_area.IsMeasured)
        {
            return;
        }

        // The readout's floor is Subtitle. Alone (Number) it is the widget and grows as far as the tile
        // allows, past Display as a lone reading does ([S-1762]); over a chart, to the tier's step ([S-3860]).
        var heroWidth = Math.Max(0, _area.Width - (2 * OnlineLayouts.Inset));
        var heroFloor = FluentType.Subtitle.LineHeight + FluentType.Caption.LineHeight;
        var plan = OnlineLayouts.Rates(_area, _config.Style, heroFloor);

        // [S-6168] A strip in the chart style: the readout a column on the left, the chart beside it.
        ColumnDefinitions[0].Width = plan.Beside ? new GridLength(plan.HeroWidth + (2 * OnlineLayouts.Inset)) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = plan.Beside ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        SetRow(_chart, plan.Beside ? 0 : 1);
        SetRowSpan(_chart, plan.Beside ? 2 : 1);
        SetColumn(_chart, plan.Beside ? 1 : 0);
        SetColumnSpan(_chart, plan.Beside ? 1 : 2);
        SetRowSpan(_hero, plan.Beside ? 2 : 1);
        _chart.Margin = new Thickness(0, plan.Beside ? 0 : OnlineLayouts.Gap, 0, 0);
        _hero.Margin = new Thickness(OnlineLayouts.Inset, plan.Beside ? 0 : OnlineLayouts.VerticalInset, OnlineLayouts.Inset, 0);

        _hero.Visibility = Visibility.Visible;
        var alone = _config.Style == ExchangeRatesStyle.Number;
        ShowHero(rate, plan.HeroRoom, plan.Beside ? plan.HeroWidth : heroWidth, alone ? FluentType.Display : HeroLayout.TierStep(_area), grow: alone);

        _chartRate = plan.ChartHeight > 0 ? rate : null;
        _chart.Visibility = _chartRate is not null ? Visibility.Visible : Visibility.Collapsed;
        var heroOnly = _chartRate is null || plan.Beside;
        RowDefinitions[0].Height = heroOnly ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        RowDefinitions[1].Height = heroOnly ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        DrawChart();
    }

    private PairRate? _chartRate;

    /// <summary>[S-5145] The pair, big: the rate, the quote's code, the day change beside it, "per CHF" under.</summary>
    private void ShowHero(PairRate rate, double room, double width, TypeStep ceiling, bool grow)
    {
        _hero.SetValue(ExchangeRules.Format(rate.Rate), rate.Quote, spaced: true);
        _perBase.Text = Str.Online_Rates_PerBase.Format(("code", rate.Base));

        // The change stands beside the rate where it fits; under it, its line is paid for out of
        // the same room, so the hero never grows past what the ladder gave it (an overflow here
        // changed the card's header and the area with it, and the two fought).
        bool Beside(TypeStep s) => HeroLayout.ValueWidth(s, ExchangeRules.RateCharacters, 4) + HeroLayout.BesideGap
            + FluentType.EstimateWidth(HeroLayout.TrailingStep(s), 7) <= width;

        var lines = room - FluentType.Caption.LineHeight;
        var step = HeroLayout.Fit(width, lines, ExchangeRules.RateCharacters, 3, FluentType.Subtitle, ceiling);
        var beside = Beside(step);
        if (!beside)
        {
            step = HeroLayout.Fit(width, lines - HeroLayout.TrailingStep(step).LineHeight, ExchangeRules.RateCharacters, 3, FluentType.Subtitle, ceiling);
            beside = Beside(step);
        }

        // [S-5146] Alone on the tile, past Display where the box has room for more.
        if (grow)
        {
            var grown = HeroLayout.Grow(step, width, beside ? lines : lines - HeroLayout.TrailingStep(step).LineHeight, ExchangeRules.RateCharacters, 3);
            if (!beside || Beside(grown))
            {
                step = grown;
            }
        }

        // The change: after a space beside the rate, flush with the inset under it.
        var change = OnlineText.Change(rate.ChangePercent);
        _hero.SetTrailing(change.Length > 0 ? [WidgetParts.Run((beside ? " " : string.Empty) + change, Direction(rate.ChangePercent))] : []);
        _hero.Lay(step, icon: false, trailingBeside: beside, linesBeside: false);
    }

    /// <summary>[S-6168] The pair's 90 days, edge to edge and down to the card's bottom, filled under the line.</summary>
    private void DrawChart()
    {
        _chart.Children.Clear();

        if (_chartRate is not { History.Count: >= 2 } rate || _chart.Visibility != Visibility.Visible)
        {
            return;
        }

        var width = _chart.ActualWidth;
        var height = _chart.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var scaled = MarketQuote.Normalise(rate.History);
        var pad = Fluent.Space2;
        var usable = Math.Max(1, height - (2 * pad));
        var brush = Direction(rate.ChangePercent);
        var points = new PointCollection();

        for (var i = 0; i < scaled.Count; i++)
        {
            points.Add(new Point(Sparkline.SampleX(i, scaled.Count, scaled.Count) * width, pad + (Sparkline.SampleY(scaled[i]) * usable)));
        }

        var fill = new PointCollection { new(points[0].X, height) };
        foreach (var p in points)
        {
            fill.Add(p);
        }

        fill.Add(new Point(points[^1].X, height));
        _chart.Children.Add(new Polygon { Points = fill, Fill = brush, Opacity = Fluent.ChartWash });
        _chart.Children.Add(new Polyline { Points = points, Stroke = brush, StrokeThickness = Fluent.ChartStroke, StrokeLineJoin = PenLineJoin.Round });
    }

    /// <summary>Up in the success colour, down in the critical one, unchanged in the secondary text colour.</summary>
    private static Brush Direction(double? change) => change switch
    {
        > 0 => FluentLayout.Themed("SystemFillColorSuccessBrush", Colors.SeaGreen),
        < 0 => FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red),
        _ => FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray),
    };

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

        _chart.Children.Clear();
        _ticker = null;
    }
}
