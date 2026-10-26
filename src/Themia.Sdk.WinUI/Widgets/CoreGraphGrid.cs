using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Typography = Microsoft.UI.Xaml.Documents.Typography;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6317] [S-6319] The CPU meter's Core graphs: a tile per logical processor in the Core grid's
/// arrangement (<see cref="BarLayout.Tiles"/>), each that core's history drawn by the one sparkline
/// every meter draws (<see cref="SparkBand"/>: edge to edge in the tile, stepping as the main graph
/// does, [S-6331]) in its load's pressure colour, its load over the top left
/// (<see cref="CoreGraphLayout.Tile"/>) shown as it is read ([S-6330]). Tiles are kept and
/// rewritten in place; the history comes from the hub ([S-6318]), so a grid built again draws it at once.
/// </summary>
public sealed partial class CoreGraphGrid : Grid
{
    /// <summary>The tile's own wash: the columns' track, so the lines lead.</summary>
    private const double TrackOpacity = 0.3;

    private readonly List<Tile> _tiles = [];
    private TileGridPlan _plan;
    private int _capacity = MeterTiming.Points(60, SystemCadence.TickSeconds);

    public CoreGraphGrid()
    {
        // [S-1113] The first core is on the left, and time runs left to right, in every language.
        FlowDirection = FlowDirection.LeftToRight;
        IsHitTestVisible = false;
    }

    public int Count => _tiles.Count;

    /// <summary>[S-1121] The span every tile's history shows, in points.</summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = value;

            foreach (var tile in _tiles)
            {
                tile.Band.Capacity = value;
            }
        }
    }

    /// <summary>[S-6331] How many of a core's newest points a tile keeps (its span and the folding's lead).</summary>
    public int Holds => _tiles.Count > 0 ? _tiles[0].Band.Holds : _capacity + SparkSlots.Lead(1);

    /// <summary>Keep exactly <paramref name="count"/> tiles, the existing ones kept.</summary>
    public void SetCount(int count)
    {
        count = Math.Max(0, count);

        while (_tiles.Count > count)
        {
            Children.Remove(_tiles[^1].Root);
            _tiles.RemoveAt(_tiles.Count - 1);
        }

        while (_tiles.Count < count)
        {
            var tile = new Tile(_capacity);
            _tiles.Add(tile);
            Children.Add(tile.Root);
        }

        Place();
    }

    /// <summary>[S-6318] Draw a core's history as the hub holds it, replacing what its tile held.</summary>
    public void Load(int index, IReadOnlyList<SeriesPoint> points, long added)
    {
        if ((uint)index < (uint)_tiles.Count)
        {
            _tiles[index].Band.Load(points, added);
        }
    }

    /// <summary>One read of a core: a point on its history when <paramref name="point"/>, and its figure and colour.</summary>
    public void Show(int index, double? load, bool point)
    {
        if ((uint)index >= (uint)_tiles.Count)
        {
            return;
        }

        var tile = _tiles[index];
        var known = load is { } l && double.IsFinite(l);

        if (point && known)
        {
            tile.Band.Push(load!.Value);
        }

        tile.Band.SetBrush(MeterBrushes.Pressure(MeterPressures.For(PressureScale.Usage, known ? load : null)));

        // [S-6330] A whole percent, at once, as the reading.
        tile.ShowText(known ? Formatting.Percent(Math.Clamp(load!.Value, 0, 1)) : "--");
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

    /// <summary>Each tile at its cell, row by row from the top left, its figure at the step the tile takes.</summary>
    private void Place()
    {
        var plan = _plan;
        if (plan.Columns <= 0)
        {
            return;
        }

        var radius = Math.Clamp(Math.Min(plan.TileWidth, plan.TileHeight) / 6, 1, Fluent.ControlRadius);
        var tilePlan = CoreGraphLayout.Tile(plan.TileWidth, plan.TileHeight, Formatting.Percent(1).Length);

        for (var i = 0; i < _tiles.Count; i++)
        {
            var column = i % plan.Columns;
            var row = i / plan.Columns;
            _tiles[i].Lay(plan.TileWidth, plan.TileHeight, radius, tilePlan);
            _tiles[i].Root.Margin = new Thickness(column * (plan.TileWidth + plan.Gap), row * (plan.TileHeight + plan.Gap), 0, 0);
        }
    }

    /// <summary>One core: its wash, its history and its figure.</summary>
    private sealed class Tile
    {
        private readonly TextBlock _text;
        private TypeStep? _step;
        private string _shown = string.Empty;

        public Tile(int capacity)
        {
            var wash = new Rectangle { Opacity = TrackOpacity };
            ItemFill.Paint(wash, ItemRole.Track);
            Band = new SparkBand(capacity, ChartScale.Share);

            _text = Fluent.Text(FluentType.Caption, "TextFillColorPrimaryBrush");
            _text.HorizontalAlignment = HorizontalAlignment.Left;
            _text.VerticalAlignment = VerticalAlignment.Top;
            _text.Visibility = Visibility.Collapsed;
            Typography.SetNumeralAlignment(_text, FontNumeralAlignment.Tabular);

            Root = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new Grid { Children = { wash, Band, _text } },
            };
        }

        public Border Root { get; }

        public SparkBand Band { get; }

        public void ShowText(string text)
        {
            if (text != _shown)
            {
                _shown = text;
                _text.Text = text;
            }
        }

        public void Lay(double width, double height, double radius, CoreGraphTilePlan plan)
        {
            Root.Width = Math.Max(0, width);
            Root.Height = Math.Max(0, height);
            Root.CornerRadius = new CornerRadius(radius);

            if (plan.Figure is not { } step)
            {
                _text.Visibility = Visibility.Collapsed;
                Band.Margin = new Thickness(0);
                return;
            }

            // The figure stands over the history, not on it: a pegged core's line would cross it.
            _text.Visibility = Visibility.Visible;
            _text.Margin = new Thickness(plan.Inset, plan.Inset / 2, plan.Inset, 0);
            Band.Margin = new Thickness(0, step.LineHeight + (plan.Inset / 2), 0, 0);

            if (step != _step)
            {
                _step = step;
                _text.Apply(step);
            }
        }
    }
}
