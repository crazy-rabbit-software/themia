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
/// [S-6717]): edge to edge and down to the card's foot under the inset, the accent while the price rose
/// or held, Critical while it fell (the price atom's colour), the start and end times always, the high and
/// low where switched on. It reads the quote the price atom reads (<see cref="SkyFeed{TWant, T}"/>), so
/// the two agree; a header it is given names the instrument.
/// </summary>
internal sealed partial class StockHistoryView : Grid, IWidgetView, IWidgetOpens, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => _config.Page();

    private readonly KitHeader _header;
    private readonly StatusBlock _state = new() { Visibility = Visibility.Collapsed };
    private readonly SkyFeed<QuoteInterest, TickerQuote> _feed;
    private StockHistoryConfig _config = new();
    private IWidgetSurface? _surface;

    /// <summary>The chart's role: Critical while the price falls, else the accent.</summary>
    private bool _falling;

    public StockHistoryView(IWidgetHost host)
    {
        _header = new KitHeader(host);
        _feed = new(
            () => new(h => host.Quotes.Updated += h, h => host.Quotes.Updated -= h, host.Quotes.Key, host.Quotes.Quote, host.Quotes.Show),
            () => _config.Interest(),
            Render);
        BuildChart();

        // [S-11600] The sides and the foot are edge to edge; the top is the card's (the inset, or the header's gap).
        _chart.Visibility = Visibility.Collapsed;
        Children.Add(_chart);
        Children.Add(_state);
    }

    /// <summary>The quote in hand.</summary>
    private Fetched<TickerQuote>? _quote => _feed.Held;

    public FrameworkElement? HeaderTrailing => _header;

    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.EffectiveSymbol.Length == 0 ? [] : [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as StockHistoryConfig ?? new StockHistoryConfig();
        _feed.Attach(ticker, _config.TickInterval);
        ApplyConfig(config);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as StockHistoryConfig ?? new StockHistoryConfig();
        PublishTitle();
        _feed.Reload();
    }

    public void Detach()
    {
        _feed.Detach();
        DetachChart();
    }

    private void Render()
    {
        PublishTitle();
        if (_feed.Held is not { Value: { } quote } held || quote.Bars.Count < 2)
        {
            _chart.Visibility = Visibility.Collapsed;
            _header.Clear();
            StockQuoteLines.ShowState(_state, _feed.Held is not null ? FetchStatus.Empty : _feed.Want is null ? StockQuoteLines.Unasked(_config) : _feed.Status, _config.Symbol, _feed.Refresh);
            return;
        }

        _state.Hide();
        _header.Show(OnlineParts.Problems(held), busy: false);
        _falling = quote.ChangePercent < 0;
        _chart.Visibility = Visibility.Visible;
        _labels.Visibility = Visibility.Visible;
        UpdateLabels(quote);
        RedrawChart();
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
