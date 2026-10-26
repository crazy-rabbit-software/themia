using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.App.Widgets.Meters;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;

namespace Themia.App.Widgets;

/// <summary>
/// The face every system meter shares: the reading and its history, nothing else — the card's
/// header is the label. Which series it shows is the kind's <see cref="MeterPart"/>
/// (<c>Meters/&lt;Kind&gt;Meter.cs</c>); the readings and the history are the system hub's
/// ([S-2500], [S-2504]), so a meter built again shows the history its series already has. This file
/// is the layout, so the meters grow and shrink the same way.
/// <para>
/// [S-664] [S-1762] The ladder (<see cref="MeterFace"/>): a tiny tile is the reading alone; from
/// there the history appears under the reading and grows to fill the tile, the reading growing by
/// ramp step up to Display; a wide strip puts the reading in a column on the left and the history
/// the full height beside it. With the history switched off the reading fills the tile.
/// </para>
/// <para>
/// [S-1824] The history is the card's bottom band, edge to edge with no inset at its sides or under
/// it; the card's rounded corners clip it. Only the reading carries the content inset.
/// </para>
/// </summary>
public sealed partial class MeterView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IMeterHost
{
    private const string Scope = "meter";

    /// <summary>The graph never shrinks below this: under it a trace is a smudge, not a chart.</summary>
    private const double GraphFloor = Fluent.Space6;

    private readonly MeterPart _part;
    private readonly IWidgetHost _host;
    private readonly WidgetId _kind;

    // [S-3860] The shared hero readout: the number with its smaller unit, the battery's glyph as its icon.
    private readonly HeroReadout _reading = new();
    private readonly SparkBand _band;

    // Looked up once. The reading swaps between them on every sample, and a resource lookup per
    // sample per widget is a dictionary hit and a possible log line for nothing.
    private readonly Brush _readingBrush = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _unavailableBrush = ThemeBrush.Of("TextFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _accentBrush = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly Brush _cautionBrush = ThemeBrush.Of("SystemFillColorCautionBrush", Colors.Orange);
    private readonly Brush _criticalBrush = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.Red);
    private readonly Brush _successBrush = ThemeBrush.Of("SystemFillColorSuccessBrush", Colors.Green);

    private WidgetConfig _config;
    private ISeriesWatch? _watch;
    private SeriesInterest? _series;

    // The ramp step currently applied. Kept so a sample that lands on the same step does not touch
    // the text block at all: re-applying a font size is a layout invalidation the tile does not need.
    private TypeStep? _heroStep;
    private bool _badged;
    private string _shown = string.Empty;

    private WidgetArea _area;
    private bool _graph;

    // [S-1941] The history shows the stand-in line of a thing not on this machine.
    private bool _standIn;

    // [S-6300] The style's look and what it builds: made when the look is chosen, let go when another is.
    private MeterLook _look;
    private FrameworkElement? _body;
    private GaugeDial? _dial;
    private BarList? _bars;
    private TileGrid? _tiles;
    private CoreGraphGrid? _graphs;
    private MeterItemsWatch? _items;
    private IReadOnlyList<MeterItem> _itemList = [];

    // [S-1881] Until Attach: a parked view watches nothing, whatever config it is handed.
    private bool _detached = true;

#if DEBUG
    // Debug renders: a fixture state stands still; no live items are watched over it again.
    private bool _frozen;
#else
    private const bool _frozen = false;
#endif

    public MeterView(IWidgetHost host, MeterPart part)
    {
        _host = host;
        // No padding on the view: the reading carries the content inset and the history none
        // ([S-1824]), so the gap under the reading is the ladder's to size.
        ArgumentNullException.ThrowIfNull(part);
        _kind = part.Kind;
        _part = part;
        _config = WidgetConfig.CreateDefault(_kind);
        _band = new SparkBand(60, _part.Scale);

        _reading.SetValue("--", string.Empty, false);

        // [S-1187] The kind's badge (the battery icon) leads the reading as the readout's glyph; the
        // ladder measures its width ([S-3860]).
        _reading.Icon.VerticalAlignment = VerticalAlignment.Center;
        _reading.Margin = new Thickness(HeroLayout.SideInset, HeroLayout.TopInset, HeroLayout.SideInset, 0);

        // Two rows (the reading, the history) and two columns (the reading's, the history's on a
        // strip): each arrangement only moves the two parts between these cells.
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        Children.Add(_reading);
        Children.Add(_band);
    }

    /// <summary>[S-663] The card's word on how much room the meter has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>[S-1762] Place the reading and the history for the current area and size the reading.</summary>
    private void Lay()
    {
        // A face hides the readout; every other look shows it.
        _reading.Visibility = Visibility.Visible;

        // [S-6300] A style other than the history lays its own part on the same ladder.
        if (_look != MeterLook.Graph)
        {
            _band.Visibility = Visibility.Collapsed;

            if (_look == MeterLook.Gauge)
            {
                LayGauge();
            }
            else if (_face is not null)
            {
                LayFace();
            }
            else
            {
                LayItems();
            }

            return;
        }

        SetColumnSpan(_reading, 1);
        _reading.HorizontalAlignment = HorizontalAlignment.Stretch;

        var plan = MeterFace.Plan(_area, _graph, _part.Reading, _part.HasBadge, Fluent.Space2, GraphFloor);
        var side = plan.Arrangement == MeterFaceArrangement.Side;
        var alone = plan.Arrangement == MeterFaceArrangement.ReadingOnly;

        ColumnDefinitions[0].Width = side ? new GridLength(plan.ReadingColumn) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        // Alone, or beside the history on a strip, the reading is centred in the whole height; over
        // the history it sits at the top and the history takes everything under it.
        SetRow(_reading, 0);
        SetRowSpan(_reading, alone || side ? 2 : 1);
        _reading.VerticalAlignment = alone || side ? VerticalAlignment.Center : VerticalAlignment.Top;
        _reading.Margin = alone || side
            ? new Thickness(HeroLayout.SideInset, 0, HeroLayout.SideInset, 0)
            : new Thickness(HeroLayout.SideInset, HeroLayout.TopInset, HeroLayout.SideInset, 0);

        _band.Visibility = plan.Graph ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_band, side ? 0 : 1);
        SetRowSpan(_band, side ? 2 : 1);
        SetColumn(_band, side ? 1 : 0);
        // Stacked, the history is the star row: exactly what the reading leaves (plan.GraphHeight).
        // [S-1824] Edge to edge either way; beside the reading its column's own inset separates the two.
        _band.Margin = side ? new Thickness(0) : new Thickness(0, Fluent.Space2, 0, 0);

        ApplyHero(plan.Reading);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config.Kind == _kind ? config : WidgetConfig.CreateDefault(_kind);

        var gauge = _config as GaugeConfig;
        _graph = gauge is { DrawsHistory: true };

        _part.ApplyConfig(_config, this);
        UseLook(_part.Look(_config));

        // [S-1121] The span at the series' spacing ([S-2512]: a ping point is five seconds).
        var series = _part.Series(_config);
        var capacity = MeterTiming.Points(gauge?.GraphSpanSeconds ?? 60, series.Key.PeriodSeconds);
        var grew = capacity > _band.Capacity;
        _band.Capacity = capacity;

        // [S-6317] The core graphs span what the graph spans.
        if (_graphs is not null && _graphs.Capacity != capacity)
        {
            var grewCores = capacity > _graphs.Capacity;
            _graphs.Capacity = capacity;

            if (grewCores)
            {
                LoadGraphs();
            }
        }

        if (!Rewatch(series) && grew && _watch is not null)
        {
            // [S-2504] A longer span shows the longer history the hub already holds, at once.
            _band.Load(_watch.History(_band.Holds), _watch.Added);
        }

        Lay();
    }

    /// <summary>
    /// [S-2504] Watch the series the config names, only when it names a different one: dragging a
    /// settings slider must not drop and remake the watch. A different series (a new drive or host) is
    /// a different history. True when it watched again.
    /// </summary>
    private bool Rewatch(SeriesInterest series)
    {
        if (_detached)
        {
            return false;
        }

        if (_watch is not null && series == _series)
        {
            return false;
        }

        _watch?.Dispose();
        _watch = null;
        _series = series;

        if (_host.System is not { } hub)
        {
            // Only before the host has started: there is nothing to read from yet.
            Log.Warn(Scope, $"no system hub for {_kind}");
            _band.Clear();
            Present(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()));
            return true;
        }

        _watch = hub.WatchSeries(series, OnUpdate);

        // [S-2506] The watch never calls back from inside Watch: the history so far and the newest
        // reading are read here, so a meter built again (a view switch, edit mode) shows them at once.
        _band.Load(_watch.History(_band.Holds), _watch.Added);
        Present(_watch.Latest ?? _part.Waiting);
        return true;
    }

    /// <summary>One read of the series: the reading, and the point it added to the history.</summary>
    private void OnUpdate(SeriesUpdate update)
    {
        if (_detached)
        {
            return;
        }

        Present(update.Reading);

        if (update.Point is { } point)
        {
            _band.Push(point.First, double.IsNaN(point.Second) ? null : point.Second);
        }
    }

    /// <summary>
    /// [S-6330] Put one reading on screen, at once: the number, the gauge and the face show it as it
    /// arrives, and the history moves only with its points.
    /// </summary>
    private void Present(MeterReading reading)
    {
        if (_detached)
        {
            return;
        }

        ShowText(reading.Primary);

        // Unavailable is a colour, not an opacity. Pressure colours the chart, never the number,
        // which stays legible whatever the load. Why it is unavailable is said on hover.
        _reading.ValueLine.Foreground = reading.Available ? _readingBrush : _unavailableBrush;
        // [S-1161] Available, it names what it is about (the disk's drive), for a card whose header is off.
        // [S-6332] Touched only when it says something else.
        var tip = reading.Available ? _part.ReadingTip : reading.Absent ? null : reading.Reason;
        if (!Equals(ToolTipService.GetToolTip(_reading), tip))
        {
            ToolTipService.SetToolTip(_reading, tip);
        }

        // [S-1940] [S-82a] Not on this machine (no battery): the same face in the disabled look, its
        // level at the middle ([S-1941]), the reason on the whole tile — never an empty state.
        UnavailableLook.Say(this, reading.Absent ? reading.Reason : null);

        if (reading.Absent != _standIn)
        {
            // The chart is drawn where it would be: a flat line at the middle, gone with the first real reading.
            _standIn = reading.Absent;

            if (_standIn)
            {
                _band.Load(Unavailable.History(_band.Holds));
            }
            else
            {
                _band.Clear();
            }
        }

        var pressure = LevelBrush(reading);
        _band.SetBrush(pressure);

        // [S-6302] The gauge is the reading's own share, in the colour the history would have.
        _dial?.SetFraction(Unavailable.Fraction(reading));
        _dial?.SetBrush(pressure);

        // [S-6261]..[S-6266] A face that draws the reading itself (numbers, a battery, a ring, signal bars).
        _face?.Present(reading, pressure);

        var badge = _part.Badge(reading);
        var badged = badge is not null;
        _reading.Icon.Glyph = badge ?? string.Empty;
        _reading.Icon.Foreground = reading.Absent ? UnavailableLook.Text : pressure;

        _part.Observe(reading, this);

        // Only a glyph coming or going changes the face; every other reading keeps the layout it has.
        if (badged != _badged)
        {
            _badged = badged;
            _heroStep = null;
            Lay();
        }
    }

    /// <summary>[S-3860] The number and its smaller unit, split again only when the text changed.</summary>
    private void ShowText(string primary)
    {
        if (primary != _shown)
        {
            _shown = primary;
            var text = HeroLayout.Split(primary);
            _reading.SetValue(text.Value, text.Unit, text.Spaced);
        }
    }

    /// <summary>[S-1167] Keep the surface for a kind's header title (MeterView.Part.cs).</summary>
    public void Bind(IWidgetSurface surface) => BindTitle(surface);

    /// <summary>[S-1940] A reading's level colour: its pressure's, or the disabled fill for a thing not on this machine.</summary>
    private Brush LevelBrush(MeterReading reading) => reading.Absent ? UnavailableLook.Fill : Pressure(_part.PressureOf(reading));

    private Brush Pressure(MeterPressure pressure) => pressure switch
    {
        MeterPressure.Critical => _criticalBrush,
        MeterPressure.Elevated => _cautionBrush,
        MeterPressure.Healthy => _successBrush,
        _ => _accentBrush,
    };

    public void Detach()
    {
        _detached = true;

        // [S-2513] The series and its history stay in the hub while the state wants them, so the
        // meter built in this one's place (a view switch, edit mode) picks them up.
        _watch?.Dispose();
        _watch = null;
        _series = null;
        _items?.Dispose();
        _items = null;
    }
}
