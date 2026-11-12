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
using Themia.Widget.ExchangeRate;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5140] [S-11150] The ECB's reference rate of one pair as a reading, on the family's blocks: the
/// rate a Readout on 28 at the top with the quote's code and the day change ([S-6707]: beside it, or on
/// the line under it) and "per CHF" under it (<see cref="RatesLayout"/>). The change is secondary while
/// the rate rises or holds, Critical while it falls (no green, [S-6706]). The header names the pair and
/// says which day's rate this is. Its 90 days are the history atom's.
/// </summary>
internal sealed partial class ExchangeRateView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions, IWidgetAligned
{
    /// <summary>[S-5146] The rate's day ("ECB, 9 Oct"), the header's informing label beside the problem glyph.</summary>
    private readonly TextBlock _fix = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);

    /// <summary>[S-7064] The header's problem glyph ([S-7042]): the one stale rule, no age; the day gives way to the title on a small tile.</summary>
    private readonly KitHeader _header;

    private readonly ReadoutBlock _rate = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = Family.SideInset };

    /// <summary>[S-6734] No rate to draw: the Status row at the top.</summary>
    private readonly StatusBlock _state = new();

    private Ticker? _ticker;
    private IWidgetSurface? _surface;
    private ExchangeRateConfig _config = new();
    private IDisposable? _subscription;
    private readonly IWidgetHost _host;
    private IRateAnswers? _web;
    private FetchStatus _status = FetchStatus.Loading;
    private Fetched<RateTable>? _table;
    private WidgetArea _area;
    private bool _detached;

    public ExchangeRateView(IWidgetHost host)
    {
        FollowInstants();
        _host = host;
        _header = new KitHeader(host, _fix);
        _rate.Use(FamilyType.Big28);

        // Collapsed until a state is said: a view waiting for setup is never attached.
        _state.Visibility = Visibility.Collapsed;

        Children.Add(_rate);
        Children.Add(_state);
    }

    public FrameworkElement? HeaderTrailing => _header;

    public IReadOnlyList<WidgetAction> ContextActions =>
        [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_table), () => Reload(force: true))];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        _web?.Updated -= OnWebUpdated;
        _web = _host.Rates;
        _web.Updated += OnWebUpdated;

        // Only the header's day and problem move between fetches; a minute is fine enough for them.
        _subscription?.Dispose();
        _subscription = _ticker.Subscribe(TimeSpan.FromMinutes(1), () =>
        {
            if (!_detached)
            {
                Stamp();
            }
        });

        ApplyConfig(config);
        _links.Join(_surface?.Links);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    /// <summary>[S-5146] The header names the pair: "Exchange rates · CHF/EUR" (a typed title wins, as everywhere).</summary>
    private void PublishTitle() => _surface?.SetHeaderTitle($"{ExchangeRateStr.Online_Rates_Label.Text()} · {_config.EffectiveBase}/{_config.EffectiveQuote}");

    /// <summary>The base, the quote or the style: all drawn from the one table in hand.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ExchangeRateConfig ?? new ExchangeRateConfig();
        PublishTitle();
        Reload(force: false);
    }

    /// <summary>[S-6583] The Number style's rate across the card: the widget's alignment, from the start by default ([S-6582]).</summary>
    public void ApplyAlign(ContentAlign align) => _rate.HorizontalAlignment = Aligned.Horizontal(align);

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

    private void Stamp()
    {
        if (_table is not { Value: { } table } held)
        {
            _fix.Text = string.Empty;
            ToolTipService.SetToolTip(_fix, null);
            _header.Clear();
            return;
        }

        var date = LocaleFormat.Date(table.Latest.Date, DateStyle.MonthDayShort);
        _fix.Text = ExchangeRateStr.Online_Rates_Fix.Format(("date", date));
        ToolTipService.SetToolTip(_fix, ExchangeRateStr.Online_Rates_FixTooltip.Format(("date", LocaleFormat.Date(table.Latest.Date, DateStyle.Long))));
        _header.Show(OnlineParts.Problems(held), busy: false);
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var rate = _table?.Value is { } table ? ExchangeRules.Cross(table, _config.EffectiveBase, _config.EffectiveQuote) : null;
        Stamp();

        // [S-5145] [S-6734] No table yet: how the sync went; the latest day lacks one of the two: said as a state.
        if (rate is null)
        {
            _rate.Visibility = Visibility.Collapsed;
            if (_table is null)
            {
                OnlineParts.ShowState(_state, _status, ExchangeRateModule.Glyph, () => Reload(force: true));
            }
            else
            {
                _state.Show(StatusKind.Empty, ExchangeRateModule.Glyph, Str.Online_Rates_Missing.Text());
            }

            return;
        }

        _state.Hide();
        if (!_area.IsMeasured)
        {
            return;
        }

        var value = ExchangeRules.Format(rate.Rate);
        var code = " " + rate.Quote;
        var change = RatesText.Change(rate.ChangePercent);
        var falling = rate.ChangePercent < 0;
        var fold = RatesLayout.Fold(new ReadoutText(value, code), Math.Max(0, _area.Width - (2 * FamilySpace.Inset)));
        var changeRole = falling ? ColourRole.Critical : ColourRole.TextSecondary;
        var beside = fold.DeltaBeside || change.Length == 0;
        var perBase = ExchangeRateStr.Online_Rates_PerBase.Format(("code", rate.Base));
        _rate.Show(
            value,
            fold.Unit ? code : string.Empty,
            beside ? perBase : change,
            beside ? change : null,
            changeRole,
            underRole: beside ? ColourRole.TextSecondary : changeRole);
        _rate.Visibility = Visibility.Visible;

        // [S-11912] At a moment pointed at on a history of this pair: that day's rate, the day under it.
        if (RateAtInstant(rate) is { } then)
        {
            _rate.Show(then.Rate, fold.Unit ? code : string.Empty, then.Caption);
        }
    }

    public void Detach()
    {
        _detached = true;
        _links.Leave();
        _standsAt = null;
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;

        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web = null;
        }

        _ticker = null;
    }
}
