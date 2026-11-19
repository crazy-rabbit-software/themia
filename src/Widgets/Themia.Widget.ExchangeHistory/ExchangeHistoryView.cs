using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
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
internal sealed partial class ExchangeHistoryView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetContextActions
{
    private readonly KitStatus _mark;
    /// <summary>[S-6717] [S-11002] A history band: edge to edge at the sides and the foot, as the stock's; the top is the card's (the inset).</summary>
    private readonly Chart _chart = new() { FlowDirection = FlowDirection.LeftToRight };
    private readonly IWidgetHost _host;
    private IRateAnswers? _web;
    private IWidgetSurface? _surface;
    private ExchangeHistoryConfig _config = new();
    private Fetched<RateTable>? _table;
    private FetchStatus _status = FetchStatus.Loading;
    private bool _detached;

    public ExchangeHistoryView(IWidgetHost host)
    {
        _host = host;
        _mark = new KitStatus(host);
        Children.Add(_chart);
        _mark.Place(this);
        JoinInstants();
    }

    public IReadOnlyList<WidgetAction> ContextActions =>
        [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_table), () => Reload(force: true))];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _web?.Updated -= OnWebUpdated;
        _web = _host.Rates;
        _web.Updated += OnWebUpdated;
        ApplyConfig(config);
        _links.Join(_surface?.Links);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ExchangeHistoryConfig ?? new ExchangeHistoryConfig();
        Reload(force: false);
    }

    public void Detach()
    {
        _detached = true;
        _links.Leave();
        Strings.Changed -= OnLanguage;
        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web = null;
        }
    }

    private void OnLanguage(Locale locale)
    {
        Reload(force: false);
    }

    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        _web?.Show(force);
        Take();
    }

    private void OnWebUpdated(string key)
    {
        if (!_detached && _web is { } web && key == web.Key)
        {
            Take();
        }
    }

    private void Take()
    {
        var answer = _web?.Rates();
        _table = answer?.Value is not null ? answer : null;
        _status = answer?.Status ?? FetchStatus.Loading;
        Render();
    }

    private void Render()
    {
        var rate = _table?.Value is { } table ? ExchangeRules.Cross(table, _config.EffectiveBase, _config.EffectiveQuote) : null;
        _shownRate = rate;
        if (_table is { } held)
        {
            _mark.Show(OnlineParts.Problems(held), busy: false);
        }
        else
        {
            _mark.Clear();
        }

        // [S-6753] With no table yet, or no rate for the pair, the chart is drawn at its baseline with why as its caption.
        var caption = rate is { History.Count: >= 2 } ? string.Empty
            : _table is null ? OnlineParts.Caption(_status) : Str.Online_Rates_Missing.Text();
        _chart.Role(rate?.ChangePercent < 0 ? ColourRole.Critical : ColourRole.Accent);
        var history = rate is { History.Count: >= 2 } ? rate.History : [];
        var days = rate?.Days is { } known && known.Count == history.Count ? known : null;
        var looks = (ChartLooks.Effective(_config), ChartLooks.EffectiveDetail(_config));
        _chart.Show(size => Layout(history, days, looks.Item1, looks.Item2, caption, size.Width));
        MarkInstant();
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
