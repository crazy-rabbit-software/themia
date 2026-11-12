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
using Themia.Widget.WeatherNow;

namespace Themia.App.Widgets;

/// <summary>
/// The weather where the user says they are, from open-meteo, on the family's blocks ([S-723]): the
/// temperature as the Readout, led by the sky's glyph, the condition under it; today's range and the
/// readings as fact rows, beside it on an expanded width, else under it. Where each part goes is
/// <see cref="WeatherNowLayout"/>'s (the detail widgets' plan); the large-icon and minimal styles are
/// <see cref="WeatherNowStyles"/>' ([S-6160]). The next hours and days are the forecast widget's,
/// never drawn here.
/// <para>
/// [S-7042] A reading kept through a failed refresh stays on screen and the header's problem glyph says
/// so; with no reading at all the Status block's one row at the top says why ([S-6734]).
/// </para>
/// </summary>
internal sealed partial class WeatherNowView : Grid, IWidgetView, IWidgetOpens, IWidgetHeaderContent,
    IWidgetSizeAware, IWidgetContextActions, IWidgetAligned
{
    /// <inheritdoc />
    public CardOpen? Opens => WeatherNowModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    /// <summary>[S-7064] The header's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitHeader _header;

    private readonly ReadoutBlock _hero = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly StackPanel _facts = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<RowBlock> _factRows = [];

    /// <summary>[S-6734] Without a reading: why, in one row at the top. Collapsed until then (a view waiting for setup is never attached).</summary>
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };

    private Ticker? _ticker;
    private WeatherNowConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the properties menu edits the live config object in place, so by
    /// the time a change arrives the "previous" config already holds the new values.
    /// </summary>
    private WeatherNowConfig _applied = new();

    /// <summary>The card this view draws on, which owns the header it names.</summary>
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private readonly IWidgetHost _host;
    private IWeatherAnswers? _web;

    /// <summary>What the widget asks for now, or null without a place ([S-2663]).</summary>
    private WeatherInterest? _want;

    /// <summary>The state to say when there is no reading: not set up, or how the last sync went.</summary>
    private FetchStatus _status = FetchStatus.Loading;

    private Fetched<CurrentWeather>? _reading;
    private IReadOnlyList<WeatherFact> _readings = [];
    private string _number = string.Empty;
    private string _unit = string.Empty;
    private string _condition = string.Empty;
    private string _sky = Glyphs.Weather;
    private WidgetArea _area;

    /// <summary>[S-6583] Where the reading sits: the widget's alignment, from the start by default in the standard style.</summary>
    private ContentAlign _align = ContentAlign.Left;
    private bool _detached;

    public WeatherNowView(IWidgetHost host)
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

    /// <summary>The header's problem glyph. Built in the constructor because the card reads this once, when it builds that header.</summary>
    public FrameworkElement? HeaderTrailing => _header;

    /// <summary>[S-730] "Refresh": the network now, past both the back-off and the cache.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_reading), () => Reload(force: true))] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        // [S-2663] The reading comes from the background sync; the widget only hears it.
        _web?.Updated -= OnWebUpdated;
        _web = _host.Weather;
        _web.Updated += OnWebUpdated;

        ApplyConfig(config);
    }

    /// <summary>
    /// The language changed: every text on the tile is rebuilt from the reading in hand. [S-3770] [S-3780]
    /// It is also how a change to the shared temperature scale or cadence arrives: the reading is asked for
    /// again under the new scale (the sync holds both, keyed apart).
    /// </summary>
    private void OnLanguage(Locale locale)
    {
        Resubscribe();
        Reload(force: false);
    }

    /// <summary>[S-3770] The scale of the reading in hand: the one it was asked in.</summary>
    private TemperatureUnit Unit => _want?.Unit ?? RegionFormat.Current.Temperature;

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as WeatherNowConfig ?? new WeatherNowConfig();
        var previous = _applied;
        _config = next;
        _applied = (WeatherNowConfig)next.Clone();
        PublishPlace();

        // A different place is a different reading: the one in hand must not linger under the new heading.
        if (previous.Latitude != next.Latitude || previous.Longitude != next.Longitude)
        {
            _reading = null;
        }

        // [S-2663] Always through the sync: it asks nothing for a reading already held and fresh,
        // so a toggle that only changes the drawing costs no request.
        Resubscribe();
        Reload(force: false);
    }

    /// <summary>[S-6583] The reading at the start, the middle or the end of its column; the rows keep theirs.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        Lay();
    }

    /// <summary>[S-663] The card's word on how much room the widget has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>Rebuild the ticker subscription only when the cadence actually changed.</summary>
    private void Resubscribe()
    {
        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, OnTick);
    }

    /// <summary>A tick only redraws (the header's stale line). [S-2663] The reading is kept fresh by the sync, never fetched from here.</summary>
    private void OnTick()
    {
        if (!_detached)
        {
            Render();
        }
    }

    /// <summary>
    /// [S-2663] Tell the sync what is on screen and draw what it holds. Nothing is fetched for a
    /// reading already held and fresh; <paramref name="force"/> (the user's Refresh) asks now.
    /// </summary>
    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        _want = WeatherInterests.For(_config);
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

    /// <summary>[S-2663] A reading's answer or state changed in the sync service.</summary>
    private void OnWebUpdated(string key)
    {
        if (!_detached && _want is { } want && _web is { } web && web.Key(want) == key)
        {
            Take();
        }
    }

    /// <summary>What the sync holds for the place: the newest reading (kept through a failure), or the state to say when there is none.</summary>
    private void Take()
    {
        if (_want is not { } want)
        {
            return;
        }

        var answer = _web?.Reading(want);
        _reading = answer?.Value is not null ? answer : null;
        _status = answer?.Status ?? FetchStatus.Loading;
        Render();
    }

    /// <summary>Draw what is in hand; with no reading, say which state it is ([S-6734]): a blank weather tile is indistinguishable from a broken one.</summary>
    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var held = _reading;
        if (held?.Value is not { } reading)
        {
            OnlineParts.ShowState(_empty, _status, Glyphs.Weather, () => Reload(force: true));
            _hero.Visibility = Visibility.Collapsed;
            _facts.Visibility = Visibility.Collapsed;
            HideStyled();
            _header.Clear();
            return;
        }

        _empty.Hide();
        (_number, _unit) = WeatherReport.TemperatureParts(reading.Temperature, Unit);
        _condition = WeatherReport.Describe(reading.Code);
        _sky = WeatherGlyph.Of(WeatherReport.Look(reading.Code));
        _readings = WeatherFacts.For(reading, Unit);
        _header.Show(OnlineParts.Problems(held), busy: false);
        Lay();
    }

    /// <summary>[S-723] Plan the face for the area and the readings in hand, then fill it.</summary>
    private void Lay()
    {
        if (_detached || _reading?.Value is null || !_area.IsMeasured)
        {
            return;
        }

        // [S-6160] The other styles draw the same reading in a panel of their own.
        if (_config.Style != WeatherNowStyle.Standard)
        {
            _hero.Visibility = Visibility.Collapsed;
            _facts.Visibility = Visibility.Collapsed;
            LayStyled();
            return;
        }

        HideStyled();
        var plan = WeatherNowLayout.Plan(_area, _readings.Count, _config.ShowDetails);

        ColumnDefinitions[0].Width = plan.Side ? new GridLength(plan.HeroColumn) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = plan.Side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        // The reading at the view's top, in the inset (on the side its column holds the inset on its left only).
        _hero.Visibility = Visibility.Visible;
        SetRowSpan(_hero, plan.Side ? 3 : 1);
        _hero.Margin = plan.Side ? new Thickness(FamilySpace.Inset, 0, 0, 0) : Family.SideInset;
        _hero.HorizontalAlignment = Aligned.Horizontal(_align);
        var (width, height) = MeterPlan.Content(_area);
        _hero.Size(_area, plan.Side ? plan.HeroColumn - FamilySpace.Inset : width, height, WeatherNowLayout.Worst);
        _hero.Show(_number, _unit, _condition, lead: _sky);

        // The rows: under the reading, 8 down, or beside it from the top. They carry their own side inset.
        _facts.Visibility = plan.Facts > 0 ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_facts, plan.Side ? 0 : 1);
        SetColumn(_facts, plan.Side ? 1 : 0);
        SetRowSpan(_facts, plan.Side ? 3 : 1);
        _facts.Margin = new Thickness(0, plan.Side ? 0 : FamilySpace.S2, 0, 0);

        while (_factRows.Count < plan.Facts)
        {
            var row = new RowBlock(RowKind.Fact);
            _factRows.Add(row);
            _facts.Children.Add(row);
        }

        for (var i = 0; i < _factRows.Count; i++)
        {
            var shown = i < plan.Facts && i < _readings.Count;
            _factRows[i].Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            if (shown)
            {
                var fact = _readings[i];
                _factRows[i].LeadGlyph(FactGlyph(fact.Kind));
                _factRows[i].Show(fact.Label, fact.Value);
            }
        }
    }

    /// <summary>The glyph for a reading, checked against the font ([S-733]).</summary>
    private static string FactGlyph(WeatherFactKind kind) => kind switch
    {
        WeatherFactKind.Today => Glyphs.Calendar,
        WeatherFactKind.FeelsLike => Glyphs.Thermometer,
        WeatherFactKind.Humidity or WeatherFactKind.Precipitation => Glyphs.Droplet,
        WeatherFactKind.Wind or WeatherFactKind.Gusts => Glyphs.Wind,
        WeatherFactKind.Pressure => Glyphs.Gauge,
        WeatherFactKind.Uv or WeatherFactKind.Sunrise => Glyphs.Sun,
        WeatherFactKind.Sunset => Glyphs.Moon,
        WeatherFactKind.CloudCover => Glyphs.Cloudy,
        _ => Glyphs.Info,
    };

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

    /// <summary>[S-728] The city as the header title, or the widget's label when the user hid it.</summary>
    /// <summary>[S-2260] The place is the header's label beside the widget's name; no place, no label ([S-2848]).</summary>
    private void PublishPlace() => _header.SetAbout(_config.PlaceLabel);
}
