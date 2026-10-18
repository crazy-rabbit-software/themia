using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Tasks;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-782]/[S-786] The task rows: one column, or two filled column by column (reading order kept),
/// with the two cells of a line sharing its height so a wrapped title never knocks the columns out
/// of step. It also keeps the drag's state: the order rows are laid out in while one is lifted, and
/// where the lifted row is drawn. Reordering during a drag is a change of layout order, never of the
/// visual tree, so the lifted row keeps its pointer capture.
/// </summary>
internal sealed partial class TaskRowsPanel : Panel
{
    private readonly List<double> _lineBottoms = [];
    private readonly List<bool> _lineHeadings = [];
    private readonly List<SlotBox> _slots = [];

    /// <summary>Slot → child index. Identity except while a drag has moved a row.</summary>
    private readonly List<int> _order = [];

    private int _columns = 1;
    private UIElement? _lifted;
    private Point _liftedAt;

    public int Columns
    {
        get => _columns;
        set
        {
            var columns = Math.Max(1, value);
            if (columns != _columns)
            {
                _columns = columns;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The bottom of each line of rows, top to bottom, as last measured.</summary>
    public IReadOnlyList<double> LineBottoms => _lineBottoms;

    /// <summary>Per line, whether it holds only a section heading (no task row).</summary>
    public IReadOnlyList<bool> LineHeadings => _lineHeadings;

    /// <summary>Every slot's box as last arranged, in list order.</summary>
    public IReadOnlyList<SlotBox> Slots => _slots;

    public UIElement ChildAt(int slot)
    {
        EnsureOrder();
        return Children[_order[slot]];
    }

    public int SlotOf(UIElement child)
    {
        EnsureOrder();
        return _order.IndexOf(Children.IndexOf(child));
    }

    /// <summary>The children in the order they are laid out; read it at once (it follows the panel).</summary>
    public IEnumerable<UIElement> InSlotOrder()
    {
        EnsureOrder();
        return _order.Select(i => Children[i]);
    }

    /// <summary>Back to the children's own order: after a rebuild, or a drag let go without a drop.</summary>
    public void ResetOrder()
    {
        _order.Clear();
        Settle();
        InvalidateMeasure();
    }

    public void Lift(UIElement child)
    {
        _lifted = child;
        Canvas.SetZIndex(child, 1);
        child.Opacity = 1;
    }

    /// <summary>Draw the lifted row with its top-left corner here (panel coordinates).</summary>
    public void MoveLifted(Point topLeft)
    {
        _liftedAt = topLeft;
        InvalidateArrange();
    }

    public void MoveTo(UIElement child, int slot)
    {
        EnsureOrder();
        var from = SlotOf(child);
        if (from < 0 || slot < 0 || slot >= _order.Count || from == slot)
        {
            return;
        }

        var index = _order[from];
        _order.RemoveAt(from);
        _order.Insert(slot, index);
        InvalidateMeasure();
    }

    public void Settle()
    {
        if (_lifted is not null)
        {
            Canvas.SetZIndex(_lifted, 0);
            _lifted = null;
            InvalidateArrange();
        }
    }

    /// <summary>
    /// [S-781] Fade the lines near the bottom of the viewport while there is more below. Opacity, not
    /// a gradient painted over the rows: the card can be transparent, and a gradient to "the card's
    /// colour" would draw a band over the wallpaper.
    /// </summary>
    public void ApplyFade(double viewBottom, bool moreBelow)
    {
        EnsureOrder();
        var lines = TaskLayout.LinesFor(_order.Count, _columns);

        for (var slot = 0; slot < _order.Count; slot++)
        {
            var child = Children[_order[slot]];
            var line = slot % Math.Max(1, lines);

            if (ReferenceEquals(child, _lifted) || line >= _lineBottoms.Count)
            {
                child.Opacity = 1;
                continue;
            }

            var top = line == 0 ? 0 : _lineBottoms[line - 1];
            child.Opacity = TaskLayout.FadeOpacity(top, _lineBottoms[line], viewBottom, moreBelow);
        }
    }

    private void EnsureOrder()
    {
        if (_order.Count == Children.Count)
        {
            return;
        }

        _order.Clear();
        for (var i = 0; i < Children.Count; i++)
        {
            _order.Add(i);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureOrder();

        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var columnWidth = TaskLayout.ColumnWidth(width, _columns);
        var lines = TaskLayout.LinesFor(_order.Count, _columns);
        var heights = new double[lines];
        var rowsIn = new bool[lines];

        for (var slot = 0; slot < _order.Count; slot++)
        {
            var child = Children[_order[slot]];
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            var line = slot % lines;
            heights[line] = Math.Max(heights[line], child.DesiredSize.Height);
            rowsIn[line] |= child is not TextBlock;
        }

        _lineHeadings.Clear();
        _lineHeadings.AddRange(rowsIn.Select(hasRow => !hasRow));

        _lineBottoms.Clear();
        var total = 0.0;
        foreach (var height in heights)
        {
            total += height;
            _lineBottoms.Add(total);
        }

        return new Size(width, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureOrder();

        var columnWidth = TaskLayout.ColumnWidth(finalSize.Width, _columns);
        var lines = TaskLayout.LinesFor(_order.Count, _columns);
        _slots.Clear();

        for (var slot = 0; slot < _order.Count; slot++)
        {
            var child = Children[_order[slot]];
            var line = slot % lines;
            var column = slot / lines;
            var top = line == 0 ? 0 : (line - 1 < _lineBottoms.Count ? _lineBottoms[line - 1] : 0);
            var height = line < _lineBottoms.Count ? _lineBottoms[line] - top : child.DesiredSize.Height;
            var box = new SlotBox(column * (columnWidth + TaskLayout.ColumnGap), top, columnWidth, height);
            _slots.Add(box);

            var rect = ReferenceEquals(child, _lifted)
                ? new Rect(_liftedAt.X, _liftedAt.Y, box.Width, box.Height)
                : new Rect(box.X, box.Y, box.Width, box.Height);

            child.Arrange(rect);
        }

        return finalSize;
    }
}

/// <summary>
/// [S-781] Hosts the rows' scroll viewer and gives it only the height of the lines that fit whole,
/// so the list never ends in a row sliced through at the card's edge; the rest is a scroll away and
/// the bottom lines fade to say so. Decided in the layout pass from the measured lines.
/// </summary>
internal sealed partial class TaskListHost : Panel
{
    private readonly ScrollViewer _scroll;
    private readonly TaskRowsPanel _rows;
    private double _viewHeight;

    public TaskListHost(ScrollViewer scroll, TaskRowsPanel rows)
    {
        _scroll = scroll;
        _rows = rows;
        Children.Add(scroll);

        // Rebuilt rows change the panel but not this host's size, which would keep the old cap. The
        // three live and die together, so the handlers need no unhooking.
        _rows.SizeChanged += (_, _) => InvalidateArrange();
        _scroll.ViewChanged += (_, _) => Fade();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _scroll.Measure(availableSize);
        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : _scroll.DesiredSize.Width,
            double.IsFinite(availableSize.Height) ? availableSize.Height : _scroll.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = _scroll.Padding.Top + _scroll.Padding.Bottom;
        var room = Math.Max(0, finalSize.Height - padding);
        var lines = _rows.LineBottoms;
        var content = lines.Count > 0 ? lines[^1] : 0;

        // All of it when it fits; otherwise down to the last whole line.
        _viewHeight = content <= room + 0.5 ? content : TaskLayout.WholeLineCap(lines, room, _rows.LineHeadings);
        var height = Math.Min(finalSize.Height, _viewHeight + padding);

        _scroll.Arrange(new Rect(0, 0, finalSize.Width, height));
        Fade();
        return finalSize;
    }

    private void Fade()
    {
        var lines = _rows.LineBottoms;
        var content = lines.Count > 0 ? lines[^1] : 0;
        var viewBottom = _scroll.VerticalOffset + _viewHeight;
        _rows.ApplyFade(viewBottom, moreBelow: viewBottom < content - 0.5);
    }
}
