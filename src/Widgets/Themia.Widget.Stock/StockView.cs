using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Stock;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11160] One instrument's price as a reading, on the family's blocks: the price a Readout on 28 at the
/// top with its currency and its change ([S-6707]: beside it, or on the line under it), the session the
/// line under it otherwise; the holding and the extended-hours print as fact rows under it where the
/// config has them ([S-2777], [S-1308]). The change is secondary while the price rises or holds, Critical
/// while it falls (no green, [S-6706]). The chart is the history atom's; both read one quote through
/// <see cref="SkyFeed{TWant, T}"/>.
/// </summary>
internal sealed partial class StockView : Grid, IWidgetView, IWidgetOpens, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetAligned
{
    /// <inheritdoc />
    public CardOpen? Opens => _config.Page();

    /// <summary>[S-7064] The header's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitHeader _header;
    private readonly ReadoutBlock _price = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = Family.SideInset };

    /// <summary>[S-2777] [S-1308] The holding and the extended print: fact rows under the price.</summary>
    private readonly StackPanel _facts = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, FamilySpace.S2, 0, 0) };
    private readonly RowBlock _holdingRow = new(RowKind.Fact, lead: 0);
    private readonly RowBlock _extendedRow = new(RowKind.Fact, lead: 0);

    /// <summary>[S-6734] No quote to draw: the Status row at the top.</summary>
    private readonly StatusBlock _state = new() { Visibility = Visibility.Collapsed };
    private readonly SkyFeed<QuoteInterest, TickerQuote> _feed;
    private StockConfig _config = new();
    private IWidgetSurface? _surface;
    private WidgetArea _area;

    public StockView(IWidgetHost host)
    {
        FollowInstants();
        _header = new KitHeader(host);
        _feed = new(
            () => new(h => host.Quotes.Updated += h, h => host.Quotes.Updated -= h, host.Quotes.Key, host.Quotes.Quote, host.Quotes.Show),
            () => _config.Interest(),
            Render);
        _price.Use(FamilyType.Big28);
        _price.UnderText.TextTrimming = TextTrimming.CharacterEllipsis;

        // [S-11161] The line under always holds its place, said or not: the rows never move up into it.
        _price.MinHeight = FamilyType.Big28.LineHeight + FamilyType.Under(FamilyType.Big28).LineHeight;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _facts.Children.Add(_holdingRow);
        _facts.Children.Add(_extendedRow);
        SetRow(_facts, 1);
        SetRowSpan(_state, 2);
        Children.Add(_price);
        Children.Add(_facts);
        Children.Add(_state);
    }

    /// <summary>The header's problem glyph. Built in the constructor because the card reads this once, when it builds that header.</summary>
    public FrameworkElement? HeaderTrailing => _header;

    /// <summary>[S-1311] Refresh: the network now, past the cache and any back-off.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.EffectiveSymbol.Length == 0 ? [] : [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as StockConfig ?? new StockConfig();

        // [S-1311] The session follows the clock, not the refresh: with a 30-minute refresh it must still
        // say "Market closed" at the bell. A render, never a fetch.
        _feed.Attach(ticker, TimeSpan.FromMinutes(1));
        ApplyConfig(config);
        _links.Join(_surface?.Links);
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    /// <summary>[S-6583] The price across the card: the widget's alignment, from the start by default ([S-6582]).</summary>
    public void ApplyAlign(ContentAlign align) => _price.HorizontalAlignment = Aligned.Horizontal(align);

    /// <summary>The box's width only decides the fold of the change and the holding's move.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as StockConfig ?? new StockConfig();
        PublishTitle();
        _feed.Reload();
    }

    public void Detach()
    {
        _links.Leave();
        _standsAt = null;
        _feed.Detach();
    }

    private void Render()
    {
        PublishTitle();
        if (_feed.Held is not { Value: { } quote } held)
        {
            _price.Visibility = Visibility.Collapsed;
            _facts.Visibility = Visibility.Collapsed;
            _header.Clear();
            StockQuoteLines.ShowState(_state, _feed.Want is null ? StockQuoteLines.Unasked(_config) : _feed.Status, _config.Symbol, _feed.Refresh);
            return;
        }

        _state.Hide();
        _header.Show(OnlineParts.Problems(held), busy: false);
        var now = DateTimeOffset.Now;
        var price = MarketQuote.Price(quote.Price, quote.PriceHint);
        var currency = quote.Currency.Length > 0 ? " " + quote.Currency : string.Empty;
        var change = MarketQuote.ChangePercent(quote.ChangePercent);
        var falling = quote.ChangePercent < 0;
        var phase = MarketQuote.Phase(quote.Sessions, now);
        var extended = _config.WantsPrePost
            ? MarketQuote.CurrentExtended(MarketQuote.ExtendedQuotesOf(quote.Bars, quote.Sessions, quote.Price), phase)
            : null;
        var width = _area.IsMeasured ? Math.Max(0, _area.Width - (2 * FamilySpace.Inset)) : double.PositiveInfinity;
        ShowHolding(quote);
        var extendedShown = ShowExtended(quote, extended, phase, now);

        // [S-11800] The session under the price where the config asks for it, unless the extended print's row names it already.
        var session = !_config.ShowsSession || extendedShown ? string.Empty : MarketQuote.SessionLine(phase);
        var fold = StockLayout.Fold(new ReadoutText(price, currency), width);
        var changeRole = falling ? ColourRole.Critical : ColourRole.TextSecondary;
        _price.Show(
            price,
            fold.Unit ? currency : string.Empty,
            fold.DeltaBeside ? session : change,
            fold.DeltaBeside ? change : null,
            changeRole,
            underRole: fold.DeltaBeside ? ColourRole.TextSecondary : changeRole);
        _price.Visibility = Visibility.Visible;

        // [S-11912] At a moment pointed at on a history of this symbol: the price then, the moment under it.
        if (PriceAtInstant(quote) is { } then)
        {
            _price.Show(then.Price, fold.Unit ? currency : string.Empty, then.Caption);
        }

        // [S-1309] The session names the exchange's zone, so "closed" reads against its clock.
        var label = MarketQuote.PhaseLabel(phase);
        ToolTipService.SetToolTip(_price, label.Length == 0 ? null : StockStr.WidgetsC_Stocks_PhaseTooltip.Format(("phase", label), ("zone", ExchangeTime.ZoneNote(quote.ZoneName))));

        // [S-11161] A row each the config has, the same with or without a value to show in it.
        _holdingRow.Visibility = _config.SharesOwned is not null ? Visibility.Visible : Visibility.Collapsed;
        _extendedRow.Visibility = _config.WantsPrePost ? Visibility.Visible : Visibility.Collapsed;
        _facts.Visibility = _config.Rows > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// [S-2777] The holding's row: "10 held" and what they are worth now with how much that moved over the
    /// period, in the quote's currency ("4,202.68 USD +82.68"; the move from 300 wide). The dash where
    /// there is nothing to price it.
    /// </summary>
    private bool ShowHolding(TickerQuote quote)
    {
        var locale = Strings.Current;
        if (_config.SharesOwned is not { } owned)
        {
            return false;
        }

        if (StockHolding.Worth(owned, quote.Price, quote.Currency) is not { } worth)
        {
            _holdingRow.Show(StockHolding.Held(locale, owned), "—");
            return false;
        }

        var move = StockHolding.Change(owned, quote.Price, quote.ChangePercent, quote.Currency);
        _holdingRow.Show(StockHolding.Held(locale, owned), StockHolding.Value(locale, worth, move, _area.IsMeasured ? _area.Width : 0));
        return true;
    }

    /// <summary>[S-1308] The extended print's row: "After hours · 16:42" and "412.10 −0.36%"; the dash while there is none.</summary>
    private bool ShowExtended(TickerQuote quote, ExtendedPrint? print, MarketPhase phase, DateTimeOffset now)
    {
        if (print is not { } p)
        {
            _extendedRow.Show(Str.Stocks_ExtendedHours.Text(), "—");
            return false;
        }

        var name = phase == MarketPhase.Pre || (phase != MarketPhase.Post && quote.Sessions.Pre is { } pre && p.At >= pre.Start && p.At < pre.End)
            ? Str.Stocks_PreMarket.Text()
            : Str.Stocks_AfterHours.Text();
        var at = ExchangeTime.Label(p.At, quote.ExchangeOffset, QuoteRange.Day, now.Offset);
        _extendedRow.Show($"{name} · {at}", $"{MarketQuote.Price(p.Price, quote.PriceHint)} {MarketQuote.ChangePercent(p.ChangePercent)}");
        return true;
    }

    /// <summary>The header title: the user's name, else the service's own name for the instrument, else the symbol.</summary>
    private void PublishTitle()
    {
        var title = !string.IsNullOrWhiteSpace(_config.DisplayName)
            ? _config.DisplayName
            : _feed.Held?.Value is { Name.Length: > 0 } quote && _config.Market == QuoteMarket.Listed
                ? quote.Name
                : _config.EffectiveSymbol;
        _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(title) ? null : title);
    }
}
