using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Typography = Microsoft.UI.Xaml.Documents.Typography;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

// [S-6300] The parts every meter style shares: a half-circle gauge, a bar list (vertical columns or
// labelled rows) and a tile grid, plus the theme brushes they paint with. They only draw: what size
// and how many is the caller's ladder's decision, measured with Core's rules (GaugeLayout,
// BarLayout), so a CPU gauge and a battery gauge of one size are one gauge.
//
// API (kept stable; the other meters' styles build on it):
//   GaugeDial  new(); Lay(GaugeGeometry); SetFraction(double? 0..1); SetBrush(Brush)
//   BarList    new(BarListShape.Columns | Rows); SetCount(n);
//              SetItem(i, double? fraction, Brush brush, string? label = null, string? value = null);
//              LayColumns(BarColumnsPlan) | LayRows(BarRowsPlan)
//   TileGrid   new(); SetCount(n); SetItem(i, LoadShade); Lay(TileGridPlan)
//   MeterBrushes.Pressure(MeterPressure), .Track, .Unknown
// None of them is hit-testable, and each is flow-direction left to right ([S-1113]: a gauge fills
// clockwise and the first core is on the left in every language).

/// <summary>[S-6300] The theme brushes the meter parts paint with, looked up once.</summary>
internal static class MeterBrushes
{
    private static readonly Brush Accent = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private static readonly Brush Caution = FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Orange);
    private static readonly Brush Critical = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);
    private static readonly Brush Success = FluentLayout.Themed("SystemFillColorSuccessBrush", Colors.Green);

    /// <summary>[S-837] The empty part of a gauge or bar (the free share, the idle time): a progress bar's track, never the disabled fill.</summary>
    public static Brush Track { get; } = FluentLayout.Themed("ControlStrongFillColorDefaultBrush", Colors.Gray);

    /// <summary>An item with no reading.</summary>
    public static Brush Unknown { get; } = FluentLayout.Themed("ControlStrongFillColorDisabledBrush", Colors.Gray);

    /// <summary>[S-1123] The pressure colours every meter shares.</summary>
    public static Brush Pressure(MeterPressure pressure) => pressure switch
    {
        MeterPressure.Critical => Critical,
        MeterPressure.Elevated => Caution,
        MeterPressure.Healthy => Success,
        _ => Accent,
    };
}

/// <summary>
/// [S-6302] A half-circle gauge: a track over the top from the left end to the right, and the
/// value's arc over it in the pressure colour, round-capped as Windows' progress ring is. Its
/// geometry is <see cref="GaugeLayout.Fit"/>'s; the reading inside its bowl is the caller's.
/// </summary>
internal sealed partial class GaugeDial : Grid
{
    private readonly Microsoft.UI.Xaml.Shapes.Path _track = new() { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Microsoft.UI.Xaml.Shapes.Path _value = new() { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private GaugeGeometry _geometry;
    private double? _fraction;

    public GaugeDial()
    {
        FlowDirection = FlowDirection.LeftToRight;
        IsHitTestVisible = false;
        _track.Stroke = MeterBrushes.Track;
        _value.Stroke = MeterBrushes.Pressure(MeterPressure.Normal);
        Children.Add(_track);
        Children.Add(_value);
    }

    /// <summary>The geometry to draw at; the dial takes its box's size.</summary>
    public void Lay(GaugeGeometry geometry)
    {
        if (geometry == _geometry)
        {
            return;
        }

        _geometry = geometry;
        Width = geometry.Width;
        Height = geometry.Height;
        _track.StrokeThickness = geometry.Thickness;
        _value.StrokeThickness = geometry.Thickness;
        _track.Data = Arc(geometry, 1);
        Redraw();
    }

    /// <summary>The share shown, 0..1; null (no reading) draws the track alone.</summary>
    public void SetFraction(double? fraction)
    {
        var value = fraction is { } f && double.IsFinite(f) ? Math.Clamp(f, 0, 1) : (double?)null;
        if (value == _fraction)
        {
            return;
        }

        _fraction = value;
        Redraw();
    }

    public void SetBrush(Brush brush)
    {
        if (!ReferenceEquals(_value.Stroke, brush))
        {
            _value.Stroke = brush;
        }
    }

    private void Redraw()
    {
        // Under a hundredth there is no arc to draw, only a round cap: nothing reads better than a dot.
        _value.Visibility = _fraction is >= 0.005 && _geometry.Radius > 0 ? Visibility.Visible : Visibility.Collapsed;
        _value.Data = _fraction is { } f && _geometry.Radius > 0 ? Arc(_geometry, f) : null;
    }

    private static PathGeometry Arc(GaugeGeometry g, double fraction)
    {
        var (sx, sy) = g.PointAt(0);
        var (ex, ey) = g.PointAt(fraction);
        var figure = new PathFigure { StartPoint = new Point(sx, sy), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(ex, ey),
            Size = new Size(g.Radius, g.Radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = false,
        });

        var path = new PathGeometry();
        path.Figures.Add(figure);
        return path;
    }
}

/// <summary>[S-6303] [S-6304] How a <see cref="BarList"/> draws its items.</summary>
internal enum BarListShape
{
    /// <summary>[S-6303] Unlabelled vertical bars side by side, filling from the bottom (a CPU's cores).</summary>
    Columns = 0,

    /// <summary>[S-6304] A row per item: its label and value on a line, the bar filling from the left under it.</summary>
    Rows = 1,
}

/// <summary>
/// [S-6303] [S-6304] Bars, one per item, each a track with the item's share filled in its own brush.
/// Items are kept and rewritten in place, so a tick restyles bars rather than rebuilding them.
/// </summary>
internal sealed partial class BarList : Grid
{
    private readonly BarListShape _shape;
    private readonly StackPanel _stack;
    private readonly List<Item> _items = [];
    private BarRowsPlan? _rows;

    public BarList(BarListShape shape)
    {
        _shape = shape;
        FlowDirection = FlowDirection.LeftToRight;
        IsHitTestVisible = false;
        _stack = new StackPanel
        {
            Orientation = shape == BarListShape.Columns ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = shape == BarListShape.Columns ? HorizontalAlignment.Left : HorizontalAlignment.Stretch,
        };
        Children.Add(_stack);
    }

    public int Count => _items.Count;

    /// <summary>Keep exactly <paramref name="count"/> items, the existing ones kept.</summary>
    public void SetCount(int count)
    {
        count = Math.Max(0, count);

        while (_items.Count > count)
        {
            _stack.Children.RemoveAt(_items.Count - 1);
            _items.RemoveAt(_items.Count - 1);
        }

        while (_items.Count < count)
        {
            var item = new Item(_shape);
            _items.Add(item);
            _stack.Children.Add(item.Root);

            if (_rows is { } plan)
            {
                item.LayRow(plan);
            }
        }
    }

    /// <summary>One item's share (null: no reading, the track alone), its brush, and a row's label and value.</summary>
    public void SetItem(int index, double? fraction, Brush brush, string? label = null, string? value = null)
    {
        if ((uint)index < (uint)_items.Count)
        {
            _items[index].Set(fraction, brush, label, value);
        }
    }

    /// <summary>[S-6303] Columns: each bar's width and the gap between; the bars take the list's height.</summary>
    public void LayColumns(BarColumnsPlan plan)
    {
        _stack.Spacing = plan.Gap;

        foreach (var item in _items)
        {
            item.Root.Width = plan.Bar;
        }
    }

    /// <summary>[S-6304] Rows: their type, bar and spacing; only the first <see cref="BarRowsPlan.Shown"/> are drawn.</summary>
    public void LayRows(BarRowsPlan plan)
    {
        _rows = plan;
        _stack.Spacing = plan.Spacing;

        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].LayRow(plan);
            _items[i].Root.Visibility = i < plan.Shown ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>One bar: a track and its fill, sized from the track's own size so no transform squashes its corners.</summary>
    private sealed class Item
    {
        /// <summary>The columns' track, as faint as the history's wash is against its line.</summary>
        private const double ColumnTrackOpacity = 0.3;

        private readonly BarListShape _shape;
        private readonly Grid _track = new();
        private readonly Rectangle _fill = new();
        private readonly TextBlock? _label;
        private readonly TextBlock? _value;
        private double? _fraction;

        public Item(BarListShape shape)
        {
            _shape = shape;
            // [S-6303] A column's track is the chart's wash, not a progress bar's: sixteen strong
            // tracks side by side outshout the loads. A row's is the progress track ([S-837]).
            _track.Children.Add(new Rectangle
            {
                Fill = MeterBrushes.Track,
                Opacity = shape == BarListShape.Columns ? ColumnTrackOpacity : 1,
            });
            _track.CornerRadius = new CornerRadius(shape == BarListShape.Columns ? 2 : Fluent.ControlRadius);
            _fill.HorizontalAlignment = shape == BarListShape.Columns ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            _fill.VerticalAlignment = shape == BarListShape.Columns ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            _track.Children.Add(_fill);
            _track.SizeChanged += (_, _) => Size();

            if (shape == BarListShape.Columns)
            {
                Root = _track;
                return;
            }

            _label = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            _label.TextTrimming = TextTrimming.CharacterEllipsis;
            _value = Fluent.Text(FluentType.Caption, "TextFillColorPrimaryBrush");
            _value.HorizontalAlignment = HorizontalAlignment.Right;
            Typography.SetNumeralAlignment(_value, FontNumeralAlignment.Tabular);

            var line = new Grid { ColumnSpacing = Fluent.Space2 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            SetColumn(_value, 1);
            line.Children.Add(_label);
            line.Children.Add(_value);

            var row = new StackPanel { Spacing = BarLayout.LineGap };
            row.Children.Add(line);
            row.Children.Add(_track);
            Root = row;
        }

        public FrameworkElement Root { get; }

        public void Set(double? fraction, Brush brush, string? label, string? value)
        {
            if (!ReferenceEquals(_fill.Fill, brush))
            {
                _fill.Fill = brush;
            }

            if (_label is not null && _label.Text != (label ?? string.Empty))
            {
                _label.Text = label ?? string.Empty;
            }

            if (_value is not null && _value.Text != (value ?? string.Empty))
            {
                _value.Text = value ?? string.Empty;
            }

            var share = fraction is { } f && double.IsFinite(f) ? Math.Clamp(f, 0, 1) : (double?)null;
            if (share != _fraction)
            {
                _fraction = share;
                Size();
            }
        }

        public void LayRow(BarRowsPlan plan)
        {
            _track.Height = plan.Thickness;
            _track.CornerRadius = new CornerRadius(Math.Min(Fluent.ControlRadius, plan.Thickness / 2));
            _label?.Apply(plan.Label);
            _value?.Apply(plan.Label);
        }

        private void Size()
        {
            var share = _fraction ?? 0;

            if (_shape == BarListShape.Columns)
            {
                _fill.Height = Math.Max(0, _track.ActualHeight * share);
            }
            else
            {
                _fill.Width = Math.Max(0, _track.ActualWidth * share);
            }
        }
    }
}

/// <summary>
/// [S-6305] A tile per item (a core), filling the box in <see cref="BarLayout.Tiles"/>' grid, each
/// shaded by its load (<see cref="LoadTint"/>): the pressure colour at an opacity that rises with it.
/// </summary>
internal sealed partial class TileGrid : Grid
{
    private readonly List<Border> _tiles = [];
    private TileGridPlan _plan;

    public TileGrid()
    {
        FlowDirection = FlowDirection.LeftToRight;
        IsHitTestVisible = false;
    }

    public int Count => _tiles.Count;

    public void SetCount(int count)
    {
        count = Math.Max(0, count);

        while (_tiles.Count > count)
        {
            Children.Remove(_tiles[^1]);
            _tiles.RemoveAt(_tiles.Count - 1);
        }

        while (_tiles.Count < count)
        {
            var tile = new Border { CornerRadius = new CornerRadius(2), Background = MeterBrushes.Unknown, Opacity = LoadTint.Faintest };
            _tiles.Add(tile);
            Children.Add(tile);
        }

        Place();
    }

    public void SetItem(int index, LoadShade shade)
    {
        if ((uint)index >= (uint)_tiles.Count)
        {
            return;
        }

        var tile = _tiles[index];
        var brush = shade.Known ? MeterBrushes.Pressure(shade.Pressure) : MeterBrushes.Unknown;

        if (!ReferenceEquals(tile.Background, brush))
        {
            tile.Background = brush;
        }

        tile.Opacity = shade.Strength;
    }

    public void Lay(TileGridPlan plan)
    {
        if (plan == _plan)
        {
            return;
        }

        _plan = plan;
        Place();
    }

    /// <summary>Each tile at its cell, row by row from the top left; the corner radius small enough for the smallest tile.</summary>
    private void Place()
    {
        var plan = _plan;
        if (plan.Columns <= 0)
        {
            return;
        }

        var radius = Math.Clamp(Math.Min(plan.TileWidth, plan.TileHeight) / 6, 1, Fluent.ControlRadius);

        for (var i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            var column = i % plan.Columns;
            var row = i / plan.Columns;
            tile.Width = plan.TileWidth;
            tile.Height = plan.TileHeight;
            tile.HorizontalAlignment = HorizontalAlignment.Left;
            tile.VerticalAlignment = VerticalAlignment.Top;
            tile.CornerRadius = new CornerRadius(radius);
            tile.Margin = new Thickness(column * (plan.TileWidth + plan.Gap), row * (plan.TileHeight + plan.Gap), 0, 0);
        }
    }
}
