using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Run = Microsoft.UI.Xaml.Documents.Run;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// One instrument's price, its move, and the shape of the period — the readout
/// <see cref="MeterView"/> gives a system meter, pointed at a market.
/// <para>
/// The chart is <b>full-bleed</b>: it reaches the card's left, right and bottom edges, and the text
/// above it carries the inset instead. It takes whatever the ladder leaves ([S-1311]) rather than a
/// fixed band, so a large tile is a large chart with its axis and times, not a strip under a huge
/// number. Concise ([S-2770]): no facts and no range chips; the period is a setting.
/// </para>
/// <para>
/// The two services behind it — Yahoo's chart endpoint and CoinGecko's public API — are free,
/// shared and unauthenticated, so this widget is careful about asking: one request per symbol per
/// interval however many widgets want it, and a failure backs off rather than retrying.
/// </para>
/// </summary>
internal sealed partial class StocksView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetAligned
{
    private const string Scope = "stocks";

    private readonly TextBlock _freshness = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    // [S-667] The shared hero readout: the price with its currency smaller beside it, the change
    // and the session on its baseline (or on a line under it), then the holding and the extended
    // print as its caption lines.
    private readonly HeroReadout _hero = new();
    private readonly TextBlock _holding;
    private readonly TextBlock _extended;
    private readonly Run _change = new();
    private readonly Run _phaseIcon;
    private readonly Run _phase = new();
    private string _changeText = string.Empty;
    private string _phaseText = string.Empty;
    private bool _phaseShown;
    private string _phaseGlyph = Glyphs.Moon;

    private readonly StackPanel _emptyState;
    private readonly TextBlock _emptyMessage = new();

    private readonly Brush _freshBrush = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _staleBrush = ThemeBrush.Of("SystemFillColorCautionBrush", Colors.Goldenrod);
    private readonly Brush _flatBrush = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray);
    private readonly Brush _accentBrush = ThemeBrush.Of("AccentTextFillColorPrimaryBrush", Colors.DodgerBlue);


    private Ticker? _ticker;
    private StocksConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the settings page edits the live config object in place.
    /// </summary>
    private StocksConfig _applied = new();

    private IWidgetSurface? _surface;
    private IDisposable? _clock;
    private readonly IWidgetHost _host;
    private IQuoteAnswers? _web;

    /// <summary>What the widget asks for now, or null without a usable symbol ([S-2663]).</summary>
    private QuoteInterest? _want;

    /// <summary>The state to say when there is no quote: not set up, refused, or how the last sync went.</summary>
    private FetchStatus _status = FetchStatus.Loading;

    private Fetched<TickerQuote>? _quote;
    private WidgetArea _area;
    private StockPlan _plan;
    private Brush _direction;
    private bool _detached;

    public StocksView(IWidgetHost host)
    {
        _host = host;
        // Rows: the readout (price, change, extended print), the chart (the rest, its times inside it).
        // Columns: one, or readout | chart on a strip.
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _direction = _flatBrush;

        _phaseIcon = WidgetParts.GlyphRun(Glyphs.Moon, _freshBrush);
        BuildReadout(out _holding, out _extended);
        BuildChart();

        _emptyState = Fluent.EmptyState(Glyphs.Stocks, _emptyMessage, out _);
        _emptyState.Visibility = Visibility.Collapsed;
        SetRowSpan(_emptyState, 2);
        SetColumnSpan(_emptyState, 2);

        Children.Add(_hero);
        Children.Add(_chart);
        Children.Add(_emptyState);
    }

    /// <summary>
    /// The age of the quote, in the card's header. Built in the constructor because the card reads
    /// this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _freshness;

    /// <summary>[S-1311] Refresh: the network now, past the cache and any back-off.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.EffectiveSymbol.Length == 0 ? [] : [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: true))];

    private void BuildReadout(out TextBlock holding, out TextBlock extended)
    {
        holding = _hero.AddLine(FluentType.Caption);
        extended = _hero.AddLine(FluentType.Caption);
        holding.Visibility = Visibility.Collapsed;
        extended.Visibility = Visibility.Collapsed;
        _hero.Margin = new Thickness(StockLayout.Inset, Fluent.Space1, StockLayout.Inset, 0);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;

        // [S-1311] The session badge and the age follow the clock, not the refresh: with a
        // 30-minute refresh the badge must still flip at the bell. A render, never a fetch.
        _clock = ticker.Subscribe(TimeSpan.FromMinutes(1), OnMinute);

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        // [S-2663] The quote comes from the background sync; the widget only hears it.
        _web?.Updated -= OnWebUpdated;
        _web = _host.Quotes;
        _web.Updated += OnWebUpdated;

        ApplyConfig(config);
    }

    /// <summary>The language changed: every text is rewritten from the quote in hand.</summary>
    private void OnLanguage(Locale locale) => Render();

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    /// <summary>[S-6583] Where the price and its change sit: the widget's alignment, from the start by default ([S-6582]).</summary>
    private ContentAlign _align = ContentAlign.Left;

    /// <summary>[S-6583] The widget's alignment, from the properties panel.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        _hero.Align(align);
        Render();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as StocksConfig ?? new StocksConfig();
        var previous = _applied;
        _config = next;
        _applied = (StocksConfig)next.Clone();
        PublishTitle();

        var needsFetch = previous.EffectiveSymbol != next.EffectiveSymbol
            || previous.Market != next.Market
            || previous.Range != next.Range
            || previous.WantsPrePost != next.WantsPrePost;

        if (needsFetch)
        {
            // A different instrument, or a different period. The numbers in hand are about
            // something else and must not sit under the new heading for even one frame.
            _quote = null;
        }

        // [S-2663] Always through the sync: it asks nothing for a quote already held and fresh, so
        // a colour or a toggle costs no request.
        Reload(force: false);
    }

    private void OnMinute()
    {
        if (_detached || _quote?.Value is not { } quote)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        UpdatePhase(quote, now);
        UpdateFreshness(_quote, now);
        Lay(quote);
    }

    /// <summary>
    /// [S-2663] Tell the sync what is on screen and draw what it holds. The quote is kept fresh by
    /// the sync service, never fetched from a tick; <paramref name="force"/> (the user's Refresh,
    /// [S-1311]) asks now.
    /// </summary>
    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        _want = _config.Interest();
        if (_want is null)
        {
            // Told apart on purpose: nothing typed is "not set up", something typed that is not a
            // symbol is "refused".
            _status = string.IsNullOrWhiteSpace(_config.Symbol) ? FetchStatus.NotConfigured : FetchStatus.Refused;
            _quote = null;
            PublishTitle();
            Render();
            return;
        }

        _web?.Show(_want, force);
        Take();
    }

    /// <summary>[S-2663] A quote's answer or state changed in the sync service.</summary>
    private void OnWebUpdated(string key)
    {
        if (!_detached && _want is { } want && _web is { } web && web.Key(want) == key)
        {
            Take();
        }
    }

    /// <summary>What the sync holds: the newest quote (kept through a failure, annotated with it), or the state to say.</summary>
    private void Take()
    {
        if (_want is not { } want)
        {
            return;
        }

        var answer = _web?.Quote(want);
        _quote = answer?.Value is not null ? answer : null;
        _status = answer?.Status ?? FetchStatus.Loading;
        PublishTitle();
        Render();
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var held = _quote;

        if (held?.Value is not { } quote)
        {
            var status = _status;

            _emptyMessage.Text = status switch
            {
                // No symbol yet: the card's Set up button covers this ([S-1862]).
                FetchStatus.NotConfigured => string.Empty,
                FetchStatus.Refused => Str.WidgetsC_Stocks_NotSymbol.Format(("symbol", _config.Symbol)),
                FetchStatus.Empty => Str.WidgetsC_Stocks_NoQuote.Text(),
                _ => FetchState.Message(status),
            };

            _emptyState.Visibility = Visibility.Visible;
            _hero.Visibility = Visibility.Collapsed;
            _chart.Visibility = Visibility.Collapsed;
            _freshness.Text = string.Empty;
            return;
        }

        _emptyState.Visibility = Visibility.Collapsed;
        _hero.Visibility = Visibility.Visible;

        var price = MarketQuote.Price(quote.Price, quote.PriceHint);
        _hero.SetValue(price, quote.Currency, spaced: true);
        _changeText = MarketQuote.ChangePercent(quote.ChangePercent);

        _direction = quote.ChangePercent switch
        {
            > 0 => Up(),
            < 0 => Down(),
            _ => _flatBrush,
        };

        _change.Foreground = _direction;
        var holding = UpdateHolding(quote);

        var phase = UpdatePhase(quote, now);
        var extended = _config.WantsPrePost
            ? MarketQuote.CurrentExtended(MarketQuote.ExtendedQuotesOf(quote.Bars, quote.Sessions, quote.Price), phase)
            : null;

        UpdateExtended(quote, extended);
        UpdateFreshness(held, now);

        // [S-6165] The chart style draws the period's line; the number style the price and change alone.
        var number = _config.Style == StocksStyle.Number;
        var chart = !number && quote.Bars.Count >= 2;
        var extras = _changeText.Length + 1 + quote.Currency.Length;

        _plan = StockLayout.Plan(_area, new StockInputs(price.Length, extras, chart, extended is not null, _config.ShowMinMax, holding, number));
        Lay(quote);
    }

    /// <summary>Place the parts the plan chose. The chart draws itself on its own size change.</summary>
    private void Lay(TickerQuote quote)
    {
        var plan = _plan;

        _hero.ShowUnit(plan.Currency);
        _holding.Visibility = plan.Holding && _holding.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _extended.Visibility = plan.ExtendedLine && _extended.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // The change and the session: on the price's baseline, or on their own line under it.
        var beside = !plan.ExtrasUnder;
        _change.Text = (beside ? "  " : string.Empty) + _changeText;
        var trailing = new List<Run> { _change };

        if (_phaseShown)
        {
            _phaseIcon.Text = "  " + _phaseGlyph;
            trailing.Add(_phaseIcon);

            if (plan.BadgeText)
            {
                _phase.Text = " " + _phaseText;
                trailing.Add(_phase);
            }
        }

        _hero.SetTrailing(trailing);
        _hero.Lay(plan.Price, icon: false, trailingBeside: beside, linesBeside: false,
            _config.Style == StocksStyle.Number ? plan.Change : null);

        if (plan.Beside)
        {
            // A strip: the readout is a column on the left, the chart has the strip's height.
            ColumnDefinitions[0].Width = new GridLength(plan.ReadoutWidth + (2 * StockLayout.Inset));
            ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            Place(_hero, 0, 0, 2, 1);
            _hero.VerticalAlignment = VerticalAlignment.Center;
            Place(_chart, 0, 1, 2, 1);
        }
        else
        {
            ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinitions[1].Width = GridLength.Auto;
            Place(_hero, 0, 0, plan.Chart ? 1 : 2, 2);

            // Without a chart the price is the tile: centred in it rather than stranded at the top.
            _hero.VerticalAlignment = plan.Chart ? VerticalAlignment.Top : VerticalAlignment.Center;
            _hero.Margin = new Thickness(StockLayout.Inset, plan.Chart ? Fluent.Space1 : 0, StockLayout.Inset, 0);
            Place(_chart, 1, 0, 1, 2);
        }

        _chart.Visibility = plan.Chart ? Visibility.Visible : Visibility.Collapsed;
        _labels.Visibility = plan.Labels ? Visibility.Visible : Visibility.Collapsed;

        if (plan.Chart)
        {
            UpdateLabels(quote);
            RedrawChart();
        }
    }

    /// <summary>
    /// [S-2777] The holding's caption: what the shares owned are worth now and how much that moved
    /// over the shown period, in the quote's currency ("10 held · 4,202.70 USD  +82.95"; the amount
    /// is dropped where the line is narrow). False when there is no holding or nothing to price it.
    /// </summary>
    private bool UpdateHolding(TickerQuote quote)
    {
        _holding.Inlines.Clear();

        var locale = Strings.Current;
        if (_config.SharesOwned is not { } owned || StockHolding.Worth(owned, quote.Price, quote.Currency) is not { } worth)
        {
            return false;
        }

        _holding.Inlines.Add(new Run { Text = StockHolding.Caption(locale, owned, worth, _area.Width) });

        if (StockHolding.Change(owned, quote.Price, quote.ChangePercent, quote.Currency) is { } move)
        {
            _holding.Inlines.Add(WidgetParts.Run("  " + StockHolding.SignedSum(locale, move), _direction));
        }

        return true;
    }

    private static void Place(FrameworkElement element, int row, int column, int rowSpan, int columnSpan)
    {
        SetRow(element, row);
        SetColumn(element, column);
        SetRowSpan(element, rowSpan);
        SetColumnSpan(element, columnSpan);
    }

    /// <summary>
    /// The session badge, evaluated against the clock (not the fetch), so it flips at the bell
    /// whatever the refresh interval. Crypto never carries one; an unknown schedule carries none.
    /// [S-1308] An extended session with a print shows in the accent colour.
    /// </summary>
    private MarketPhase UpdatePhase(TickerQuote quote, DateTimeOffset now)
    {
        var phase = MarketQuote.Phase(quote.Sessions, now);
        var label = !_config.ShowMarketState || _config.Market == QuoteMarket.Crypto ? string.Empty : MarketQuote.PhaseLabel(phase);

        _phaseShown = label.Length > 0;
        _phaseText = label;

        if (!_phaseShown)
        {
            ToolTipService.SetToolTip(_hero.ValueLine, null);
            return phase;
        }

        var accent = _config.WantsPrePost && phase is MarketPhase.Pre or MarketPhase.Post && quote.HasPrePost;
        var brush = accent ? _accentBrush : _freshBrush;

        _phase.Foreground = brush;
        _phaseGlyph = phase == MarketPhase.Closed ? Glyphs.Moon : Glyphs.Sun;
        _phaseIcon.Foreground = brush;

        // [S-1309] The session names the exchange's zone, so "closed" reads against its clock.
        ToolTipService.SetToolTip(_hero.ValueLine, Str.WidgetsC_Stocks_PhaseTooltip.Format(("phase", label), ("zone", ExchangeTime.ZoneNote(quote.ZoneName))));
        return phase;
    }

    /// <summary>[S-1308] "After hours 412.10 −0.36%" under the price, in its own direction's colour.</summary>
    private void UpdateExtended(TickerQuote quote, ExtendedPrint? print)
    {
        if (print is not { } p)
        {
            _extended.Text = string.Empty;
            return;
        }

        var phase = MarketQuote.Phase(quote.Sessions, DateTimeOffset.Now);
        var name = phase == MarketPhase.Pre || (phase != MarketPhase.Post && quote.Sessions.Pre is { } pre && p.At >= pre.Start && p.At < pre.End)
            ? Str.Stocks_PreMarket.Text()
            : Str.Stocks_AfterHours.Text();
        var at = ExchangeTime.Label(p.At, quote.ExchangeOffset, QuoteRange.Day, DateTimeOffset.Now.Offset);

        _extended.Text = Str.WidgetsC_Stocks_ExtendedLine.Format(
            ("session", name),
            ("price", MarketQuote.Price(p.Price, quote.PriceHint)),
            ("change", MarketQuote.ChangePercent(p.ChangePercent)),
            ("time", at));
        _extended.Foreground = p.ChangePercent switch
        {
            > 0 => Up(),
            < 0 => Down(),
            _ => _flatBrush,
        };
    }

    private void UpdateFreshness(Fetched<TickerQuote> held, DateTimeOffset now)
    {
        var failed = FetchState.IsFailure(held.Status);

        // [S-1300] A price read back from disk is shown as stale until a fetch replaces it.
        var stale = failed || held.Restored || FetchState.IsStale(held.At, now, _config.TickInterval);
        var age = FetchState.Freshness(held.At, now);

        _freshness.Text = age;
        _freshness.Foreground = stale ? _staleBrush : _freshBrush;

        // A price is the single most dangerous number in this app to show stale, so the age is
        // always on screen and the reason for it is always one hover away.
        var inline = WeatherNowView.InlineAge(age);
        ToolTipService.SetToolTip(
            _freshness,
            failed
                ? Str.WidgetsC_Stocks_Tooltip_Failed.Format(("problem", FetchState.Message(held.Status)), ("age", inline))
                : held.Restored
                    ? Str.WidgetsC_Stocks_Tooltip_Saved.Format(("age", inline))
                    : Str.WidgetsC_Stocks_Tooltip_Priced.Format(("age", inline)));
    }

    /// <summary>[S-1310] The rise colour: the user's, or the theme's success green.</summary>
    private Brush Up() => Custom(_config.UpColor) ?? ThemeBrush.Of("SystemFillColorSuccessBrush", Colors.SeaGreen);

    /// <summary>[S-1310] The fall colour: the user's, or the theme's critical red.</summary>
    private Brush Down() => Custom(_config.DownColor) ?? ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);

    private static SolidColorBrush? Custom(string? hex)
        => HexColor.TryParse(hex, out var r, out var g, out var b) ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b)) : null;

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

        _clock?.Dispose();
        _clock = null;

        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web = null;
        }

        DetachChart();
        _ticker = null;
    }

    /// <summary>
    /// The header title: the user's name, else the service's own name for the instrument (S-01),
    /// else the symbol.
    /// </summary>
    private void PublishTitle()
    {
        var title = !string.IsNullOrWhiteSpace(_config.DisplayName)
            ? _config.DisplayName
            : _quote?.Value is { Name.Length: > 0 } quote && _config.Market == QuoteMarket.Listed
                ? quote.Name
                : _config.EffectiveSymbol;

        _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(title) ? null : title);
    }
}
