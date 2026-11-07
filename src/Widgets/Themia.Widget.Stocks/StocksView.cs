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
using Themia.Widget.Stocks;

namespace Themia.App.Widgets;

/// <summary>
/// One instrument's price, its move and the shape of the period, read as a system meter reads
/// (<c>MeterView</c>), on the family's blocks: the price a Readout at the top with its currency and its
/// change ([S-6707]: beside it, or on the line under it), the holding and the extended-hours print as
/// fact rows under it, and the chart style's period 8 under them, edge to edge and down to the card's
/// foot ([S-1311]). Where each goes at each size is <see cref="StockLayout"/>'s. The change is secondary
/// while the price rises or holds, Critical while it falls, and so is the chart (no green, [S-6706]).
/// <para>
/// The two services behind it — Yahoo's chart endpoint and CoinGecko's public API — are free,
/// shared and unauthenticated, so this widget is careful about asking: one request per symbol per
/// interval however many widgets want it, and a failure backs off rather than retrying.
/// </para>
/// </summary>
internal sealed partial class StocksView : Grid, IWidgetView, IWidgetOpens, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetAligned
{
    /// <inheritdoc />
    public CardOpen? Opens => StocksModule.Opens(_config);

    private const string Scope = "stocks";

    /// <summary>[S-7064] The header's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitHeader _header;

    private readonly ReadoutBlock _price = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = Family.SideInset };

    /// <summary>[S-2777] [S-1308] The holding and the extended print: fact rows under the price.</summary>
    private readonly StackPanel _facts = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, FamilySpace.S2, 0, 0) };
    private readonly RowBlock _holdingRow = new(RowKind.Fact, lead: 0);
    private readonly RowBlock _extendedRow = new(RowKind.Fact, lead: 0);

    /// <summary>[S-6734] No quote to draw: the Status row at the top.</summary>
    private readonly StatusBlock _state = new();

    private Ticker? _ticker;
    private StocksConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the properties menu edits the live config object in place.
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

    /// <summary>The chart's and the change's role: Critical while the price falls, else the accent (the change secondary).</summary>
    private bool _falling;
    private bool _detached;

    public StocksView(IWidgetHost host)
    {
        _host = host;
        _header = new KitHeader(host);

        // Three rows: the price, the facts 8 under it, the chart 8 under those and down to the foot.
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _facts.Children.Add(_holdingRow);
        _facts.Children.Add(_extendedRow);
        SetRow(_facts, 1);
        BuildChart();
        SetRow(_chart, 2);

        // Collapsed until a state is said: a view waiting for setup is never attached.
        _state.Visibility = Visibility.Collapsed;
        SetRowSpan(_state, 3);

        Children.Add(_price);
        Children.Add(_facts);
        Children.Add(_chart);
        Children.Add(_state);
    }

    /// <summary>The header's problem glyph. Built in the constructor because the card reads this once, when it builds that header.</summary>
    public FrameworkElement? HeaderTrailing => _header;

    /// <summary>[S-1311] Refresh: the network now, past the cache and any back-off.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.EffectiveSymbol.Length == 0 ? [] : [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_quote), () => Reload(force: true))];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;

        // [S-1311] The session follows the clock, not the refresh: with a 30-minute refresh it must
        // still say "Market closed" at the bell. A render, never a fetch.
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

    /// <summary>[S-6583] The Number style's price across the card: the widget's alignment, from the start by default ([S-6582]).</summary>
    public void ApplyAlign(ContentAlign align) => _price.HorizontalAlignment = Aligned.Horizontal(align);

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
        // a toggle costs no request.
        Reload(force: false);
    }

    private void OnMinute()
    {
        if (!_detached && _quote?.Value is not null)
        {
            Render();
        }
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

        var held = _quote;
        if (held?.Value is not { } quote)
        {
            ShowState();
            return;
        }

        _state.Hide();
        _header.Show(OnlineParts.Problems(held), busy: false);
        if (!_area.IsMeasured)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var price = MarketQuote.Price(quote.Price, quote.PriceHint);
        var currency = quote.Currency.Length > 0 ? " " + quote.Currency : string.Empty;
        var change = MarketQuote.ChangePercent(quote.ChangePercent);
        _falling = quote.ChangePercent < 0;

        var phase = MarketQuote.Phase(quote.Sessions, now);
        var extended = _config.WantsPrePost
            ? MarketQuote.CurrentExtended(MarketQuote.ExtendedQuotesOf(quote.Bars, quote.Sessions, quote.Price), phase)
            : null;
        var holding = ShowHolding(quote);
        var extendedShown = ShowExtended(quote, extended, phase, now);

        // [S-1311] The session under the price, unless the extended print's row names it already.
        var session = !_config.ShowMarketState || _config.Market == QuoteMarket.Crypto || extendedShown ? string.Empty : MarketQuote.PhaseLabel(phase);
        var chart = _config.Style == StocksStyle.Chart && quote.Bars.Count >= 2;

        _plan = StockLayout.Plan(_area, new StockInputs(
            new ReadoutText(price, currency),
            MarketQuote.ChangePercent(88.88),
            (holding ? 1 : 0) + (extendedShown ? 1 : 0),
            chart,
            _config.ShowMinMax,
            session.Length > 0));

        var plan = _plan;
        var changeRole = _falling ? ColourRole.Critical : ColourRole.TextSecondary;
        var underText = !plan.Under ? null : plan.Fold.DeltaBeside ? session : change;
        _price.Use(plan.Price);
        _price.Show(
            price,
            plan.Fold.Unit ? currency : string.Empty,
            underText,
            plan.Fold.DeltaBeside ? change : null,
            changeRole,
            underRole: plan.Fold.DeltaBeside ? ColourRole.TextSecondary : changeRole);
        _price.Visibility = Visibility.Visible;

        // [S-1309] The session names the exchange's zone, so "closed" reads against its clock.
        var label = MarketQuote.PhaseLabel(phase);
        ToolTipService.SetToolTip(_price, label.Length == 0 ? null : StocksStr.WidgetsC_Stocks_PhaseTooltip.Format(("phase", label), ("zone", ExchangeTime.ZoneNote(quote.ZoneName))));

        // The rows the plan has room for, the holding first.
        var rows = plan.Rows;
        _holdingRow.Visibility = holding && rows > 0 ? Visibility.Visible : Visibility.Collapsed;
        var left = rows - (holding ? 1 : 0);
        _extendedRow.Visibility = extendedShown && left > 0 ? Visibility.Visible : Visibility.Collapsed;
        _facts.Visibility = rows > 0 ? Visibility.Visible : Visibility.Collapsed;

        _chart.Visibility = plan.Chart ? Visibility.Visible : Visibility.Collapsed;
        _chart.Margin = new Thickness(0, FamilySpace.S2, 0, 0);
        if (plan.Chart)
        {
            _labels.Visibility = plan.Labels ? Visibility.Visible : Visibility.Collapsed;
            UpdateLabels(quote);
            RedrawChart();
        }
    }

    /// <summary>[S-6734] No quote: the symbol refused, nothing quoted for it, or how the sync went; not set up is the card's Set up row ([S-1862]).</summary>
    private void ShowState()
    {
        _price.Visibility = Visibility.Collapsed;
        _facts.Visibility = Visibility.Collapsed;
        _chart.Visibility = Visibility.Collapsed;
        _header.Clear();

        switch (_status)
        {
            case FetchStatus.Refused:
                _state.Show(StatusKind.Problem, Glyphs.Stocks, StocksStr.WidgetsC_Stocks_NotSymbol.Format(("symbol", _config.Symbol)));
                break;
            case FetchStatus.Empty:
                _state.Show(StatusKind.Empty, Glyphs.Stocks, StocksStr.WidgetsC_Stocks_NoQuote.Text());
                break;
            default:
                OnlineParts.ShowState(_state, _status, Glyphs.Stocks, () => Reload(force: true));
                break;
        }
    }

    /// <summary>
    /// [S-2777] The holding's row: "10 held" and what they are worth now with how much that moved over
    /// the period, in the quote's currency ("4,202.68 USD +82.68"; the move from 300 wide). False when
    /// there is no holding or nothing to price it.
    /// </summary>
    private bool ShowHolding(TickerQuote quote)
    {
        var locale = Strings.Current;
        if (_config.SharesOwned is not { } owned || StockHolding.Worth(owned, quote.Price, quote.Currency) is not { } worth)
        {
            return false;
        }

        var move = StockHolding.Change(owned, quote.Price, quote.ChangePercent, quote.Currency);
        _holdingRow.Show(StockHolding.Held(locale, owned), StockHolding.Value(locale, worth, move, _area.Width));
        return true;
    }

    /// <summary>[S-1308] The extended print's row: "After hours · 16:42" and "412.10 −0.36%". False when there is none.</summary>
    private bool ShowExtended(TickerQuote quote, ExtendedPrint? print, MarketPhase phase, DateTimeOffset now)
    {
        if (print is not { } p)
        {
            return false;
        }

        var name = phase == MarketPhase.Pre || (phase != MarketPhase.Post && quote.Sessions.Pre is { } pre && p.At >= pre.Start && p.At < pre.End)
            ? Str.Stocks_PreMarket.Text()
            : Str.Stocks_AfterHours.Text();
        var at = ExchangeTime.Label(p.At, quote.ExchangeOffset, QuoteRange.Day, now.Offset);
        _extendedRow.Show($"{name} · {at}", $"{MarketQuote.Price(p.Price, quote.PriceHint)} {MarketQuote.ChangePercent(p.ChangePercent)}");
        return true;
    }

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
