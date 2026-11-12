using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.App.Widgets.Meters;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// The face every system meter shares, built from the family's blocks ([S-6702], [S-6717]): the
/// reading (a Readout) and the part its style draws under it, nothing else: the card's header is
/// the label. Which series it shows is the kind's <see cref="MeterPart"/>; the readings and the
/// history are the system hub's ([S-2500], [S-2504]), so a meter built again shows the history its
/// series already has. Where the parts go at each size is <see cref="MeterPlan"/>'s.
/// <para>
/// [S-1762] The reading stands at the top, start-aligned, on the step its widest case takes (28, or 40
/// once expanded both ways, and no larger); the history runs under it to the card's foot, edge to edge ([S-1824]), and grows
/// with the tile, so a large tile is a longer chart, never a bigger number.
/// </para>
/// </summary>
public sealed partial class MeterView : Grid, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetSurfaceAware, IMeterHost
{
    private const string Scope = "meter";

    private readonly MeterPart _part;
    private readonly IWidgetHost _host;
    private readonly WidgetId _kind;

    // [S-6702] The reading: the number with its smaller unit, the battery's glyph as its lead.
    private readonly ReadoutBlock _reading = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly SparkBand _band;

    private WidgetConfig _config;
    private ISeriesWatch? _watch;
    private SeriesInterest? _series;
    private MeterReading? _last;

    private WidgetArea _area;
    private bool _graph;

    // [S-1941] The history shows the stand-in line of a thing not on this machine.
    private bool _standIn;

    // [S-433] [S-1940] The part said the thing measured is gone (an adapter unplugged): its reason, the series ignored.
    private string? _absent;

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
        ArgumentNullException.ThrowIfNull(part);
        _kind = part.Kind;
        _part = part;
        _config = WidgetConfig.CreateDefault(_kind);
        _band = new SparkBand(60, _part.Scale);

        _reading.Show("--", valueRole: ColourRole.TextDisabled);
        _reading.Margin = Family.SideInset;

        // Two rows: the reading, and the part its look draws under it, 8 down (the part's own margin).
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Children.Add(_reading);

        SetRow(_band, 1);
        _band.Margin = new Thickness(0, FamilySpace.S2, 0, 0);
        Children.Add(_band);
    }

    /// <summary>[S-663] The card's word on how much room the meter has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>The widest reading the step is chosen for, with the lead glyph where the look draws one.</summary>
    private ReadoutText Worst => _part.Reading with { Lead = _look == MeterLook.Numbers || (_part.HasBadge && !DrawsCharge) };

    /// <summary>[S-6262] [S-6266] A look that draws the charge itself (the battery, the ring) needs no badge before the figure.</summary>
    private bool DrawsCharge => _look is MeterLook.Battery or MeterLook.Ring;

    /// <summary>
    /// [S-1187] The glyph before the reading: the Numbers look's download arrow (as its upload), else
    /// the kind's badge (the battery's level) where the look does not draw the charge already.
    /// </summary>
    private string? Lead(MeterReading reading)
        => _look == MeterLook.Numbers ? Glyphs.Download : DrawsCharge ? null : _part.Badge(reading);

    /// <inheritdoc />
    public CardOpen? Opens => _part.Opens(_config);

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
        if (_cells.Count > 0 && _cells[0].Band.Capacity != capacity)
        {
            var grewCores = capacity > _cells[0].Band.Capacity;
            foreach (var cell in _cells)
            {
                cell.Band.Capacity = capacity;
            }

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
        _part.Watch(hub, this);

        // [S-2506] The watch never calls back from inside Watch: the history so far and the newest
        // reading are read here, so a meter built again (a view switch, edit mode) shows them at once.
        _band.Load(_watch.History(_band.Holds), _watch.Added);
        Present(_watch.Latest ?? _part.Waiting);
        return true;
    }

    /// <summary>One read of the series: the reading, and the point it added to the history.</summary>
    private void OnUpdate(SeriesUpdate update)
    {
        if (_detached || _absent is not null)
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
    /// [S-433] [S-1940] The part says the thing measured is gone (no adapter at its place) or back:
    /// gone, the meter is drawn absent with the reason on hover and the series ignored until it is back.
    /// </summary>
    public void SetAbsent(string? reason)
    {
        if (reason == _absent)
        {
            return;
        }

        var back = _absent is not null && reason is null;
        _absent = reason;

        if (reason is not null)
        {
            Present(MeterReading.Missing(reason));
        }
        else if (back)
        {
            _band.Clear();
            Present(_watch?.Latest ?? _part.Waiting);
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

        _last = reading;
        var role = MeterPlan.Role(_part.PressureOf(reading), reading.Absent);

        // Unavailable is a role, not an opacity. Pressure colours the chart, never the number, which
        // stays legible whatever the load; a lead glyph (the battery's) takes Caution or Critical.
        ShowReading(_reading, reading.Primary, reading.Available, Lead(reading), role);

        // [S-1161] Available, it names what it is about (the disk's drive), for a card whose header is off.
        // [S-6332] Touched only when it says something else.
        var tip = reading.Available ? _part.ReadingTip : reading.Absent ? null : reading.Reason;
        if (!Equals(ToolTipService.GetToolTip(_reading), tip))
        {
            ToolTipService.SetToolTip(_reading, tip);
        }

        // [S-1940] [S-82a] Not on this machine (no battery): the same face in the disabled role, its
        // level at the middle ([S-1941]), the reason on the whole tile — never an empty state.
        UnavailableLook.Say(this, reading.Absent ? reading.Reason : null);

        if (reading.Absent != _standIn)
        {
            // The chart is drawn where it would be: a flat line at the middle, gone with the first real reading.
            _standIn = reading.Absent;

            if (_standIn)
            {
                _band.Load(Unavailable.History(_band.Holds));
                ShowItems(_part.AbsentItems, point: false);
            }
            else
            {
                _band.Clear();
            }
        }

        _band.Role(role);
        PresentLook(reading, role);
        _part.Observe(reading, this);
    }

    /// <summary>
    /// [S-3860] A reading on a Readout, its figure and unit split as the Readout draws them; the block
    /// touches only what changed ([S-6330]).
    /// </summary>
    private static void ShowReading(ReadoutBlock block, string primary, bool available, string? lead, ColourRole role)
    {
        var split = ReadoutRules.Split(primary);
        block.Show(split.Value, split.Unit, lead: lead, valueRole: available ? ColourRole.TextPrimary : ColourRole.TextDisabled,
            leadRole: role is ColourRole.Caution or ColourRole.Critical ? role : ColourRole.TextSecondary);
    }

    /// <summary>[S-1167] Keep the surface for a kind's header title (MeterView.Part.cs).</summary>
    public void Bind(IWidgetSurface surface) => BindTitle(surface);

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
        _part.Unwatch();
    }
}
