using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5100] Air quality at a place: the index and its band as the hero (XS), then UV, PM2.5, pollen,
/// PM10, ozone, the other index and each pollen in the air as stat cells, lines or a side column as the tile grows ([S-5107]). The band is
/// written in a Fluent status colour and in words, so the colour is never the only carrier. The
/// gauge style ([S-6170]) puts the index on a half-circle gauge instead of the number.
/// </summary>
internal sealed partial class AirQualityView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetAligned
{
    /// <summary>The widest index this widget prints ("500") and its unit ("US AQI").</summary>
    private const double ValueCharacters = 3;

    private const double UnitCharacters = 6;

    private readonly TextBlock _freshness = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly HeroReadout _hero = new();
    private readonly TextBlock _band;
    private readonly Grid _facts = new() { ColumnSpacing = OnlineLayouts.Gap, RowSpacing = 0 };
    private readonly StackPanel _below = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly Grid _strip = new() { Margin = new Thickness(0, OnlineLayouts.Gap, 0, 0) };
    private readonly Grid _side = new() { ColumnSpacing = Fluent.Space4, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _emptyState;
    private readonly TextBlock _emptyMessage = new();

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
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _hero.Icon.Glyph = Glyphs.AirQuality;
        _band = _hero.AddLine(FluentType.Body);
        _hero.Margin = new Thickness(OnlineLayouts.Inset, OnlineLayouts.VerticalInset, OnlineLayouts.Inset, 0);
        _hero.VerticalAlignment = VerticalAlignment.Center;

        _below.Margin = new Thickness(OnlineLayouts.Inset, OnlineLayouts.Gap, OnlineLayouts.Inset, OnlineLayouts.VerticalInset);
        _below.Children.Add(_facts);
        _below.Children.Add(_strip);
        _strip.FlowDirection = FlowDirection.LeftToRight;
        SetRow(_below, 1);
        SetColumnSpan(_below, 2);

        _side.Margin = new Thickness(0, OnlineLayouts.VerticalInset, OnlineLayouts.Inset, OnlineLayouts.VerticalInset);
        SetColumn(_side, 1);
        SetRowSpan(_side, 2);

        _emptyState = Fluent.EmptyState(Glyphs.AirQuality, _emptyMessage, out _);
        SetRowSpan(_emptyState, 2);
        SetColumnSpan(_emptyState, 2);

        Children.Add(_hero);
        Children.Add(_below);
        Children.Add(_side);
        Children.Add(_emptyState);
    }

    public FrameworkElement? HeaderTrailing => _freshness;

    /// <summary>[S-730] Refresh: the network now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: true))] : [];

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

    /// <summary>[S-6583] The index (or the gauge) at the start, the middle or the end; the readings keep their grid.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        _hero.Align(align);
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

    /// <summary>The age line counts up on the widget's own cadence; nothing is fetched from here ([S-2663]).</summary>
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

    /// <summary>[S-5107] The readings in priority order: UV, PM2.5, pollen (where it is forecast), PM10, ozone.</summary>
    private static List<(string Glyph, string Label, string Value)> Facts(AirReading air, AirQualityScale scale)
    {
        var facts = new List<(string, string, string)>(5);

        if (air.Uv is { } uv)
        {
            facts.Add((Glyphs.Sun, Str.Online_Air_Uv.Text(), AirText.UvReading(uv)));
        }

        if (air.Pm25 is { } pm25)
        {
            facts.Add((Glyphs.Fog, Str.Online_Air_Pm25.Text(), AirText.Concentration(pm25)));
        }

        if (AirQualityRules.HasPollen(air))
        {
            facts.Add((Glyphs.AirQuality, Str.Online_Air_Pollen.Text(), AirText.PollenSummary(AirQualityRules.Pollen(air))));
        }

        if (air.Pm10 is { } pm10)
        {
            facts.Add((Glyphs.Fog, Str.Online_Air_Pm10.Text(), AirText.Concentration(pm10)));
        }

        if (air.Ozone is { } ozone)
        {
            facts.Add((Glyphs.Globe, Str.Online_Air_Ozone.Text(), AirText.Concentration(ozone)));
        }

        // [S-5107] The large tiles' rungs: the other agency's index, then the other pollens in the air.
        var other = scale == AirQualityScale.Us ? AirQualityScale.European : AirQualityScale.Us;
        if (AirQualityRules.Index(air, other) is { } otherIndex)
        {
            facts.Add((Glyphs.AirQuality, AirText.ScaleUnit(other),
                AirText.Index(otherIndex)));
        }

        foreach (var (type, level) in AirQualityRules.Pollen(air).Skip(1))
        {
            facts.Add((Glyphs.AirQuality, AirText.Pollen(type), AirText.Level(level)));
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
            _emptyMessage.Text = _status == FetchStatus.NotConfigured ? string.Empty
                : _reading is not null ? FetchState.Message(FetchStatus.Empty)
                : FetchState.Message(_status);
            _emptyState.Visibility = Visibility.Visible;
            _hero.Visibility = _below.Visibility = _side.Visibility = _gauge.Visibility = Visibility.Collapsed;
            _freshness.Text = string.Empty;
            return;
        }

        _emptyState.Visibility = Visibility.Collapsed;
        _hero.Visibility = Visibility.Visible;

        var band = AirQualityRules.Band(scale, index);
        var tone = Tone(AirQualityRules.Tone(band));
        _hero.SetValue(AirText.Index(index), AirText.ScaleUnit(scale), spaced: true);
        _hero.Icon.Foreground = tone;
        _band.Text = AirText.Band(scale, band);
        _band.Foreground = tone;

        OnlineParts.Stamp(_freshness, held, _subscribedInterval, note: Str.Online_Source.Format(("name", "CAMS · Open-Meteo")));
        Lay(Facts(air, scale), scale, index);
    }

    private void Lay(List<(string Glyph, string Label, string Value)> facts, AirQualityScale scale, double index)
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        // [S-6169] The gauge style draws the same reading on the meters' gauge.
        if (_config.Style == AirQualityStyle.Gauge)
        {
            LayGauge(facts, scale, index, Tone(AirQualityRules.Tone(AirQualityRules.Band(scale, index))));
            return;
        }

        HideGauge();
        _hero.Visibility = Visibility.Visible;
        var width = Math.Max(0, _area.Width - (2 * OnlineLayouts.Inset));
        var bandBeside = BandFitsBeside(width, FluentType.Subtitle);
        var floor = FluentType.Subtitle.LineHeight + (bandBeside ? 0 : FluentType.Body.LineHeight);
        var plan = AirLayout.Air(_area, floor, facts.Count);

        _facts.Children.Clear();
        _facts.ColumnDefinitions.Clear();
        _facts.RowDefinitions.Clear();
        _side.Children.Clear();
        _side.ColumnDefinitions.Clear();
        _side.RowDefinitions.Clear();
        _facts.Visibility = _side.Visibility = Visibility.Collapsed;
        _facts.RowSpacing = plan.Facts == AirFacts.Cells ? OnlineLayouts.Gap : 0;

        var shown = facts.Take(plan.Count).ToList();
        _below.Visibility = plan.Facts is AirFacts.Cells or AirFacts.Lines || plan.Strip ? Visibility.Visible : Visibility.Collapsed;
        BuildStrip(plan.Strip, scale, index);
        var heroWidth = width;

        switch (plan.Facts)
        {
            case AirFacts.Cells:
                Place(_facts, shown, plan.Columns, cells: true, columnWidth: null);
                _facts.Visibility = Visibility.Visible;
                break;

            case AirFacts.Lines:
                Place(_facts, shown, 1, cells: false, columnWidth: null);
                _facts.Visibility = Visibility.Visible;
                break;

            case AirFacts.Side:
                Place(_side, shown, plan.Columns, cells: false, columnWidth: (plan.SideWidth - ((plan.Columns - 1) * Fluent.Space4)) / plan.Columns);
                _side.Visibility = Visibility.Visible;
                heroWidth = width - plan.SideWidth - Fluent.Space4;
                break;
        }

        SetRowSpan(_hero, plan.Facts == AirFacts.Side ? 2 : 1);
        var alone = plan.Facts is AirFacts.None or AirFacts.Side;
        _hero.VerticalAlignment = alone ? VerticalAlignment.Center : VerticalAlignment.Top;
        RowDefinitions[0].Height = alone ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        RowDefinitions[1].Height = alone ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        var used = FitHero(heroWidth, plan.HeroRoom);

        // With readings under it, what the hero could not use is split above it and below the
        // readings, so the block sits centred instead of leaving a blank lower half.
        var spare = alone ? 0 : Math.Max(0, plan.HeroRoom - used) / 2;
        _hero.Margin = new Thickness(OnlineLayouts.Inset, OnlineLayouts.VerticalInset + spare, OnlineLayouts.Inset, 0);
    }

    /// <summary>
    /// [S-5109] The six bands as a strip in their status colours, a marker over the index, and the
    /// breakpoints under it, so the number reads against its whole scale.
    /// </summary>
    private void BuildStrip(bool show, AirQualityScale scale, double index)
    {
        _strip.Children.Clear();
        _strip.ColumnDefinitions.Clear();
        _strip.RowDefinitions.Clear();
        _strip.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show)
        {
            return;
        }

        const double Bar = 6;
        const double Marker = 8;
        _strip.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Marker + 2) });
        _strip.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Bar) });
        _strip.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var edges = AirQualityRules.Breakpoints(scale);
        for (var band = 0; band < 6; band++)
        {
            _strip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var segment = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Fill = Tone(AirQualityRules.Tone((AirBand)band)),
                RadiusX = Bar / 2,
                RadiusY = Bar / 2,
                Margin = new Thickness(band == 0 ? 0 : 1, 0, band == 5 ? 0 : 1, 0),
            };

            SetRow(segment, 1);
            SetColumn(segment, band);
            _strip.Children.Add(segment);

            var edge = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            edge.Text = band == 0 ? "0" : LocaleFormat.Integer((long)edges[band - 1]);
            edge.Margin = new Thickness(0, Fluent.Space1, 0, 0);
            SetRow(edge, 2);
            SetColumn(edge, band);
            _strip.Children.Add(edge);
        }

        var marker = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = Marker,
            Height = Marker,
            Fill = ThemeBrush.Of("TextFillColorPrimaryBrush", Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        SetColumnSpan(marker, 6);
        _strip.Children.Add(marker);
        // The strip spans the card's inset width, known from the area: no size handler to keep.
        var width = Math.Max(0, _area.Width - (2 * OnlineLayouts.Inset));
        marker.Margin = new Thickness(Math.Clamp((AirQualityRules.ScalePosition(scale, index) * width) - (Marker / 2), 0, Math.Max(0, width - Marker)), 0, 0, 0);
    }

    /// <summary>Readings as cells (label over value) or stat lines, row-major, in a fixed number of columns.</summary>
    private static void Place(Grid grid, List<(string Glyph, string Label, string Value)> facts, int columns, bool cells, double? columnWidth)
    {
        columns = Math.Max(1, columns);
        var perColumn = cells ? 0 : (facts.Count + columns - 1) / columns;

        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = columnWidth is { } w ? new GridLength(w) : new GridLength(1, GridUnitType.Star) });
        }

        var rows = cells ? (facts.Count + columns - 1) / columns : perColumn;
        for (var r = 0; r < rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < facts.Count; i++)
        {
            var (glyph, label, value) = facts[i];
            FrameworkElement part = cells ? WidgetParts.StatCell(glyph, label, value) : WidgetParts.StatLine(glyph, label, value);
            SetColumn(part, cells ? i % columns : i / perColumn);
            SetRow(part, cells ? i / columns : i % perColumn);
            grid.Children.Add(part);
        }
    }

    private bool BandFitsBeside(double room, TypeStep step)
    {
        var showIcon = room >= HeroLayout.IconSize(step) + HeroLayout.IconGap + HeroLayout.ValueWidth(step, ValueCharacters, UnitCharacters);
        var left = showIcon ? room - HeroLayout.IconSize(step) - HeroLayout.IconGap : room;
        var needed = HeroLayout.ValueWidth(step, ValueCharacters, UnitCharacters) + HeroLayout.BesideGap + FluentType.EstimateWidth(FluentType.Body, _band.Text.Length);
        return needed <= left;
    }

    /// <summary>The index at the largest step the room allows, the band beside it where it fits, under it where not.</summary>
    /// <returns>The height the hero takes: its value line and, when it stands under, the band.</returns>
    private double FitHero(double width, double height)
    {
        if (!(width > 0) || !(height > 0))
        {
            return 0;
        }

        var showIcon = width >= HeroLayout.IconSize(FluentType.Subtitle) + HeroLayout.IconGap + HeroLayout.ValueWidth(FluentType.Subtitle, ValueCharacters, UnitCharacters);

        TypeStep Pick(double h)
        {
            var first = HeroLayout.Fit(width, h, ValueCharacters, UnitCharacters, FluentType.Subtitle, FluentType.Display);
            var room = showIcon ? width - HeroLayout.IconSize(first) - HeroLayout.IconGap : width;
            return HeroLayout.Fit(room, h, ValueCharacters, UnitCharacters, FluentType.Subtitle, FluentType.Display);
        }

        var step = Pick(height);
        var beside = BandFitsBeside(width, step);
        if (!beside)
        {
            step = Pick(height - FluentType.Body.LineHeight);
        }

        _hero.Lay(step, showIcon, trailingBeside: true, linesBeside: beside);
        return Math.Max(step.LineHeight, HeroLayout.IconSize(step)) + (beside ? 0 : FluentType.Body.LineHeight);
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

    /// <summary>[S-5104] Good, caution and critical, from the Fluent status fills.</summary>
    private static Brush Tone(AirTone tone) => tone switch
    {
        AirTone.Good => ThemeBrush.Of("SystemFillColorSuccessBrush", Colors.SeaGreen),
        AirTone.Caution => ThemeBrush.Of("SystemFillColorCautionBrush", Colors.Goldenrod),
        _ => ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.Red),
    };
}
