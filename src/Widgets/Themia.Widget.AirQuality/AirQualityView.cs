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
using Themia.Widget.AirQuality;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5100] Air quality at a place, on the family's blocks: the index as the Readout with its band as
/// the line under it, then UV, PM2.5, pollen, PM10, ozone, the other index and each pollen in the air
/// as fact rows ([S-5107], <see cref="AirLayout.Number"/>). The band is written out, and the index is in
/// Caution or Critical for the worse bands ([S-5104]: never green, the colour never the only carrier).
/// The gauge style ([S-6170]) puts the index on the Chart's half ring instead.
/// </summary>
internal sealed partial class AirQualityView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetAligned
{
    /// <summary>[S-7064] The header's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitHeader _header;
    private readonly ReadoutBlock _hero = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly StackPanel _facts = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<RowBlock> _factRows = [];

    /// <summary>[S-6734] Without a reading: why, in one row at the top. Collapsed until then (a view waiting for setup is never attached).</summary>
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };

    private Ticker? _ticker;
    private AirQualityConfig _config = new();
    private AirQualityConfig _applied = new();
    private IWidgetSurface? _surface;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval;
    private readonly IWidgetHost _host;
    private IWeatherAnswers? _web;
    private AirInterest? _want;
    private FetchStatus _status = FetchStatus.Loading;
    private Fetched<AirReading>? _reading;
    private WidgetArea _area;

    /// <summary>[S-6583] Where the index sits: from the start in the number style, centred on the gauge by default.</summary>
    private ContentAlign _align = ContentAlign.Left;
    private bool _detached;

    public AirQualityView(IWidgetHost host)
    {
        _host = host;
        _header = new KitHeader(host);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

        Children.Add(_hero);
        Children.Add(_facts);
        SetColumnSpan(_empty, 2);
        SetRowSpan(_empty, 3);
        Children.Add(_empty);
        _hero.Visibility = Visibility.Collapsed;
        _facts.Visibility = Visibility.Collapsed;
    }

    public FrameworkElement? HeaderTrailing => _header;

    /// <summary>[S-730] Refresh: the network now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_reading), () => Reload(force: true))] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        _web?.Updated -= OnWebUpdated;
        _web = _host.Weather;
        _web.Updated += OnWebUpdated;

        ApplyConfig(config);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as AirQualityConfig ?? new AirQualityConfig();
        var previous = _applied;
        _config = next;
        _applied = (AirQualityConfig)next.Clone();
        PublishTitle();

        if (previous.Latitude != next.Latitude || previous.Longitude != next.Longitude)
        {
            _reading = null;
        }

        Resubscribe();
        Reload(force: false);
    }

    /// <summary>[S-6583] The index (or the gauge) at the start, the middle or the end of its column; the rows keep theirs.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        Render();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    private void OnLanguage(Locale locale)
    {
        Resubscribe();
        Reload(force: false);
    }

    /// <summary>The header's stale line counts up on the widget's own cadence; nothing is fetched from here ([S-2663]).</summary>
    private void Resubscribe()
    {
        var interval = OnlineCadence.Air(UpdateCadence.Current);
        if (_subscription is not null && interval == _subscribedInterval)
        {
            return;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(TimeSpan.FromMinutes(1), () =>
        {
            if (!_detached)
            {
                Render();
            }
        });
    }

    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        _want = _config.Interest();
        if (_want is null)
        {
            _status = FetchStatus.NotConfigured;
            _reading = null;
            Render();
            return;
        }

        _web?.Show(_want, force);
        Take();
    }

    private void OnWebUpdated(string key)
    {
        if (!_detached && _want is { } want && _web is { } web && web.Key(want) == key)
        {
            Take();
        }
    }

    private void Take()
    {
        if (_want is not { } want)
        {
            return;
        }

        var answer = _web?.Air(want);
        _reading = answer?.Value is not null ? answer : null;
        _status = answer?.Status ?? FetchStatus.Loading;
        Render();
    }

    private AirQualityScale InForce => AirQualityRules.Resolve(_config.Scale, _config.Latitude, _config.Longitude);

    /// <summary>[S-5107] The readings in priority order: UV, PM2.5, pollen (where it is forecast), PM10, ozone, the other index, the other pollens.</summary>
    private static List<(string Glyph, string Label, string Value)> Facts(AirReading air, AirQualityScale scale)
    {
        var facts = new List<(string, string, string)>(5);

        if (air.Uv is { } uv)
        {
            facts.Add((Glyphs.Sun, AirQualityStr.Online_Air_Uv.Text(), AirText.UvReading(uv)));
        }

        if (air.Pm25 is { } pm25)
        {
            facts.Add((Glyphs.Fog, AirQualityStr.Online_Air_Pm25.Text(), AirText.Concentration(pm25)));
        }

        if (AirQualityRules.HasPollen(air))
        {
            facts.Add((AirQualityModule.Glyph, AirQualityStr.Online_Air_Pollen.Text(), AirText.PollenSummary(AirQualityRules.Pollen(air))));
        }

        if (air.Pm10 is { } pm10)
        {
            facts.Add((Glyphs.Fog, AirQualityStr.Online_Air_Pm10.Text(), AirText.Concentration(pm10)));
        }

        if (air.Ozone is { } ozone)
        {
            facts.Add((Glyphs.Globe, AirQualityStr.Online_Air_Ozone.Text(), AirText.Concentration(ozone)));
        }

        // [S-5107] The large tiles' rungs: the other agency's index, then the other pollens in the air.
        var other = scale == AirQualityScale.Us ? AirQualityScale.European : AirQualityScale.Us;
        if (AirQualityRules.Index(air, other) is { } otherIndex)
        {
            facts.Add((AirQualityModule.Glyph, AirText.ScaleUnit(other), AirText.Index(otherIndex)));
        }

        foreach (var (type, level) in AirQualityRules.Pollen(air).Skip(1))
        {
            facts.Add((AirQualityModule.Glyph, AirText.Pollen(type), AirText.Level(level)));
        }

        return facts;
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var scale = InForce;
        if (_reading is not { Value: { } air } held || AirQualityRules.Index(air, scale) is not { } index)
        {
            // [S-6734] Not set up is the card's Set up row; an answer without this index is one with nothing in it.
            OnlineParts.ShowState(_empty, _reading is not null ? FetchStatus.Empty : _status, AirQualityModule.Glyph, () => Reload(force: true));
            _hero.Visibility = _facts.Visibility = Visibility.Collapsed;
            HideGauge();
            _header.Clear();
            return;
        }

        _empty.Hide();
        _header.Show(OnlineParts.Problems(held), busy: false);
        if (!_area.IsMeasured)
        {
            return;
        }

        var band = AirQualityRules.Band(scale, index);
        var tone = AirQualityRules.Tone(band);
        var reading = new AirShown(AirText.Index(index), " " + AirText.ScaleUnit(scale), AirText.Band(scale, band), AirQualityRules.ValueRole(tone), AirQualityRules.ScalePosition(scale, index), AirQualityRules.ArcRole(tone));
        var facts = Facts(air, scale);

        // [S-6169] The gauge style draws the same reading on the Chart's half ring.
        if (_config.Style == AirQualityStyle.Gauge)
        {
            _hero.Visibility = Visibility.Collapsed;
            LayGauge(reading, facts);
            return;
        }

        HideGauge();
        var plan = AirLayout.Number(_area, facts.Count);
        Columns(plan.Side, plan.HeroColumn);
        _hero.Visibility = Visibility.Visible;
        SetRowSpan(_hero, plan.Side ? 3 : 1);
        _hero.Margin = plan.Side ? new Thickness(FamilySpace.Inset, 0, 0, 0) : Family.SideInset;
        _hero.HorizontalAlignment = Aligned.Horizontal(_align);
        var (width, height) = MeterPlan.Content(_area);
        _hero.Size(_area, plan.Side ? plan.HeroColumn - FamilySpace.Inset : width, height, AirLayout.Worst);
        _hero.Show(reading.Value, reading.Unit, reading.Band, valueRole: reading.Role);
        LayFacts(facts, plan.Facts, plan.Side);
    }

    /// <summary>The index, its unit, its band and its roles, as the two styles draw them.</summary>
    private readonly record struct AirShown(string Value, string Unit, string Band, ColourRole Role, double Position, ColourRole Arc);

    /// <summary>The figure column and the rows' column: two fifths and the rest on the side, one column otherwise.</summary>
    private void Columns(bool side, double column)
    {
        ColumnDefinitions[0].Width = side ? new GridLength(column) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    /// <summary>The readings as fact rows: 8 under the figure's own row, or beside it from the top. They carry their own side inset.</summary>
    private void LayFacts(List<(string Glyph, string Label, string Value)> facts, int count, bool side)
    {
        _facts.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_facts, side ? 0 : 1);
        SetColumn(_facts, side ? 1 : 0);
        SetRowSpan(_facts, side ? 3 : 1);
        _facts.Margin = new Thickness(0, side ? 0 : FamilySpace.S2, 0, 0);

        while (_factRows.Count < count)
        {
            var row = new RowBlock(RowKind.Fact);
            _factRows.Add(row);
            _facts.Children.Add(row);
        }

        for (var i = 0; i < _factRows.Count; i++)
        {
            var shown = i < count && i < facts.Count;
            _factRows[i].Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            if (shown)
            {
                var (glyph, label, value) = facts[i];
                _factRows[i].LeadGlyph(glyph);
                _factRows[i].Show(label, value);
            }
        }
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web = null;
        }

        _ticker = null;
    }

    /// <summary>[S-728] The city as the header title, or the widget's label when hidden.</summary>
    private void PublishTitle() => _surface?.SetHeaderTitle(_config.Title);
}
