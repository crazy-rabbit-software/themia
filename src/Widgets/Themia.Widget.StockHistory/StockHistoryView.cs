using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.StockHistory;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11165] One instrument's price over its period as a chart ([S-1311]'s chart, the Chart block,
/// [S-6717]): the card's edge to edge, its top edge too ([S-6754]), the accent while the price rose
/// or held, Critical while it fell (the price atom's colour), the start and end times always, the high and
/// low where switched on. It reads the quote the price atom reads (<see cref="SkyFeed{TWant, T}"/>), so
/// the two agree; a Header widget over it names the instrument ([S-12481]).
/// </summary>
internal sealed partial class StockHistoryView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetContextActions, IWidgetTopEdge
{
    /// <summary>[S-5700] [S-6754] The plot is drawn to the card's top edge as to its sides and foot.</summary>
    public bool OwnsTopEdge => true;

    /// <inheritdoc />
    public CardOpen? Opens => _config.Page();

    private readonly KitStatus _mark;
    private readonly SkyFeed<QuoteInterest, TickerQuote> _feed;
    private StockHistoryConfig _config = new();
    private IWidgetSurface? _surface;

    /// <summary>The chart's role: Critical while the price falls, else the accent.</summary>
    private bool _falling;

    public StockHistoryView(IWidgetHost host)
    {
        FollowInstants();
        _mark = new KitStatus(host);
        _feed = new(
            () => new(h => host.Quotes.Updated += h, h => host.Quotes.Updated -= h, host.Quotes.Key, host.Quotes.Quote, host.Quotes.Show),
            () => _config.Interest(),
            Render);
        BuildChart();

        // [S-6754] The plot is the card's whole box; what is written stands inside it.
        Children.Add(_chart);
        _mark.Place(this);
    }

    /// <summary>The quote in hand.</summary>
    private Fetched<TickerQuote>? _quote => _feed.Held;

    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.EffectiveSymbol.Length == 0 ? [] : [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as StockHistoryConfig ?? new StockHistoryConfig();
        _feed.Attach(ticker, _config.TickInterval);
        ApplyConfig(config);
        _links.Join(_surface?.Links);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as StockHistoryConfig ?? new StockHistoryConfig();
        _feed.Reload();
    }

    public void Detach()
    {
        _links.Leave();
        _feed.Detach();
        DetachChart();
    }

    /// <summary>[S-6753] The quote's period, or the chart at its baseline with why as its caption: never no chart.</summary>
    private void Render()
    {
        if (_feed.Held is not { Value: { } quote } held || quote.Bars.Count < 2)
        {
            _mark.Clear();
            var status = _feed.Held is not null ? FetchStatus.Empty : _feed.Want is null ? StockQuoteLines.Unasked(_config) : _feed.Status;
            RedrawChart(StockQuoteLines.Caption(status, _config.Symbol));
            return;
        }

        _mark.Show(OnlineParts.Problems(held), busy: false);
        _falling = quote.ChangePercent < 0;
        UpdateLabels(quote);
        RedrawChart();
    }
}
