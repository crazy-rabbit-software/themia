using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.ExchangeHistory;

/// <summary>
/// [S-11155] One pair's 90 days on the Chart block's series ([S-6717]), a history band edge to edge:
/// the line and its wash in the accent while the latest day rose or held, Critical while it fell (the
/// rate atom's colour, [S-6706]). Time runs left to right in every language. The table is the one the
/// rate reads (<see cref="IRateAnswers"/>), so the two agree; a Header widget over it names the pair ([S-12481]).
/// </summary>
internal sealed partial class ExchangeHistoryView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetContextActions, IWidgetTopEdge
{
    /// <summary>[S-5700] [S-6754] The plot is drawn to the card's top edge as to its sides and foot.</summary>
    public bool OwnsTopEdge => true;

    private readonly KitStatus _mark;
    /// <summary>[S-6717] [S-11002] [S-6754] [S-6756] A history band, the price panel: the card's edge to edge, its top edge too, as the stock's.</summary>
    private readonly PriceChart _chart = new();
    /// <summary>[S-11171] The rates table the exchange rate reads too, followed as every sky and money atom follows its answer.</summary>
    private readonly SkyFeed<RatesInterest, RateTable> _feed;
    private IWidgetSurface? _surface;
    private ExchangeHistoryConfig _config = new();

    public ExchangeHistoryView(IWidgetHost host)
    {
        _mark = new KitStatus(host);
        _feed = new(() => SkyFeeds.Rates(host.Rates), () => new RatesInterest(OnlineCadence.Rates), Render);
        Children.Add(_chart);
        _mark.Place(this);
        JoinInstants();
    }

    public IReadOnlyList<WidgetAction> ContextActions =>
        [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // [S-7042] The problem glyph's staleness moves between fetches; a minute is fine enough for it.
        _feed.Attach(ticker, TimeSpan.FromMinutes(1));
        ApplyConfig(config);
        _links.Join(_surface?.Links);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ExchangeHistoryConfig ?? new ExchangeHistoryConfig();
        _feed.Reload();
    }

    public void Detach()
    {
        _links.Leave();
        _feed.Detach();
        _chart.Clear();
    }

    private void Render()
    {
        var rate = _feed.Held?.Value is { } table ? ExchangeRules.Cross(table, _config.EffectiveBase, _config.EffectiveQuote) : null;
        _shownRate = rate;
        if (_feed.Held is { } held)
        {
            _mark.Show(OnlineParts.Problems(held), busy: false);
        }
        else
        {
            _mark.Clear();
        }

        // [S-6753] With no table yet, or no rate for the pair, the chart is drawn at its baseline with why as its caption.
        var caption = rate is { History.Count: >= 2 } ? string.Empty
            : _feed.Held is null ? OnlineParts.Caption(_feed.Status) : Str.Online_Rates_Missing.Text();
        _chart.Series.Role(rate?.ChangePercent < 0 ? ColourRole.Critical : ColourRole.Accent);
        var history = rate is { History.Count: >= 2 } ? rate.History : [];
        var days = rate?.Days is { } known && known.Count == history.Count ? known : null;
        var looks = (ChartLooks.Effective(_config), ChartLooks.EffectiveDetail(_config));
        _chart.Series.Show(size => Layout(history, days, looks.Item1, looks.Item2, caption, size.Width));
        _chart.Mark();
    }

    /// <summary>[S-6750] [S-6751] The days evenly across the band, the oldest on the left; Detailed, their dates along the foot.</summary>
    private static ChartModel Layout(IReadOnlyList<double> history, IReadOnlyList<DateOnly>? days, ChartType type, ChartDetail detail, string caption, double w)
    {
        var n = history.Count;
        double X(int i) => n < 2 ? 0 : i * w / (n - 1);
        var times = new List<ChartTick>();
        if (detail == ChartDetail.Detailed && days is { Count: >= 2 })
        {
            var offset = DateTimeOffset.Now.Offset;
            var (start, end) = (PairRate.Noon(days[0], offset), PairRate.Noon(days[^1], offset));
            foreach (var at in ChartPlot.Times(start, end, ChartPlot.TimeRoom(w), TimeZoneInfo.Local).At)
            {
                times.Add(new ChartTick(ChartPlot.Share(at, start, end) * w, LocaleFormat.Date(Strings.Current, at.DateTime, DateStyle.MonthDayShort)));
            }
        }

        return new ChartModel
        {
            Type = type,
            Detail = detail,
            Series = [.. history.Select((v, i) => new ChartSample(X(i), v))],
            Low = n > 0 ? history.Min() : 0,
            High = n > 0 ? history.Max() : 1,
            Format = v => LocaleFormat.Number(Strings.Current, v, 4),
            Times = times,
            Caption = caption,
        };
    }
}
