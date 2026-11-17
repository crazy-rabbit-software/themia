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
    /// <summary>[S-6717] [S-11002] A history band: edge to edge at the sides and the foot, as the stock's; the top is the card's (the inset, none where a group mate joins it).</summary>
    private readonly ChartSeries _chart = new() { FlowDirection = FlowDirection.LeftToRight, Visibility = Visibility.Collapsed };
    private readonly StatusBlock _state = new() { Visibility = Visibility.Collapsed };
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
        Children.Add(_state);
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

        if (rate is null || rate.History.Count < 2)
        {
            _chart.Visibility = Visibility.Collapsed;
            if (_table is null)
            {
                OnlineParts.ShowState(_state, _status, ExchangeHistoryModule.Glyph, () => Reload(force: true));
            }
            else
            {
                _state.Show(StatusKind.Empty, ExchangeHistoryModule.Glyph, Str.Online_Rates_Missing.Text());
            }

            return;
        }

        _state.Hide();
        _chart.Visibility = Visibility.Visible;
        _chart.Role(rate.ChangePercent < 0 ? ColourRole.Critical : ColourRole.Accent);
        _chart.Show(MarketQuote.Normalise(rate.History));
        MarkInstant();
    }
}
