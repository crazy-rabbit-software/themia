using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets.Meters;
using Themia.Core.Diagnostics;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6300] The meter's styles, each the reading and one part of the Chart block's under it
/// (<see cref="MeterLook"/>): the history, a gauge, the items as columns, rows, rings or graphs, the
/// battery, a ring, the signal bars, or two readings. The reading is the same Readout on the same
/// ladder in every look; only the part changes, and what one look builds or watches is released when
/// another is chosen ([S-6005]). Where each goes is <see cref="MeterPlan"/>'s.
/// </summary>
public sealed partial class MeterView
{
    private MeterLook _look = (MeterLook)(-1);

    // The look's part (columns, rows, rings, graphs, battery, signal), in the row under the reading.
    private FrameworkElement? _body;

    // Gauge and Ring: the part and the reading in one box; Numbers: the two readings in one grid.
    private Grid? _box;
    private ChartRing? _dial;
    private ReadoutBlock? _second;

    private readonly List<ChartBar> _columns = [];
    private readonly List<RowBlock> _rows = [];
    private readonly List<ChartRing> _rings = [];
    private readonly List<GraphCell> _cells = [];

    private Border? _batteryFill;
    private Grid? _batteryBody;
    private Border? _batteryCap;
    private FontIcon? _bolt;
    private BatteryPicture? _picture;
    private double _charge;

    private readonly List<Border> _bars = [];
    private int _signal;
    private ColourRole _signalRole = ColourRole.Accent;

    private MeterItemsWatch? _items;
    private IReadOnlyList<MeterItem> _itemList = [];

    /// <summary>Use <paramref name="look"/>: build its part (once), let go of the previous one's, and watch its items.</summary>
    private void UseLook(MeterLook look)
    {
        if (look != _look)
        {
            _look = look;
            Release();
            Build(look);
        }

        WatchItems();
    }

    /// <summary>Take down the last look's part, and put the reading back in its row.</summary>
    private void Release()
    {
        _items?.Dispose();
        _items = null;
        _itemList = [];

        foreach (var element in new FrameworkElement?[] { _body, _box })
        {
            if (element is not null)
            {
                Children.Remove(element);
            }
        }

        // The reading back in its row (a gauge, a ring or the numbers held it in their box).
        _box?.Children.Remove(_reading);
        if (!Children.Contains(_reading))
        {
            Children.Add(_reading);
        }

        _reading.HorizontalAlignment = HorizontalAlignment.Left;
        _reading.VerticalAlignment = VerticalAlignment.Top;
        _reading.Margin = Family.SideInset;
        SetRow(_reading, 0);

        _body = null;
        _box = null;
        _dial = null;
        _second = null;
        _columns.Clear();
        _rows.Clear();
        _rings.Clear();
        _cells.Clear();
        _batteryFill = null;
        _batteryBody = null;
        _batteryCap = null;
        _bolt = null;
        _picture = null;
        _bars.Clear();
    }

    private void Build(MeterLook look)
    {
        _band.Visibility = look == MeterLook.Graph ? Visibility.Visible : Visibility.Collapsed;

        switch (look)
        {
            case MeterLook.Gauge:
            case MeterLook.Ring:
                _dial = new ChartRing(half: look == MeterLook.Gauge);
                _box = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = Family.SideInset };
                SetRowSpan(_box, 2);
                Move(_reading, _box);
                _reading.HorizontalAlignment = HorizontalAlignment.Center;
                _reading.VerticalAlignment = look == MeterLook.Gauge ? VerticalAlignment.Bottom : VerticalAlignment.Center;
                _reading.Margin = default;
                _box.Children.Insert(0, _dial);
                Children.Add(_box);
                break;

            case MeterLook.Numbers:
                _second = new ReadoutBlock { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                _box = new Grid { Margin = Family.SideInset, ColumnSpacing = FamilySpace.ColumnGap, RowSpacing = FamilySpace.S2 };
                SetRowSpan(_box, 2);
                Move(_reading, _box);
                _reading.Margin = default;
                _box.Children.Add(_second);
                Children.Add(_box);
                break;

            case MeterLook.Columns:
                // [S-1113] The first core on the left in every language.
                Body(new Grid { FlowDirection = FlowDirection.LeftToRight, Margin = Family.Inset with { Top = FamilySpace.S2 } });
                break;

            case MeterLook.Rows:
                Body(new StackPanel { Margin = new Thickness(0, FamilySpace.S2, 0, FamilySpace.Inset) });
                break;

            case MeterLook.Tiles:
            case MeterLook.Graphs:
                Body(new Grid
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    ColumnSpacing = FamilySpace.S2,
                    RowSpacing = FamilySpace.S2,
                    Margin = Family.Inset with { Top = FamilySpace.S2 },
                });
                break;

            case MeterLook.Battery:
                BuildBattery();
                break;

            case MeterLook.Signal:
                BuildSignal();
                break;

            default:
                break;
        }
    }

    private void Body(FrameworkElement body)
    {
        _body = body;
        SetRow(body, 1);
        Children.Add(body);
    }

    /// <summary>The reading into a look's box: out of the view's own row first.</summary>
    private void Move(FrameworkElement element, Panel to)
    {
        Children.Remove(element);
        to.Children.Add(element);
    }

    /// <summary>[S-6262] The battery: its body (an outline of 2 in the secondary role, the one radius), the fill 4 inside the outline, the bolt, the cap.</summary>
    private void BuildBattery()
    {
        _batteryFill = new Border { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(FamilySpace.S1) };
        _bolt = Family.Glyph(Glyphs.ChargingBolt, FamilyShape.Icon, ColourRole.TextPrimary);
        _bolt.HorizontalAlignment = HorizontalAlignment.Center;
        _bolt.Visibility = Visibility.Collapsed;
        _batteryBody = new Grid
        {
            BorderThickness = new Thickness(FamilyShape.Stroke),
            CornerRadius = Family.Radius,
            Children = { _batteryFill, _bolt },
        };
        _batteryCap = new Border { Width = MeterPlan.Cap, VerticalAlignment = VerticalAlignment.Center };

        // [S-1113] A battery fills from the left in every language, as Windows draws it.
        Body(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = Family.Inset with { Top = FamilySpace.S2 },
            Children = { _batteryBody, _batteryCap },
        });
    }

    /// <summary>[S-6263] Four bars rising left to right, 4 apart, standing on one line.</summary>
    private void BuildSignal()
    {
        var grid = new Grid
        {
            FlowDirection = FlowDirection.LeftToRight,
            ColumnSpacing = FamilySpace.S1,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = Family.Inset with { Top = FamilySpace.S2 },
        };

        for (var i = 0; i < PingSignal.BarCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var bar = new Border { VerticalAlignment = VerticalAlignment.Bottom, Background = Family.Brush(ColourRole.Track) };
            SetColumn(bar, i);
            grid.Children.Add(bar);
            _bars.Add(bar);
        }

        Body(grid);
    }

    /// <summary>[S-6300] The items of a columns, rows, rings or graphs look, from the hub; nothing for the others.</summary>
    private void WatchItems()
    {
        var wants = _look is MeterLook.Columns or MeterLook.Rows or MeterLook.Tiles or MeterLook.Graphs;

        if (!wants || _detached || _frozen)
        {
            _items?.Dispose();
            _items = null;
            return;
        }

        if (_items is not null)
        {
            return;
        }

        if (_host.System is not { } hub)
        {
            Log.Warn(Scope, $"no system hub for {_kind}'s items");
            return;
        }

        _items = _part.WatchItems(hub, OnItems);

        if (_items?.Current is { } current)
        {
            // [S-2506] What is read so far, and (the core graphs) every item's history with it.
            ShowItems(current, point: false);
            LoadGraphs();
        }
    }

    private void OnItems(IReadOnlyList<MeterItem> items)
    {
        if (!_detached)
        {
            ShowItems(items, point: true);
        }
    }

    /// <summary>[S-6318] Every core's history as the hub keeps it, onto its cell.</summary>
    private void LoadGraphs()
    {
        if (_items is null)
        {
            return;
        }

        for (var i = 0; i < _cells.Count; i++)
        {
            _cells[i].Band.Load(_items.History(i, _cells[i].Band.Holds), _items.Added);
        }
    }

    /// <summary>
    /// [S-6330] Put the items on their parts, at once; a different count is a different layout.
    /// <paramref name="point"/>: a new read, which the core graphs add to their lines.
    /// </summary>
    private void ShowItems(IReadOnlyList<MeterItem> items, bool point)
    {
        var recount = items.Count != _itemList.Count;
        _itemList = items;

        if (recount)
        {
            Recount(items.Count);
            if (_look == MeterLook.Graphs)
            {
                // New cells start from the cores' history, which already holds this read.
                LoadGraphs();
                point = false;
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var role = MeterPlan.Role(item.Pressure, absent: false);
            var known = item.Fraction is { } f && double.IsFinite(f);

            switch (_look)
            {
                case MeterLook.Columns when i < _columns.Count:
                    _columns[i].Role(known ? role : ColourRole.TextDisabled);
                    _columns[i].Show(item.Fraction ?? 0);
                    break;

                case MeterLook.Rows when i < _rows.Count:
                    _rows[i].Show(item.Label ?? string.Empty, item.Value);
                    _rows[i].Available = known;
                    if (_rows[i].Tag is ChartBar bar)
                    {
                        bar.Role(known ? role : ColourRole.TextDisabled);
                        bar.Show(item.Fraction ?? 0);
                    }

                    break;

                case MeterLook.Tiles when i < _rings.Count:
                    _rings[i].Role(known ? role : ColourRole.TextDisabled);
                    _rings[i].Show(item.Fraction ?? 0);
                    break;

                case MeterLook.Graphs when i < _cells.Count:
                    _cells[i].Show(item.Fraction, point, role);
                    break;

                default:
                    break;
            }
        }

        if (recount)
        {
            Lay();
        }
    }

    /// <summary>Keep one part per item: the columns, rows, rings or cells, the existing ones kept.</summary>
    private void Recount(int count)
    {
        switch (_look)
        {
            case MeterLook.Columns when _body is Grid grid:
                Resize(_columns, count, grid, () => new ChartBar(vertical: true), (bar, i) =>
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    SetColumn(bar, i);
                });
                while (grid.ColumnDefinitions.Count > count)
                {
                    grid.ColumnDefinitions.RemoveAt(grid.ColumnDefinitions.Count - 1);
                }

                break;

            case MeterLook.Rows when _body is StackPanel stack:
                Resize(_rows, count, stack, () =>
                {
                    var row = new RowBlock(RowKind.Fact, lead: 0);
                    var bar = new ChartBar();
                    row.Meter(bar, double.NaN);
                    row.Tag = bar;
                    return row;
                }, (_, _) => { });
                break;

            case MeterLook.Tiles when _body is Grid grid:
                Resize(_rings, count, grid, () => new ChartRing(), (_, _) => { });
                break;

            case MeterLook.Graphs when _body is Grid grid:
                while (_cells.Count > count)
                {
                    grid.Children.Remove(_cells[^1].Root);
                    _cells.RemoveAt(_cells.Count - 1);
                }

                while (_cells.Count < count)
                {
                    var cell = new GraphCell(_band.Capacity);
                    _cells.Add(cell);
                    grid.Children.Add(cell.Root);
                }

                break;

            default:
                break;
        }
    }

    private static void Resize<T>(List<T> parts, int count, Panel panel, Func<T> make, Action<T, int> placed)
        where T : FrameworkElement
    {
        while (parts.Count > count)
        {
            panel.Children.Remove(parts[^1]);
            parts.RemoveAt(parts.Count - 1);
        }

        while (parts.Count < count)
        {
            var part = make();
            placed(part, parts.Count);
            parts.Add(part);
            panel.Children.Add(part);
        }
    }

    /// <summary>[S-6330] A reading on the look's own part: the gauge's or ring's sweep, the battery's fill, the bars lit, the second figure.</summary>
    private void PresentLook(MeterReading reading, ColourRole role)
    {
        switch (_look)
        {
            case MeterLook.Gauge:
            case MeterLook.Ring:
                // [S-6302] The reading's own share, in the colour the history would have; [S-1941] the middle for a thing not here.
                _dial?.Role(role);
                _dial?.Show(Unavailable.Fraction(reading) ?? 0);
                break;

            case MeterLook.Numbers when _second is not null:
                var up = reading.Available && reading.Second is { } second && double.IsFinite(second) ? Formatting.Rate(second) : "--";
                ShowReading(_second, up, reading.Available, Glyphs.Upload, role);
                break;

            case MeterLook.Battery:
                PaintBattery(reading, role);
                break;

            case MeterLook.Signal:
                _signal = PingSignal.Level(reading.Available ? reading.Value : null);
                _signalRole = role;
                PaintSignal();
                break;

            default:
                break;
        }
    }

    /// <summary>[S-6262] The charge at once in its role; no reading is an empty battery, no battery at all stands at the middle ([S-1941]).</summary>
    private void PaintBattery(MeterReading reading, ColourRole role)
    {
        if (_batteryFill is null || _batteryBody is null || _batteryCap is null || _bolt is null)
        {
            return;
        }

        var outline = Family.Brush(reading.Absent ? ColourRole.TextDisabled : ColourRole.TextSecondary);
        _batteryBody.BorderBrush = outline;
        _batteryCap.Background = outline;
        _batteryFill.Background = Family.Brush(role);
        _bolt.Visibility = reading.Available && reading.Charging && _picture is { Bolt: > 0 } ? Visibility.Visible : Visibility.Collapsed;

        var charge = Unavailable.Fraction(reading) ?? 0;
        if (!charge.Equals(_charge))
        {
            _charge = charge;
            _batteryFill.Width = _picture?.FillWidth(charge) ?? 0;
        }
    }

    /// <summary>[S-6263] The bars the latency earns in the status role, the rest the track.</summary>
    private void PaintSignal()
    {
        for (var i = 0; i < _bars.Count; i++)
        {
            _bars[i].Background = Family.Brush(i < _signal ? _signalRole : ColourRole.Track);
        }
    }

    /// <summary>[S-1762] Place the reading and the look's part for the current area.</summary>
    private void Lay()
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        var worst = Worst;
        var step = MeterPlan.Reading(_area, worst);

        switch (_look)
        {
            case MeterLook.Graph:
                _reading.Use(step);
                _band.Visibility = MeterPlan.Band(_area, step, _graph) > 0 ? Visibility.Visible : Visibility.Collapsed;
                break;

            case MeterLook.Gauge:
            case MeterLook.Ring:
                LayDial(worst, step);
                break;

            case MeterLook.Numbers when _box is not null && _second is not null:
                var (side, pair) = MeterPlan.Numbers(_area, worst with { Lead = true });
                _box.RowDefinitions.Clear();
                _box.ColumnDefinitions.Clear();
                if (side)
                {
                    _box.ColumnDefinitions.Add(new ColumnDefinition());
                    _box.ColumnDefinitions.Add(new ColumnDefinition());
                }
                else
                {
                    _box.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    _box.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                SetColumn(_second, side ? 1 : 0);
                SetRow(_second, side ? 0 : 1);
                _reading.Use(pair);
                _second.Use(pair);
                break;

            case MeterLook.Columns when _body is not null:
                _reading.Use(step);
                _body.Visibility = MeterPlan.Columns(_area, step, _columns.Count) ? Visibility.Visible : Visibility.Collapsed;
                break;

            case MeterLook.Rows:
                _reading.Use(step);
                var shown = MeterPlan.Rows(_area, step, _rows.Count);
                var nameRoom = MeterPlan.Content(_area).Width / 2;
                for (var i = 0; i < _rows.Count; i++)
                {
                    _rows[i].Visibility = i < shown ? Visibility.Visible : Visibility.Collapsed;
                    _rows[i].NameText.MaxWidth = nameRoom;
                }

                break;

            case MeterLook.Tiles when _body is Grid grid:
                _reading.Use(step);
                LayGrid(grid, MeterPlan.Rings(_area, step, _rings.Count), _rings);
                break;

            case MeterLook.Graphs when _body is Grid grid:
                _reading.Use(step);
                var plan = MeterPlan.Graphs(_area, step, _cells.Count);
                LayGrid(grid, plan, [.. _cells.Select(c => (FrameworkElement)c.Root)]);
                foreach (var cell in _cells)
                {
                    cell.Figured(MeterPlan.GraphFigure(plan.CellHeight));
                }
                break;

            case MeterLook.Battery when _body is not null && _batteryBody is not null && _batteryCap is not null && _bolt is not null && _batteryFill is not null:
                _reading.Use(step);
                _picture = MeterPlan.Battery(_area, step);
                _body.Visibility = _picture is null ? Visibility.Collapsed : Visibility.Visible;
                if (_picture is { } picture)
                {
                    _batteryBody.Width = picture.BodyWidth;
                    _batteryBody.Height = picture.Height;
                    _batteryCap.Height = picture.CapHeight;
                    _batteryCap.CornerRadius = Family.Round(MeterPlan.Cap, picture.CapHeight);
                    _batteryFill.Width = picture.FillWidth(_charge);
                    if (picture.Bolt > 0)
                    {
                        _bolt.FontSize = picture.Bolt;
                        _bolt.Width = picture.Bolt;
                        _bolt.Height = picture.Bolt;
                    }
                }

                if (_last is { } reading)
                {
                    PaintBattery(reading, MeterPlan.Role(_part.PressureOf(reading), reading.Absent));
                }

                break;

            case MeterLook.Signal when _body is not null:
                _reading.Use(step);
                var bars = MeterPlan.Signal(_area, step);
                _body.Visibility = bars.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                for (var i = 0; i < _bars.Count && i < bars.Count; i++)
                {
                    _bars[i].Width = bars[i].Width;
                    _bars[i].Height = bars[i].Height;
                    _bars[i].CornerRadius = bars[i].Width >= 2 * FamilyShape.Radius ? Family.Radius : Family.Round(bars[i].Width, bars[i].Height);
                }

                if (_body is Grid signal && bars.Count > 0)
                {
                    signal.Height = bars[^1].Height;
                }

                break;

            default:
                _reading.Use(step);
                break;
        }
    }

    /// <summary>[S-6302] [S-6266] The gauge or ring as large as the content takes, the reading in it; too small, the reading alone.</summary>
    private void LayDial(ReadoutText worst, TypeStep step)
    {
        if (_box is null || _dial is null)
        {
            return;
        }

        var gauge = _look == MeterLook.Gauge;
        var diameter = gauge ? MeterPlan.Gauge(_area) : MeterPlan.Ring(_area);

        if (diameter <= 0)
        {
            _dial.Visibility = Visibility.Collapsed;
            _box.Width = double.NaN;
            _box.Height = double.NaN;
            _reading.HorizontalAlignment = HorizontalAlignment.Left;
            _reading.VerticalAlignment = VerticalAlignment.Top;
            _reading.Use(step);
            return;
        }

        _dial.Visibility = Visibility.Visible;
        _box.Width = diameter;
        _box.Height = gauge ? ChartRules.GaugeHeight(diameter) : diameter;
        _reading.HorizontalAlignment = HorizontalAlignment.Center;
        _reading.VerticalAlignment = gauge ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        _reading.Use(gauge ? MeterPlan.GaugeReading(_area, diameter, worst) : MeterPlan.RingReading(_area, diameter, worst));
    }

    /// <summary>A grid of square or graph cells, row by row from the top left; none when the plan shows none.</summary>
    private static void LayGrid<T>(Grid grid, MeterGrid plan, IReadOnlyList<T> cells)
        where T : FrameworkElement
    {
        grid.Visibility = plan.Shown ? Visibility.Visible : Visibility.Collapsed;
        if (!plan.Shown)
        {
            return;
        }

        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var c = 0; c < plan.Columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(plan.CellWidth) });
        }

        for (var r = 0; r < plan.Rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plan.CellHeight) });
        }

        for (var i = 0; i < cells.Count; i++)
        {
            SetColumn(cells[i], i % plan.Columns);
            SetRow(cells[i], i / plan.Columns);
        }
    }

    /// <summary>
    /// [S-6317] One core's graph: its load in Caption (primary) over its history, 4 under it, the history
    /// in the load's role, stepping as the main graph does ([S-6331]).
    /// </summary>
    private sealed class GraphCell
    {
        private readonly TextBlock _figure = Family.Text(FamilyType.Caption);
        private string _shown = string.Empty;

        public GraphCell(int capacity)
        {
            Band = new SparkBand(capacity, ChartScale.Share);
            SetRow(Band, 1);
            Root = new Grid
            {
                RowSpacing = FamilySpace.S1,
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                },
                Children = { _figure, Band },
            };
        }

        public Grid Root { get; }

        public SparkBand Band { get; }

        /// <summary>The figure over the graph, or the graph alone in a cell too short for both.</summary>
        public void Figured(bool shown)
        {
            _figure.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            Root.RowSpacing = shown ? FamilySpace.S1 : 0;
        }

        /// <summary>One read of a core: a point on its history when <paramref name="point"/>, its figure and its role.</summary>
        public void Show(double? load, bool point, ColourRole role)
        {
            var known = load is { } l && double.IsFinite(l);
            if (point && known)
            {
                Band.Push(load!.Value);
            }

            Band.Role(known ? role : ColourRole.TextDisabled);

            // [S-6330] A whole percent, at once, as the reading.
            var text = known ? Formatting.Percent(Math.Clamp(load!.Value, 0, 1)) : "--";
            if (text != _shown)
            {
                _shown = text;
                _figure.Text = text;
                _figure.Foreground = Family.Brush(known ? ColourRole.TextPrimary : ColourRole.TextDisabled);
            }
        }
    }
}
