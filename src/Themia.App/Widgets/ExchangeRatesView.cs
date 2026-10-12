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
/// [S-5140] The ECB's reference rates for a base currency: the first currency as the hero readout
/// with its day change (the shared <see cref="HeroReadout"/>), the others as rows with their own
/// change and, where there is room, a small 90-day line; on a large tile the first currency's 90 days
/// as a chart under the rows ([S-5146]). The header says which day's rates these are.
/// </summary>
internal sealed partial class ExchangeRatesView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions
{
    private readonly TextBlock _freshness = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly HeroReadout _hero = new();
    private readonly TextBlock _perBase;
    private readonly StackPanel _rows = new();
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
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _perBase = _hero.AddLine(FluentType.Caption);
        _hero.Margin = new Thickness(OnlineLayouts.Inset, OnlineLayouts.VerticalInset, OnlineLayouts.Inset, 0);
        _hero.VerticalAlignment = VerticalAlignment.Center;

        _rows.Margin = new Thickness(0, 0, 0, Fluent.Space1);
        SetRow(_rows, 1);

        // [S-1113] Time runs left to right in every language.
        _chart.FlowDirection = FlowDirection.LeftToRight;
        _chart.Margin = new Thickness(0, OnlineLayouts.Gap, 0, 0);
        _chart.SizeChanged += (_, _) => DrawChart();
        SetRow(_chart, 2);

        _emptyState = Fluent.EmptyState(Glyphs.ExchangeRates, _emptyMessage, out _);
        SetRowSpan(_emptyState, 3);

        Children.Add(_hero);
        Children.Add(_rows);
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

    /// <summary>[S-5146] The header names the base: "Exchange rates · CHF" (a typed title wins, as everywhere).</summary>
    private void PublishTitle() => _surface?.SetHeaderTitle($"{Str.Online_Rates_Label.Text()} · {_config.EffectiveBase}");

    /// <summary>The base, the currencies or the chart switch: all drawn from the one table in hand.</summary>
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

    private (string Quote, PairRate? Rate)[] Pairs(RateTable table)
    {
        var basis = _config.EffectiveBase;
        return [.. _config.EffectiveQuotes.Select(q => (q, ExchangeRules.Cross(table, basis, q)))];
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var pairs = _table?.Value is { } table ? Pairs(table) : [];

        if (pairs.Length == 0 || pairs.All(p => p.Rate is null))
        {
            _emptyMessage.Text = _table is null ? FetchState.Message(_status) : Str.Online_Rates_Missing.Text();
            _emptyState.Visibility = Visibility.Visible;
            _hero.Visibility = _rows.Visibility = _chart.Visibility = Visibility.Collapsed;
            _rows.Children.Clear();
            Stamp();
            return;
        }

        _emptyState.Visibility = Visibility.Collapsed;
        Stamp();

        if (!_area.IsMeasured)
        {
            return;
        }

        // From 280 tall the hero is as large as its width allows (to Title Large); below, Subtitle.
        var heroWidth = Math.Max(0, _area.Width - (2 * OnlineLayouts.Inset));
        var heroStep = _area.HeightClass >= SizeClass.Expanded
            ? HeroLayout.Fit(heroWidth, FluentType.TitleLarge.LineHeight, ExchangeRules.RateCharacters, 3, FluentType.Subtitle, FluentType.TitleLarge)
            : FluentType.Subtitle;
        var heroFloor = heroStep.LineHeight + FluentType.Caption.LineHeight;
        var plan = OnlineLayouts.Rates(_area, pairs.Length, _config.ShowChart, heroFloor);
        var first = pairs[0];

        _hero.Visibility = plan.Hero ? Visibility.Visible : Visibility.Collapsed;
        if (plan.Hero)
        {
            ShowHero(first.Quote, first.Rate, plan.HeroRoom);
        }

        _rows.Children.Clear();
        var listed = plan.Hero ? pairs.Skip(1) : pairs;
        foreach (var (quote, rate) in listed.Take(plan.Rows))
        {
            _rows.Children.Add(Row(quote, rate, plan.RowHeight, plan.RowSparkWidth));
        }

        _rows.Visibility = _rows.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        _chartRate = plan.ChartHeight > 0 ? first.Rate : null;
        _chart.Visibility = _chartRate is not null ? Visibility.Visible : Visibility.Collapsed;
        RowDefinitions[0].Height = plan.Hero && plan.Rows == 0 && plan.ChartHeight == 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        RowDefinitions[2].Height = plan.Hero && plan.Rows == 0 && plan.ChartHeight == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        DrawChart();
    }

    private PairRate? _chartRate;

    /// <summary>The first currency, big: the rate, its code, the day change beside it, "per CHF" under.</summary>
    private void ShowHero(string quote, PairRate? rate, double room)
    {
        var width = Math.Max(0, _area.Width - (2 * OnlineLayouts.Inset));

        if (rate is null)
        {
            _hero.SetValue(quote, string.Empty, spaced: false);
            _hero.SetTrailing([]);
            _perBase.Text = Str.Online_Rates_Missing.Text();
        }
        else
        {
            _hero.SetValue(ExchangeRules.Format(rate.Rate), quote, spaced: true);
            _perBase.Text = Str.Online_Rates_PerBase.Format(("code", rate.Base));
        }

        // The change stands beside the rate where it fits; under it, its line is paid for out of
        // the same room, so the hero never grows past what the ladder gave it (an overflow here
        // changed the card's header and the area with it, and the two fought).
        bool Beside(TypeStep s) => HeroLayout.ValueWidth(s, ExchangeRules.RateCharacters, 4) + HeroLayout.BesideGap
            + FluentType.EstimateWidth(HeroLayout.TrailingStep(s), 7) <= width;

        var lines = room - FluentType.Caption.LineHeight;
        var step = HeroLayout.Fit(width, lines, ExchangeRules.RateCharacters, 3, FluentType.Subtitle, FluentType.TitleLarge);
        var beside = Beside(step);
        if (!beside)
        {
            step = HeroLayout.Fit(width, lines - HeroLayout.TrailingStep(step).LineHeight, ExchangeRules.RateCharacters, 3, FluentType.Subtitle, FluentType.TitleLarge);
            beside = Beside(step);
        }

        // The change: after a space beside the rate, flush with the inset under it.
        var change = rate is null ? string.Empty : OnlineText.Change(rate.ChangePercent);
        _hero.SetTrailing(change.Length > 0 ? [WidgetParts.Run((beside ? " " : string.Empty) + change, Direction(rate?.ChangePercent))] : []);
        _hero.Lay(step, icon: false, trailingBeside: beside, linesBeside: false);
    }

    /// <summary>One currency as a row: its code, an optional 90-day line, the rate and the change.</summary>
    private FrameworkElement Row(string quote, PairRate? rate, double height, double sparkWidth)
    {
        var grid = new Grid { ColumnSpacing = Fluent.Space3 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var code = Fluent.Text(FluentType.BodyStrong);
        code.Text = quote;
        code.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(code);

        if (rate is null)
        {
            var missing = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            missing.Text = Str.Online_Rates_Missing.Text();
            missing.VerticalAlignment = VerticalAlignment.Center;
            SetColumn(missing, 1);
            SetColumnSpan(missing, 3);
            grid.Children.Add(missing);
        }
        else
        {
            if (sparkWidth > 0 && rate.History.Count >= 2)
            {
                var spark = Spark(rate, sparkWidth, height - Fluent.Space3);
                spark.HorizontalAlignment = HorizontalAlignment.Right;
                SetColumn(spark, 1);
                grid.Children.Add(spark);
            }

            var figure = WidgetParts.Figure(ExchangeRules.Format(rate.Rate), FluentType.Body);
            figure.VerticalAlignment = VerticalAlignment.Center;
            SetColumn(figure, 2);
            grid.Children.Add(figure);

            var change = WidgetParts.Figure(OnlineText.Change(rate.ChangePercent), FluentType.Caption);
            change.Foreground = Direction(rate.ChangePercent);
            change.MinWidth = 52;
            change.VerticalAlignment = VerticalAlignment.Center;
            SetColumn(change, 3);
            grid.Children.Add(change);
        }

        var row = Fluent.Row(grid, compact: height < OnlineLayouts.RowHeight);
        row.Height = height;
        return row;
    }

    /// <summary>A small line of the 90 days, in the change's colour, on its own min-to-max scale.</summary>
    private static Polyline Spark(PairRate rate, double width, double height)
    {
        var scaled = MarketQuote.Normalise(rate.History);
        var line = new Polyline
        {
            Stroke = Direction(rate.ChangePercent),
            StrokeThickness = Fluent.ChartStroke / 1.5,
            StrokeLineJoin = PenLineJoin.Round,
            Width = width,
            Height = Math.Max(8, height),
            FlowDirection = FlowDirection.LeftToRight,
            Points = [],
        };

        for (var i = 0; i < scaled.Count; i++)
        {
            line.Points.Add(new Point(Sparkline.SampleX(i, scaled.Count, scaled.Count) * width, Sparkline.SampleY(scaled[i]) * Math.Max(8, height)));
        }

        return line;
    }

    /// <summary>[S-5146] The first currency's 90 days, edge to edge and down to the card's bottom, filled under the line.</summary>
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
        _rows.Children.Clear();
        _ticker = null;
    }
}
